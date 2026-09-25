using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// TEMPORARY diagnostic driver. Moves the real First Person Controller using genuine
/// synthesized Input System key events (same Keyboard.current state as a physical player)
/// and drives FirstPersonLook's internal velocity the way real mouse deltas do.
/// Navigation = grid BFS computed from real physics; the player still WALKS it.
/// Does NOT teleport the player.
/// </summary>
public class DiagWalker : MonoBehaviour
{
    public static DiagWalker Instance { get; private set; }

    Rigidbody rb;
    Camera cam;
    Component look;         // FirstPersonLook (different assembly; accessed via reflection)
    FieldInfo lookVelocity;
    Coroutine routine;

    public string Log { get; private set; }

    void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody>();
        cam = GetComponentInChildren<Camera>();

        look = null;
        foreach (var c in GetComponentsInChildren<Component>(true))
        {
            if (c.GetType().Name == "FirstPersonLook")
            {
                look = c;
                break;
            }
        }
        if (look != null)
            lookVelocity = look.GetType().GetField("velocity", BindingFlags.NonPublic | BindingFlags.Instance);
        Log = "DiagWalker ready (cam=" + (cam != null) + " look=" + (look != null) + ")";
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetLog(string s) { Log = s; Debug.Log("[DiagWalker] " + s); }
    public Vector3 GetPos() { return transform.position; }
    public bool IsBusy() { return routine != null; }

