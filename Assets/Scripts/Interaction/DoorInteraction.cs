using UnityEngine;

public class DoorInteraction : Interactable
{
    [Tooltip("Maximum open angle in degrees.")]
    public float openAngle = 90f;

    [Tooltip("Degrees per second while opening/closing.")]
    public float openSpeed = 180f;

    [Tooltip("+1 or -1: which way the door swings around its hinge.")]
    public int openDirection = 1;

    [Tooltip("Place the hinge at the min or max edge of the door along its width axis.")]
    public bool hingeAtMinEdge = true;

    [Tooltip("If true, pick openDirection so the free edge swings away from the parent house.")]
    public bool autoOpenDirection = true;

    [Tooltip("Name used for the runtime hinge/pivot GameObject.")]
    public string pivotName = "";

    public bool IsOpen { get; private set; }

    Transform hinge;
    BoxCollider doorCollider;
    Vector3 widthAxis;
    Vector3 hingeWorld;
    float currentAngle;
    float targetAngle;
    bool animating;

    void Start()
    {
        prompt = "Open Door";

        doorCollider = GetComponent<BoxCollider>();
        if (doorCollider == null)
            doorCollider = gameObject.AddComponent<BoxCollider>();

        SetupCollider();
        SetupHinge();
        if (autoOpenDirection)
            ResolveOpenDirection();

        OnStart();
    }

    void SetupCollider()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) return;

        Bounds wb = mr.bounds;
        doorCollider.center = transform.InverseTransformPoint(wb.center);
        Vector3 s = transform.InverseTransformVector(wb.size);
        doorCollider.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    void SetupHinge()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) return;

        Bounds wb = mr.bounds;
        bool xWide = wb.size.x >= wb.size.z;
        Vector3 pos = wb.center;
        float half = xWide ? wb.size.x * 0.5f : wb.size.z * 0.5f;
        widthAxis = xWide ? Vector3.right : Vector3.forward;
        if (xWide)
            pos.x += hingeAtMinEdge ? -half : half;
        else
            pos.z += hingeAtMinEdge ? -half : half;

        hingeWorld = pos;

        GameObject hingeGo = new GameObject(string.IsNullOrEmpty(pivotName) ? (name + "_Hinge") : pivotName);
        hingeGo.transform.position = pos;
        hingeGo.transform.rotation = Quaternion.identity;
        transform.SetParent(hingeGo.transform, true);
        hinge = hingeGo.transform;
        currentAngle = 0f;
        targetAngle = 0f;
    }

    void ResolveOpenDirection()
    {
        if (hinge == null) return;

        Transform house = transform;
        while (house != null && house.name != "house")
            house = house.parent;
        if (house == null) return;

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) return;

        Vector3 houseXZ = new Vector3(house.position.x, 0f, house.position.z);
        Vector3 freeEdge = mr.bounds.center + widthAxis * (hingeAtMinEdge ? 0.5f : -0.5f)
            * (widthAxis == Vector3.right ? mr.bounds.size.x : mr.bounds.size.z);
        freeEdge.y = 0f;

        Vector3 closedOut = freeEdge - houseXZ;
        Vector3 afterPlus = hingeWorld + Quaternion.Euler(0f, openAngle, 0f) * (freeEdge - hingeWorld);
        Vector3 afterMinus = hingeWorld + Quaternion.Euler(0f, -openAngle, 0f) * (freeEdge - hingeWorld);
        afterPlus.y = 0f;
        afterMinus.y = 0f;
        afterPlus -= houseXZ;
        afterMinus -= houseXZ;

        float plusScore = Vector3.Dot(afterPlus.normalized, closedOut.normalized);
        float minusScore = Vector3.Dot(afterMinus.normalized, closedOut.normalized);
        openDirection = plusScore >= minusScore ? 1 : -1;
    }

    void Update()
    {
        if (!animating || hinge == null) return;

        float maxDelta = openSpeed * Time.deltaTime;
        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, maxDelta);
        hinge.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
        if (Mathf.Approximately(currentAngle, targetAngle))
        {
            animating = false;
            if (doorCollider != null)
                doorCollider.enabled = true;
        }
    }

    public override string InteractionPrompt
    {
        get { return IsOpen ? "Close Door" : "Open Door"; }
    }

    public override bool CanInteract
    {
        get { return canInteract && !animating; }
    }

    public override void Interact(GameObject interactor)
    {
        if (animating || hinge == null) return;

        IsOpen = !IsOpen;
        prompt = IsOpen ? "Close Door" : "Open Door";
        float dir = openDirection >= 0 ? 1f : -1f;
        targetAngle = IsOpen ? openAngle * dir : 0f;
        animating = true;
        if (doorCollider != null)
            doorCollider.enabled = false;
        InvokeInteractionComplete();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallAuthoritativeHouseDoors()
    {
        Transform house = FindAuthoritativeHouse();
        if (house == null)
        {
            Debug.LogWarning("DoorInteraction: authoritative house not found.");
            return;
        }

        Transform root = house.Find("VOLh_objects/doors/door.fbx/RootNode.001");
        if (root == null)
        {
            Debug.LogWarning("DoorInteraction: door root path not found under house.");
            return;
        }

        Transform plane = root.Find("Plane.001");
        Transform doorNode = root.Find("Door.002");
        if (plane == null || doorNode == null)
        {
            Debug.LogWarning("DoorInteraction: door containers not found under root.");
            return;
        }

        Transform[] panels = new Transform[]
        {
            plane.Find("Plane_Material.001_0"),
            plane.Find("Plane_Material.001_0.002")
        };
        Transform[] handles = new Transform[]
        {
            doorNode.Find("Door.002_Material.009_0"),
            doorNode.Find("Door.002_Material.009_0.002")
        };

        int installed = 0;
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] == null)
                continue;
            if (panels[i].GetComponentInParent<DoorInteraction>() != null)
                continue;

            Transform handle = ClosestHandle(handles, panels[i].position);

            if (handle != null && handle.parent != panels[i])
                handle.SetParent(panels[i], true);

            DoorInteraction di = panels[i].gameObject.AddComponent<DoorInteraction>();
            di.pivotName = i == 0 ? "Door_01_Pivot" : "Door_02_Pivot";
            di.openAngle = 90f;
            di.openSpeed = 180f;
            di.autoOpenDirection = false;
            if (i == 0)
            {
                di.hingeAtMinEdge = false;
                di.openDirection = 1;
            }
            else
            {
                di.hingeAtMinEdge = true;
                di.openDirection = -1;
            }
            installed++;
        }

        if (installed < 2)
            Debug.LogWarning("DoorInteraction: expected 2 authoritative house doors, installed " + installed + ".");
    }

    static Transform ClosestHandle(Transform[] handles, Vector3 worldPos)
    {
        Transform best = null;
        float bestSqr = float.MaxValue;
        Vector3 p = new Vector3(worldPos.x, 0f, worldPos.z);
        for (int i = 0; i < handles.Length; i++)
        {
            if (handles[i] == null) continue;
            Vector3 h = new Vector3(handles[i].position.x, 0f, handles[i].position.z);
            float sqr = (p - h).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = handles[i];
            }
        }
        return best;
    }

    static Transform FindAuthoritativeHouse()
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
}