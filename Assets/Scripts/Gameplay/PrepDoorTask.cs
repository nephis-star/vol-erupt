using UnityEngine;

/// <summary>
/// Preparation step that closes the main exterior house door during HOME PREP and reopens it for
/// evacuation by driving the existing DoorInteraction panel. Before prep the main door stays a normal,
/// interactive door; the interior room doors are left as normal interactive doors.
/// </summary>
public class PrepDoorTask : PreparationTask
{
    static readonly string MainDoorName = "Plane_Material.001_0";

    DoorInteraction mainDoor;
    Collider prepSurface;
    bool surfaceEnabled;
    bool released;

    protected override void OnStart()
    {
        started = true;

        DoorInteraction[] doors = FindObjectsByType<DoorInteraction>(FindObjectsSortMode.None);
        foreach (DoorInteraction d in doors)
        {
            if (d == null) continue;
            if (d.gameObject.name == MainDoorName)
            {
                mainDoor = d;
                break;
            }
        }

        if (mainDoor == null)
        {
            Debug.LogWarning("PrepDoorTask: main door panel '" + MainDoorName + "' not found for " + name + ".", this);
            return;
        }

        prepSurface = CreateSurface();
        if (prepSurface != null) prepSurface.enabled = false;

        // Before prep the main door is a normal, interactive door.
        mainDoor.canInteract = true;

        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.RegisterPreparation(taskId, displayName, "Close " + displayName);
    }

    void Update()
    {
        if (mainDoor == null || released) return;

        ObjectiveManager om = ObjectiveManager.Instance;
        bool prep = om != null && om.preparationStarted && !om.homePrepared;

        if (prep && !surfaceEnabled)
        {
            mainDoor.canInteract = false;
            if (prepSurface != null) prepSurface.enabled = true;
            surfaceEnabled = true;
        }
        else if (!prep && surfaceEnabled)
        {
            mainDoor.canInteract = true;
            if (prepSurface != null) prepSurface.enabled = false;
            surfaceEnabled = false;
        }
    }

    public override string InteractionPrompt
    {
        get { return IsClosed ? "" : "close the main door"; }
    }

    public override bool CanInteract
    {
        get
        {
            if (released || !surfaceEnabled) return false;
            return canInteract && started && !IsClosed;
        }
    }

    public override void Interact(GameObject interactor)
    {
        if (IsClosed || !CanInteract) return;
        CloseMainDoor();
        IsClosed = true;
        if (prepSurface != null) prepSurface.enabled = false;
        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.CompleteTask(taskId);
        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast("Main door secured.");
        InvokeInteractionComplete();
    }

    public override void Reopen()
    {
        released = true;
        if (mainDoor != null) mainDoor.canInteract = true;
        if (prepSurface != null) prepSurface.enabled = false;
        surfaceEnabled = false;
        if (mainDoor != null && !mainDoor.IsOpen)
            mainDoor.Interact(null);
        IsClosed = false;
        IsOpened = true;
    }

    // The main door model lives under door.fbx which is scaled to 0.01 inside a rotated, non-uniform
    // parent chain ('doors'), so the panel renders axis-aligned in world while its local transform is
    // badly distorted. Give this task a dedicated interaction surface just inside the closed door,
    // built as a WORLD-ALIGNED box so the preparation raycast reaches this step reliably.
    Collider CreateSurface()
    {
        MeshRenderer mr = mainDoor.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            Debug.LogWarning("PrepDoorTask: main door has no mesh bounds for " + MainDoorName + ".", this);
            return null;
        }

        Bounds b = mr.bounds;

        // The player seals/closes this door while standing inside the house, so 'inward' (toward the
        // room) is anchored from the camera. Using house.position is unreliable here: its transform
        // origin sits almost co-located with the door panel in X, so the Z delta dominates and pushes
        // the surface to the wrong side of the doorway.
        Vector3 camP = Camera.main != null ? Camera.main.transform.position : b.center;
        Vector3 inwardPt = new Vector3(camP.x - b.center.x, 0f, camP.z - b.center.z);
        if (inwardPt.sqrMagnitude < 0.0001f) inwardPt = -Vector3.right;
        Vector3 inward = inwardPt.normalized;

        Vector3 center = b.center + inward * 0.25f;
        Vector3 sizeWorld = new Vector3(0.4f, Mathf.Max(b.extents.y * 1.6f, 1.8f), Mathf.Max(b.extents.z * 1.4f, 1.2f));

        GameObject go = new GameObject("MainDoorPrepSurface");
        go.transform.SetParent(transform, false);

        // Align the surface to world space regardless of the parent chain's rotations/scales.
        go.transform.position = center;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        // Child lossyScale now measures world length of each LOCAL unit axis. Divide the desired
        // world size by that so collider world bounds land exactly on sizeWorld.
        Vector3 l = go.transform.lossyScale;
        l = new Vector3(
            Mathf.Abs(l.x) > 0.0001f ? l.x : 1f,
            Mathf.Abs(l.y) > 0.0001f ? l.y : 1f,
            Mathf.Abs(l.z) > 0.0001f ? l.z : 1f);

        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.center = Vector3.zero;
        bc.size = new Vector3(sizeWorld.x / l.x, sizeWorld.y / l.y, sizeWorld.z / l.z);
        return bc;
    }

    void CloseMainDoor()
    {
        if (mainDoor != null && mainDoor.IsOpen) mainDoor.Interact(null);
    }
}