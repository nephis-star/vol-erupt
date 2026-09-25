using UnityEngine;

public class PickupItem : Interactable
{
    [Tooltip("Name shown in the objective checklist, e.g. 'Water'")]
    public string itemName = "Item";

    [System.NonSerialized] public bool collected;

    void Start()
    {
        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.RegisterEquipment(itemName, itemName);
    }

    public override bool CanInteract
    {
        get
        {
            if (!canInteract || collected) return false;
            ObjectiveManager om = ObjectiveManager.Instance;
            // Bottled water is collectible from the start so the player can drink it from the
            // inventory during the thirsty step; the other emergency items unlock in evacuation prep.
            if (om != null && !om.equipmentEnabled && itemName != "Bottled Water") return false;
            return true;
        }
    }

    public override void Interact(GameObject interactor)
    {
        if (collected) return;
        collected = true;
        canInteract = false;

        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.CollectItem(itemName);

        InventoryManager inv = InventoryManager.Instance;
        if (inv != null) inv.AddItem(itemName);

        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast(itemName + " collected.");

        ShowEducationalHint();

        InvokeInteractionComplete();
        gameObject.SetActive(false);
    }

    protected virtual void ShowEducationalHint()
    {
        if (SubtitleUI.Instance == null) return;
        string msg = null;
        switch (itemName)
        {
            case "Flashlight": msg = "Keep a flashlight ready; power may go out during ashfall."; break;
            case "Face Mask": msg = "Wear a mask to keep ash out of your lungs."; break;
            case "Eye Protection": msg = "Goggles protect your eyes from airborne ash."; break;
            case "Bottled Water": msg = "Use bottled water; ash can contaminate tap water supplies."; break;
        }
        if (msg != null) SubtitleUI.Instance.Show(msg, 4.5f);
    }
}