    public void StopAll()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        QueueKey(Key.W, false);
        QueueKey(Key.E, false);
        InputSystem.Update();
        ReleaseMouse();
    }

    public bool WalkTo(Vector3 target, float timeout = 90f)
    {
        StartRoutine(Co_Walk(target, timeout, 0.45f));
        return true;
    }

    // Wall-aware auto navigation: tries BFS; if BFS fails, opens any closed
    // DoorInteraction blocking the way, then retries. Uses only real input.
    public bool GoTo(Vector3 target, float timeout = 180f)
    {
        StartRoutine(Co_GoTo(target, timeout));
        return true;
    }

    public bool AimAt(Vector3 target)
    {
        StartRoutine(Co_Aim(target, 6f));
        return true;
    }

    public bool WalkToAndE(Vector3 target)
    {
        StartRoutine(Co_WalkThenAimThenE(target));
        return true;
    }

    public bool AimAndE(Vector3 target)
    {
        StartRoutine(Co_AimThenE(target));
        return true;
    }

    void StartRoutine(IEnumerator co)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(co);
    }

    IEnumerator Co_WalkThenAimThenE(Vector3 target)
    {
        float arrive = 1.1f;
        yield return Co_Walk(new Vector3(target.x, transform.position.y, target.z), 90f, arrive);
        yield return Co_Aim(target, 6f);
        yield return Co_PressE();
    }

    IEnumerator Co_AimThenE(Vector3 target)
    {
        yield return Co_Aim(target, 6f);
        yield return Co_PressE();
    }

    // ---- Pathfinding ----
    const float GRID = 0.35f;
    readonly Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

    bool CellBlocked(Vector3 center)
    {
        // sample at chest height on the real nav space; exclude self and window/tape colliders
        var hits = Physics.OverlapSphere(center, 0.22f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (c == null || c.isTrigger) continue;
            Transform t = c.transform;
            while (t != null)
            {
                if (t == transform) goto skip;
                t = t.parent;
            }
            // ignore window & tape hosts (thin plates, why-grid navigation)
            string n = c.name;
            if (n.StartsWith("window") || n.StartsWith("tape") || n=="Tape Seal Surface") goto skip;
            return true;
            skip:;
        }
        return false;
    }

    IEnumerable<Vector2Int> Neighbors(Vector2Int c)
    {
        yield return c + new Vector2Int(1, 0);
        yield return c + new Vector2Int(-1, 0);
        yield return c + new Vector2Int(0, 1);
        yield return c + new Vector2Int(0, -1);
        yield return c + new Vector2Int(1, 1);
        yield return c + new Vector2Int(1, -1);
        yield return c + new Vector2Int(-1, 1);
        yield return c + new Vector2Int(-1, -1);
    }

    Vector2Int ToCell(Vector3 w)
    {
        return new Vector2Int(Mathf.RoundToInt(w.x / GRID), Mathf.RoundToInt(w.z / GRID));
    }

    Vector3 CellCenter(Vector2Int c)
    {
        return new Vector3(c.x * GRID, transform.position.y + 0.55f, c.y * GRID);
    }

    List<Vector3> FindPath(Vector3 from, Vector3 to)
    {
        Vector2Int start = ToCell(from), goal = ToCell(to);
        cameFrom.Clear();
        var frontier = new Queue<Vector2Int>();
        frontier.Enqueue(start);
        cameFrom[start] = start;

        while (frontier.Count > 0)
        {
            Vector2Int cur = frontier.Dequeue();
            if (cur == goal) break;
            foreach (Vector2Int nb in Neighbors(cur))
            {
                if (cameFrom.ContainsKey(nb)) continue;
                if (CellBlocked(CellCenter(nb))) continue;
                cameFrom[nb] = cur;
                frontier.Enqueue(nb);
            }
        }

        if (!cameFrom.ContainsKey(goal))
        {
            SetLog("Pathfinding FAILED from " + from.ToString("F2") + " to " + to.ToString("F2"));
            return null;
        }

        var rev = new List<Vector2Int>();
        Vector2Int g = goal;
        while (g != start) { rev.Add(g); g = cameFrom[g]; }
        rev.Reverse();

        var path = new List<Vector3>();
        int skipCount = 0;
        for (int i = 1; i < rev.Count; i++)
        {
            Vector3 c = CellCenter(rev[i]);
            if (skipCount > 0) { skipCount--; continue; }
            // keep waypoints - simple, then simplify straight runs later if needed
            path.Add(c);
        }
        if (path.Count == 0) path.Add(CellCenter(goal));
        return path;
    }

    // ---- Walk (path-following with real input) ----
    IEnumerator Co_GoTo(Vector3 goal, float timeout)
    {
        SetLog("Co_GoTo -> " + goal.ToString("F2"));
        float t0 = Time.time;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            if (Time.time - t0 > timeout) { SetLog("GoTo TIMEOUT"); break; }
            float distGoal = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(goal.x, goal.z));
            if (distGoal < 0.7f) { SetLog("GoTo reached after " + (Time.time - t0).ToString("F1") + "s"); break; }

            var path = FindPath(transform.position, goal);
            if (path != null)
            {
                yield return Co_Walk(goal, 45f, 0.6f);
                if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(goal.x, goal.z)) < 0.7f)
                    break;
                SetLog("GoTo: stopped short (t=" + (Time.time - t0).ToString("F1") + "), find door");
            }

            // Path blocked -> try opening the nearest closed DoorInteraction ahead.
            Transform ddoor; Vector3 approach;
            if (!FindBlockingDoor(out ddoor, out approach))
            {
                SetLog("GoTo: no openable door found at attempt " + attempt);
                yield break;
            }
            yield return Co_OpenDoorSeq(ddoor, approach);
            yield return new WaitForSeconds(1.0f); // let door finish animating
        }
        SetLog("GoTo final pos=" + transform.position.ToString("F2"));
    }

    // Finds nearest closed DoorInteraction and a walk point 1.15m in front of it.
    bool FindBlockingDoor(out Transform best, out Vector3 approach)
    {
        best = null; approach = default(Vector3);
        float bx = float.MaxValue;
        var all = Object.FindObjectsOfType<Transform>(true);
        foreach (var t in all)
        {
            var comp = t.GetComponent("DoorInteraction");
            if (comp == null) continue;
            var isOpen = (bool)comp.GetType().GetProperty("IsOpen").GetValue(comp, null);
            if (isOpen) continue;
            float d = Vector3.SqrMagnitude(t.position - transform.position);
            if (d < bx) { bx = d; best = t; }
        }
        if (best == null) return false;
        Vector3 toDoor = best.position - transform.position; toDoor.y = 0;
        approach = best.position - toDoor.normalized * 1.15f;
        approach.y = transform.position.y;
        return true;
    }

    IEnumerator Co_OpenDoorSeq(Transform door, Vector3 approach)
    {
        var comp = door.GetComponent("DoorInteraction");
        var prop = comp.GetType().GetProperty("IsOpen");
        yield return Co_Walk(approach, 40f, 1.0f);
        SetLog("Opening door " + door.name + " in front");

        float deadline = Time.time + 25f;
        while (Time.time < deadline)
        {
            if ((bool)prop.GetValue(comp, null)) { SetLog("Door opened"); yield break; }
            float d = Vector3.Distance(transform.position, door.position);
            if (d > 3.0f) { SetLog("Door too far (" + d.ToString("F1") + ")"); break; }
            yield return Co_Aim(door.position, 3f);
            yield return Co_PressE();
            yield return new WaitForSeconds(0.6f);
        }
        SetLog("OpenDoor deadline");
    }

    IEnumerator Co_Walk(Vector3 goal, float timeout, float arriveRadius)
    {
        var path = FindPath(transform.position, goal);
        if (path == null) yield break;

        SetLog("Path waypoints=" + path.Count);
        float t0 = Time.time;
        int idx = 0;
        int stuck = 0;

        while (true)
        {
            if (Time.time - t0 > timeout) { SetLog("Walk TIMEOUT"); break; }

            Vector2 flatPos = new Vector2(transform.position.x, transform.position.z);
            Vector2 goalFlat = new Vector2(goal.x, goal.z);
            float distToFinal = Vector2.Distance(flatPos, goalFlat);
            if (distToFinal < arriveRadius)
            {
                SetLog("Reached goal after " + (Time.time - t0).ToString("F1") + "s");
                break;
            }

            // advance waypoint index when close to current wp or directly visible
            if (idx >= path.Count)
            {
                // fall back to direct drive to final goal
                Vector3 toG = goal - transform.position; toG.y = 0;
                float yaw = Mathf.Atan2(toG.x, toG.z) * Mathf.Rad2Deg;
                SetLookYaw(yaw);
                Vector3 before = transform.position;
                QueueKey(Key.W, true); InputSystem.Update();
                yield return null; yield return null;
                QueueKey(Key.W, false); InputSystem.Update();
                if ((transform.position - before).sqrMagnitude < 0.0002f) { stuck++; if (stuck > 120) { SetLog("Stuck final"); break; } }
                continue;
            }

            Vector3 wp = path[idx];
            if (idx > 0)
            {
                Vector3 prev = path[idx - 1];
                if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(prev.x, prev.z)) < 0.1f && Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(wp.x, wp.z)) < 0.6f)
                {
                    idx++;
                    continue;
                }
            }
            else if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(wp.x, wp.z)) < 0.5f)
            {
                idx++;
                continue;
            }

            Vector3 toWp = wp - transform.position; toWp.y = 0;
            float wpYaw = Mathf.Atan2(toWp.x, toWp.z) * Mathf.Rad2Deg;
            float curYaw = transform.eulerAngles.y;
            float dyaw = Mathf.DeltaAngle(curYaw, wpYaw);

            if (Mathf.Abs(dyaw) > 3f)
            {
                SetLookYaw(wpYaw);
                yield return null;
            }
            else
            {
                Vector3 before = transform.position;
                QueueKey(Key.W, true); InputSystem.Update();
                yield return null; yield return null;
                QueueKey(Key.W, false); InputSystem.Update();
                if ((transform.position - before).sqrMagnitude < 0.0002f)
                {
                    stuck++;
                    if (stuck > 60) { SetLog("Stuck at wp " + idx + " pos=" + transform.position.ToString("F2")); break; }
                }
            }
        }
        QueueKey(Key.W, false);
        InputSystem.Update();
    }

    IEnumerator Co_Aim(Vector3 target, float timeout)
    {
        SetLog("Aim -> " + target.ToString("F2"));
        float t0 = Time.time;
        while (Time.time - t0 < timeout)
        {
            Vector3 toT = target - cam.transform.position;
            float goalYaw = Mathf.Atan2(toT.x, toT.z) * Mathf.Rad2Deg;
            float curYaw = transform.eulerAngles.y;
            float dyaw = Mathf.DeltaAngle(curYaw, goalYaw);

            Vector3 flat = new Vector3(toT.x, 0f, toT.z);
            float dist = flat.magnitude;
            float goalPitch = Mathf.Atan2(toT.y, dist) * Mathf.Rad2Deg; // positive = look up
            float curPitch = cam.transform.eulerAngles.x;
            if (curPitch > 180f) curPitch -= 360f;
            float dpitch = goalPitch - curPitch;

            if (Mathf.Abs(dyaw) < 0.4f && Mathf.Abs(dpitch) < 0.4f)
            {
                SetLog("Aim settled: fwd=" + cam.transform.forward.ToString("F2"));
                break;
            }

            SetLookPose(goalYaw, goalPitch);
            yield return null;
        }
        SetLog("Aim done camFwd=" + cam.transform.forward.ToString("F2"));
    }

    IEnumerator Co_PressE()
    {
        QueueKey(Key.E, true);
        SetLog("E press queued (frame " + Time.frameCount + ")");
        yield return null; // engine input update consumes press -> wasPressedThisFrame true
        QueueKey(Key.E, false);
        SetLog("E release queued");
        yield return null;
    }

    void SetLookYaw(float yaw)
    {
        if (lookVelocity == null || look == null) return;
        Vector2 v = (Vector2)lookVelocity.GetValue(look);
        v.x = yaw;
        lookVelocity.SetValue(look, v);
    }

    void SetLookPose(float yaw, float pitch)
    {
        if (lookVelocity == null || look == null) return;
        Vector2 v = new Vector2(yaw, -pitch);
        lookVelocity.SetValue(look, v);
    }

    void QueueKey(Key k, bool down)
    {
        if (Keyboard.current == null) return;
        var keys = down ? new Key[] { k } : new Key[0];
        var state = new KeyboardState(keys);
        InputSystem.QueueStateEvent<KeyboardState>(Keyboard.current, state, -1);
    }

    void ReleaseMouse()
    {
        if (Mouse.current == null) return;
        var st = new MouseState { position = Mouse.current.position.ReadValue(), delta = Vector2.zero };
        InputSystem.QueueStateEvent<MouseState>(Mouse.current, st, -1);
        InputSystem.Update();
    }
}