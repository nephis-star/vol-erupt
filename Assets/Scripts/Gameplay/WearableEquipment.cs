using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hosts the equipped wearable visuals on the player's body (the existing npc_casual_set_00
/// avatar used as the visible first-person player body). The inventory state is authoritative:
/// an item visual exists only while <see cref="InventoryManager.IsEquipped"/> is true for it.
/// After the TV news grants all four wearables the bedroom source objects are disabled so they
/// no longer visually duplicate the equipped items.
/// </summary>
public class WearableEquipment : MonoBehaviour
{
    [Tooltip("The skinned avatar used as the visible player body. Disabled built-in head/hair/arms keep the first-person view clear.")]
    public GameObject avatarPrefab;
    [Tooltip("Player root (the First Person Controller). Defaults to the top-most transform of this GameObject.")]
    public Transform playerRoot;
    [Tooltip("Height of the FPC capsule; the avatar is scaled to match feet-to-head.")]
    public float bodyHeight = 1.8f;
    [Tooltip("Inventory ids, in the same order as sourceRoots.")]
    public string[] itemIds = { "Mask", "Shades", "T-shirt", "Pants" };
    [Tooltip("Scene roots of the bedroom GLB sources that must be hidden once all items are granted.")]
    public string[] sourceRoots = { "mask", "cool_shades_sunglasses", "hangers_and_pants", "t-shirts_homme" };

    GameObject avatarInstance;
    readonly Dictionary<string, GameObject> visualHolders = new Dictionary<string, GameObject>();
    bool bedroomSourcesHidden;

    void Start()
    {
        if (playerRoot == null)
        {
            Transform root = transform;
            while (root.parent != null) root = root.parent;
            playerRoot = root;
        }

        BuildBody();
        BuildVisuals();

        InventoryManager inv = InventoryManager.Instance;
        if (inv != null) inv.Changed += OnInventoryChanged;

        // Holders were created inactive; only the authoritative inventory state may activate them.
        RefreshAll();
        HideBedroomSourcesIfGranted();
    }

