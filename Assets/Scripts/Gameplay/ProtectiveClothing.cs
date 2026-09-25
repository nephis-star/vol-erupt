using UnityEngine;

/// <summary>
/// Protective clothing pickup. Interacting packs the garment into the inventory;
/// the player must then WEAR it from the inventory (tuple flow: pickup -&gt; inventory -&gt; wear).
/// Wearing reports completion to ObjectiveManager and visually marks the first-person body.
/// </summary>
public class ProtectiveClothing : Interactable
{
    public string itemName = "Protective Clothing";
    public Color wornColor = new Color(1f, 0.8f, 0.2f, 1f);

    public bool IsCollected { get; protected set; }
    public bool IsWorn { get; protected set; }

    protected override void OnStart()
    {
        base.OnStart();
        prompt = "pick up " + itemName;
    }

    public override bool CanInteract
    {
        get
        {
            if (!canInteract) return false;
            if (IsCollected || IsWorn) return false;
            ObjectiveManager om = ObjectiveManager.Instance;
            if (om == null) return false;
            // Only meaningful during evacuation prep.
            return om.equipmentEnabled;
        }
    }

    public override void Interact(GameObject interactor)
    {
        if (IsCollected || IsWorn) return;
        IsCollected = true;
        canInteract = false;

        InventoryManager inv = InventoryManager.Instance;
        if (inv != null) inv.AddItem(itemName);

        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast(itemName + " packed into inventory.");

        InvokeInteractionComplete();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Wears the garment (called from the inventory WEAR action). Applies the visual mark on the
    /// first-person body and completes the protective-clothing safety step.
    /// </summary>
    public bool Wear()
    {
        if (!IsCollected || IsWorn) return false;
        IsWorn = true;

        ObjectiveManager om = ObjectiveManager.Instance;
        if (om != null) om.CompleteProtectiveClothing();

        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast(itemName + " worn.");

        // Visual mark on the first-person body ("First Person Controller"/Capsule Mesh).
        GameObject player = GameObject.Find("First Person Controller");
        if (player != null)
        {
            Transform body = player.transform.Find("Capsule Mesh");
            if (body != null)
            {
                MeshRenderer mr = body.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    Material m = new Material(mr.sharedMaterial);
                    m.color = wornColor;
                    mr.material = m;
                }
            }
        }

        return true;
    }
}