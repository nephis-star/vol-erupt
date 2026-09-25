using UnityEngine;

/// <summary>
/// Emergency bag pickup. Adds the bag to the inventory but does NOT register an equipment task,
/// so the four-item objective checklist (bottle / flashlight / mask / eye protection) is unchanged.
/// </summary>
public class EmergencyBagPickup : Interactable
{
    public string itemName = "Emergency Bag";

    bool collected;

    protected override void OnStart()
    {
        base.OnStart();
        prompt = "pick up " + itemName;
    }

    public override bool CanInteract
    {
        get
        {
            if (!canInteract || collected) return false;
            ObjectiveManager om = ObjectiveManager.Instance;
            if (om != null && !om.equipmentEnabled) return false;
            return true;
        }
    }

    public override void Interact(GameObject interactor)
    {
        if (collected) return;
        collected = true;
        canInteract = false;

        InventoryManager inv = InventoryManager.Instance;
        if (inv != null) inv.AddItem(itemName);

        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast(itemName + " packed into inventory.");
        if (SubtitleUI.Instance != null)
            SubtitleUI.Instance.Show("Emergency bag is ready to carry out.", 4f);

        InvokeInteractionComplete();
        gameObject.SetActive(false);
    }
}