    void OnDestroy()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv != null) inv.Changed -= OnInventoryChanged;
    }

    void OnInventoryChanged()
    {
        RefreshAll();
        HideBedroomSourcesIfGranted();
    }

    void BuildBody()
    {
        if (avatarPrefab == null) return;
        avatarInstance = Instantiate(avatarPrefab, playerRoot);
        avatarInstance.name = "PlayerAvatarBody";

        float s = Mathf.Max(0.01f, bodyHeight / 2.04f);
        avatarInstance.transform.localScale = Vector3.one * s;

        // The avatar asset ships wearing its own tshirt/pants. Those are never the wearable
        // visuals — the equipped clones are. Disable every garment, plus head/hair/arms so the
        // first-person camera is never inside visible geometry. Only shoes (not a wearable) stay.
        foreach (SkinnedMeshRenderer smr in avatarInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string n = smr.name.ToLowerInvariant();
            if (n.Contains("head") || n.Contains("hair") || n.Contains("arm") ||
                n.Contains("tshirt") || n.Contains("pants"))
                smr.enabled = false;
        }

        // Feet on the ground: compute how far the lowest enabled renderer sits below the
        // avatar's origin IN LOCAL SPACE (bounds are in world space, so we convert).
        Bounds b = new Bounds();
        bool has = false;
        foreach (SkinnedMeshRenderer smr in avatarInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!smr.enabled || smr.sharedMesh == null) continue;
            if (!has) { b = smr.bounds; has = true; }
            else b.Encapsulate(smr.bounds);
        }
        if (has)
        {
            Vector3 localMin = avatarInstance.transform.InverseTransformPoint(b.min);
            avatarInstance.transform.localPosition = new Vector3(0f, -localMin.y, 0f);
        }

        // The avatar faces +Z like the First Person Controller.
        avatarInstance.transform.localRotation = Quaternion.identity;

        StripPhysicsAndInteraction(avatarInstance);
    }

    void BuildVisuals()
    {
        Transform head = FindBone(avatarInstance, "Head");
        Transform spine = FindBone(avatarInstance, "Spine1");
        Transform hips = FindBone(avatarInstance, "Hips");

        // Clone the bedroom meshes into lightweight containers parented to body bones.
        BuildWearableGo("Mask", FindSource("mask"),
            byChain: "Sketchfab_model.001", keepNames: null, keepMeshPrefix: "",
            bone: head, pos: new Vector3(0f, 0f, 0.12f), rot: new Vector3(90f, 0f, 0f), scale: 0.9f);
        BuildWearableGo("Shades", FindSource("cool_shades_sunglasses"),
            byChain: null, keepNames: null, keepMeshPrefix: "Plane_Material",
            bone: head, pos: new Vector3(0f, -0.06f, 0.1f), rot: new Vector3(90f, 90f, 0f), scale: 1.0f);
        BuildWearableGo("T-shirt", FindSource("t-shirts_homme"),
            byChain: "Obj3d66-516286-2-747_0", keepNames: new[] { "Object_7", "Object_8" }, keepMeshPrefix: "",
            bone: spine, pos: new Vector3(0f, -0.03f, 0.03f), rot: new Vector3(270f, 90f, 0f), scale: 0.7f, fixCrush: false);
        BuildWearableGo("Pants", FindSource("hangers_and_pants"),
            byChain: null, keepNames: new[] { "Object_4", "Object_5" }, keepMeshPrefix: "",
            bone: hips, pos: new Vector3(0f, -0.48f, 0.03f), rot: new Vector3(90f, 0f, 0f), scale: 0.95f, fixCrush: true);
    }

    void BuildWearableGo(string id, Transform source, string byChain, string[] keepNames,
        string keepMeshPrefix, Transform bone, Vector3 pos, Vector3 rot, float scale, bool fixCrush = false)
    {
        if (source == null || bone == null) return;

        // Holder starts inactive. Only InventoryManager.IsEquipped can ever activate it.
        GameObject holder = new GameObject("Wear_" + id);
        holder.transform.SetParent(bone, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localEulerAngles = Vector3.zero;
        holder.transform.localScale = Vector3.one;
        holder.SetActive(false);

        // Clone the full source tree exactly, then drop the leaf renderers we don't want.
        // Keeping the whole chain preserves every nested scale baked by the GLB import.
        GameObject ghost = Instantiate(source.gameObject);
        ghost.transform.SetParent(holder.transform, true);
        ghost.name = id + "_source_ghost";

        // The bedroom sources were placed/rotated arbitrarily (some also have a crushed or
        // rotated GLB root that is only a layout artifact). Strip the source root's world
        // rotation so the cloth sits axis-aligned in holder space; every authored child
        // rotation and all nested scales are preserved. The holder transform then provides
        // the only meaningful placement.
        ghost.transform.rotation = Quaternion.identity;

        // Some bedroom sources were flattened in the layout (e.g. the pants lie flat so their
        // root Y scale is ~0.01), which would render the wearable as a near-zero-thickness sheet.
        // Expand that crushed axis to match the largest axis so the runtime clone keeps a real
        // (up-right) volume. Only the crushed component is touched; authored proportions on the
        // preserved axes are left intact.
        if (fixCrush)
        {
            Vector3 ls = ghost.transform.localScale;
            float mx = Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
            float mn = Mathf.Min(ls.x, Mathf.Min(ls.y, ls.z));
            if (mn > 0f && mn < mx * 0.2f)
            {
                if (ls.y <= mn) ls.y = mx;
                else if (ls.x <= mn) ls.x = mx;
                else ls.z = mx;
                ghost.transform.localScale = ls;
            }
        }

        // The wearable copy is purely visual: no colliders, physics, or interaction scripts.
        StripPhysicsAndInteraction(ghost);

        // Drop unwanted leaf renderers. GameObject.Destroy is deferred (end of frame), so the
        // renderers are deactivated NOW (excluded from every subsequent bounds computation) and
        // unparented to a scratch node that is destroyed once the placement is finished. This keeps
        // the recenter bounds identical to what is actually rendered.
        GameObject scratch = new GameObject("Wear_" + id + "_discard");
        scratch.transform.SetParent(holder.transform, false);
        foreach (MeshRenderer mr in ghost.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            bool keep = string.IsNullOrEmpty(keepMeshPrefix) || mf.sharedMesh.name.StartsWith(keepMeshPrefix);
            bool inChain = string.IsNullOrEmpty(byChain) || IsInChain(mr.transform, byChain);
            bool named = keepNames == null || System.Array.Exists(keepNames, n => mr.name == n);

            if (!keep || !inChain || !named)
            {
                mr.gameObject.transform.SetParent(scratch.transform, true);
                mr.gameObject.SetActive(false);
            }
        }

        // Recenter the retained geometry on the holder origin in holder-local space,
        // preserving every authored (GLB) scale along the chain. The holder is inactive by
        // design, so enumerate with includeInactive=true and test the leaf's own activeSelf.
        Bounds b = new Bounds();
        bool has = false;
        foreach (MeshRenderer mr in ghost.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr == null || !mr.gameObject.activeSelf) continue;
            if (!has) { b = mr.bounds; has = true; }
            else b.Encapsulate(mr.bounds);
        }
        if (has)
        {
            Vector3 offset = holder.transform.InverseTransformPoint(b.center);
            ghost.transform.localPosition -= offset;
        }
        Destroy(scratch);
        foreach (Transform gt in scratch.transform.GetComponentsInChildren<Transform>(true))
            if (gt != scratch) Destroy(gt.gameObject);

        holder.transform.localPosition = pos;
        holder.transform.localEulerAngles = rot;
        holder.transform.localScale = Vector3.one * scale;

        visualHolders[id] = holder;
    }

    void RefreshAll()
    {
        InventoryManager inv = InventoryManager.Instance;
        foreach (string id in itemIds)
        {
            bool equipped = inv != null && inv.IsEquipped(id);
            if (visualHolders.TryGetValue(id, out GameObject holder) && holder != null)
                holder.SetActive(equipped);
        }
    }

    void HideBedroomSourcesIfGranted()
    {
        if (bedroomSourcesHidden) return;
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;

        bool allGranted = true;
        foreach (string id in itemIds)
            if (!inv.HasItem(id)) { allGranted = false; break; }
        if (!allGranted) return;

        foreach (string name in sourceRoots)
        {
            Transform src = FindSource(name);
            if (src != null) src.gameObject.SetActive(false);
        }
        bedroomSourcesHidden = true;
    }

    static bool IsInChain(Transform t, string token)
    {
        while (t != null)
        {
            if (t.name == token) return true;
            t = t.parent;
        }
        return false;
    }

    /// <summary>
    /// Removes every collider, rigidbody, and interaction MonoBehaviour from a runtime visual copy
    /// so the wearable system never interferes with PlayerInteraction raycasts (mask ~0, 4m).
    /// Only visual components (Transform, *Renderer, *Filter) are kept.
    /// </summary>
    static void StripPhysicsAndInteraction(GameObject root)
    {
        if (root == null) return;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            Component[] comps = t.GetComponents<Component>();
            foreach (Component c in comps)
            {
                if (c == null) continue;
                System.Type ty = c.GetType();
                if (ty == typeof(Transform)) continue;
                if (ty == typeof(MeshRenderer) || ty == typeof(SkinnedMeshRenderer)) continue;
                if (ty == typeof(MeshFilter)) continue;
                if (typeof(Collider).IsAssignableFrom(ty)) Object.Destroy(c);
                else if (typeof(Collider2D).IsAssignableFrom(ty)) Object.Destroy(c);
                else if (typeof(Rigidbody).IsAssignableFrom(ty)) Object.Destroy(c);
                else if (typeof(Rigidbody2D).IsAssignableFrom(ty)) Object.Destroy(c);
                else if (typeof(IInteractable).IsAssignableFrom(ty)) Object.Destroy(c);
                else if (typeof(MonoBehaviour).IsAssignableFrom(ty)) Object.Destroy(c);
            }
        }
    }

    Transform FindSource(string name)
    {
        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name == name && go.transform.parent == null) return go.transform;
        return null;
    }

    static Transform FindBone(GameObject root, string boneName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == boneName) return t;
        return null;
    }

    /// <summary>
    /// Edit-mode preview: builds the avatar + all four wearable holders at a fixed world spot
    /// (no inventory interplay) so the placement can be iterated with screenshots. The result is
    /// nested under <paramref name="previewRoot"/> which the caller destroys afterwards.
    /// </summary>
    public void DebugPreview(Transform previewRoot, Vector3 previewPos)
    {
        if (previewRoot == null) return;
        Transform prev = playerRoot;
        playerRoot = previewRoot;
        previewRoot.position = previewPos;
        previewRoot.rotation = Quaternion.identity;
        bool[] srcWasActive = new bool[sourceRoots.Length];
        for (int i = 0; i < sourceRoots.Length; i++)
        {
            // FindSource only sees active scene roots; the bedroom pieces may have been disabled
            // earlier, so briefly wake them so BuildVisuals can clone them, then restore.
            Transform src = FindSourceAny(sourceRoots[i]);
            if (src != null)
            {
                srcWasActive[i] = src.gameObject.activeSelf;
                if (!src.gameObject.activeSelf) src.gameObject.SetActive(true);
            }
        }
        BuildBody();
        BuildVisuals();
        for (int i = 0; i < sourceRoots.Length; i++)
        {
            Transform src = FindSourceAny(sourceRoots[i]);
            if (src != null && src.gameObject.activeSelf != srcWasActive[i])
                src.gameObject.SetActive(srcWasActive[i]);
        }
        playerRoot = prev;
        foreach (GameObject h in visualHolders.Values)
            if (h != null) h.SetActive(true);
    }

    static Transform FindSourceAny(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go != null && go.scene.IsValid() && go.name == name && go.transform.parent == null)
                return go.transform;
        return null;
    }
}