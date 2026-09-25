using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Runtime wiring for the home-preparation flow. Adds close/tape tasks to the existing windows,
/// the sealing-tape pickup, the main door close task, and the TV evacuation news to the scene.
/// </summary>
public static class HomePrepInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        Transform house = FindHouse();
        if (house == null)
        {
            Debug.LogWarning("HomePrepInstaller: authoritative house not found.");
            return;
        }

        Transform volh = house.Find("VOLh_objects");
        if (volh == null)
        {
            Debug.LogWarning("HomePrepInstaller: VOLh_objects not found under house.");
            return;
        }

        InstallWindow(house, volh.Find("window 1"), "window1", "tape01");
        InstallWindow(house, volh.Find("window 2"), "window2", "tape02");
        InstallWindow(house, volh.Find("window 3"), "window3", "tape03");
        InstallTape();
        InstallDoor(volh.Find("doors"));
        InstallTv(volh.Find("tv"));
    }

    static void InstallWindow(Transform house, Transform win, string closeId, string tapeId)
    {
        if (win == null)
        {
            Debug.LogWarning("HomePrepInstaller: window '" + closeId + "' not found.");
            return;
        }

        string displayName = "Window " + closeId.Substring(closeId.Length - 1);

        if (win.GetComponent<PreparationTask>() == null)
        {
            PreparationTask pt = win.gameObject.AddComponent<PreparationTask>();
            pt.taskId = closeId;
            pt.displayName = displayName;
        }
        EnsureBoxCollider(win);

        if (win.GetComponent<SealPrepVisual>() == null)
        {
            SealPrepVisual spv = win.gameObject.AddComponent<SealPrepVisual>();
            spv.taskId = closeId;
            spv.tapeTaskId = tapeId;
        }

        if (win.GetComponent<TapeSealTask>() == null)
        {
            GameObject tapeGo = new GameObject(win.name + " tape seal");
            tapeGo.transform.SetParent(win, false);
            tapeGo.transform.localPosition = Vector3.zero;
            tapeGo.transform.localScale = Vector3.one;

            TapeSealTask tst = tapeGo.AddComponent<TapeSealTask>();
            tst.taskId = tapeId;
            tst.displayName = displayName;
            tst.requiredCloseTaskId = closeId;

            GameObject strip = CreateTapeStrip();
            strip.transform.SetParent(tapeGo.transform, false);
            strip.transform.localPosition = Vector3.zero;
            tst.tapeObjects = new GameObject[] { strip };

            BoxCollider bc = tapeGo.AddComponent<BoxCollider>();
            // The collider box is authored in the window's rotated local space. Window 1 (rot 270/90/0)
            // maps local Y (thin) onto the wall plane and local X onto the approach axis; Windows 2 and 3
            // (rot 270/180/0 and 270/270/0) instead map local Y onto the player's approach axis, which
            // would turn their tape-seal into a 0.06m-thick knife-edge that the E rays can't reliably
            // surface. Author those tapes with the thin axis along the wall and the deep axis toward the
            // player, and lift them off the window's bottom edge so they present a real, hittable face
            // inward.
            bool deepAxisAlongApproach = tapeId == "tape02" || tapeId == "tape03";
            bc.size = deepAxisAlongApproach ? new Vector3(0.06f, 0.9f, 0.9f) : new Vector3(0.9f, 0.06f, 0.9f);
            PositionTapeSealCollider(house, win, tapeGo.transform, win.GetComponent<Collider>(), bc, deepAxisAlongApproach ? 0.4f : 0f);
        }
    }

    // The tape-seal collider is created at the window's local origin, which is nested INSIDE the
    // window's own interaction collider. A single raycast from inside the house therefore always
    // hits the window collider first and never reaches the tape-seal surface. Move the tape-seal
    // collider so its inward-facing face sits just INSIDE the room, past the window collider face,
    // centered on the visible tape strip (not the tall window mesh collider).
    static void PositionTapeSealCollider(Transform house, Transform win, Transform tapeGo, Collider winCol, BoxCollider bc, float liftUp = 0f)
    {
        if (house == null || winCol == null) return;

        Vector3 h = house.position;
        Vector3 inward = new Vector3(h.x - win.position.x, 0f, h.z - win.position.z);
        if (inward.sqrMagnitude < 0.0001f) inward = win.forward;
        inward.Normalize();

        Bounds wb = winCol.bounds;
        float halfIn = Mathf.Abs(Vector3.Dot(wb.extents, inward));
        float halfTape = Mathf.Abs(Vector3.Dot(bc.bounds.extents, inward));
        float gap = 0.12f;

        // Anchor on the strip position so altitude stays at the visible glass, then push along
        // 'inward' (toward the house interior) by the window-collider half depth + gap, then back
        // off by the tape collider's own half depth so its inward face ends up exactly 'gap' past
        // the window collider's interior face.
        Vector3 worldAnchor = tapeGo.position + Vector3.up * liftUp;
        Vector3 worldCenter = worldAnchor + inward * (halfIn + gap - halfTape);
        bc.center = tapeGo.InverseTransformPoint(worldCenter);
    }

    static void InstallTape()
    {
        GameObject tape = GameObject.Find("tape");
        if (tape == null)
        {
            Debug.LogWarning("HomePrepInstaller: sealing tape pickup 'tape' not found.");
            return;
        }

        if (tape.GetComponent<SealingTape>() == null)
        {
            SealingTape st = tape.AddComponent<SealingTape>();
            st.requiredCloseTaskId = "window1";
        }
        EnsureBoxCollider(tape.transform);
    }

    static void InstallDoor(Transform doors)
    {
        if (doors == null)
        {
            Debug.LogWarning("HomePrepInstaller: 'doors' node not found.");
            return;
        }
        Transform doorFbx = doors.Find("door.fbx");
        if (doorFbx == null)
        {
            Debug.LogWarning("HomePrepInstaller: 'door.fbx' not found under doors.");
            return;
        }

        if (doorFbx.GetComponent<PrepDoorTask>() == null)
        {
            PrepDoorTask pdt = doorFbx.gameObject.AddComponent<PrepDoorTask>();
            pdt.taskId = "door";
            pdt.displayName = "Main Door";
        }

        if (doorFbx.GetComponent<SealPrepVisual>() == null)
        {
            SealPrepVisual spv = doorFbx.gameObject.AddComponent<SealPrepVisual>();
            spv.taskId = "door";
            spv.tapeTaskId = "";
        }
    }

    static void InstallTv(Transform tv)
    {
        if (tv == null)
        {
            Debug.LogWarning("HomePrepInstaller: 'tv' not found.");
            return;
        }

        if (tv.GetComponent<TVController>() == null)
        {
            tv.gameObject.AddComponent<TVController>();
            if (tv.GetComponent<VideoPlayer>() == null)
                tv.gameObject.AddComponent<VideoPlayer>();
        }
    }

    static Transform FindHouse()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != "house") continue;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.gameObject.name == "Plane_Material.001_0")
                    return root.transform;
            }
        }
        return null;
    }

    static void EnsureBoxCollider(Transform target)
    {
        if (target.GetComponent<Collider>() != null) return;

        Bounds b = new Bounds(target.position, Vector3.zero);
        bool has = false;
        foreach (MeshRenderer mr in target.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!has) { b = mr.bounds; has = true; }
            else b.Encapsulate(mr.bounds);
        }
        if (!has)
        {
            Debug.LogWarning("HomePrepInstaller: no renderer bounds found for " + target.name + ".");
            return;
        }

        BoxCollider bc = target.gameObject.AddComponent<BoxCollider>();
        bc.center = target.InverseTransformPoint(b.center);
        Vector3 s = target.InverseTransformVector(b.size);
        bc.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    static GameObject CreateTapeStrip()
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Tape Strip";
        Collider c = quad.GetComponent<Collider>();
        if (c != null) Object.Destroy(c);
        MeshRenderer mr = quad.GetComponent<MeshRenderer>();
        Shader sh = Shader.Find("Sprites/Default");
        if (sh != null && mr != null)
        {
            Material m = new Material(sh);
            m.color = new Color(0.75f, 0.72f, 0.68f, 1f);
            mr.sharedMaterial = m;
        }
        quad.transform.localScale = new Vector3(0.8f, 0.12f, 1f);
        return quad;
    }
}