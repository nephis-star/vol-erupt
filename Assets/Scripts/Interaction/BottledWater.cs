using UnityEngine;

public class BottledWater : PickupItem
{
    public static bool HasBottle { get; private set; }

    /// <summary>Marks the world bottle as drunk/consumed.</summary>
    public static void ConsumeBottle()
    {
        HasBottle = false;
    }

    void Awake()
    {
        prompt = "pick up bottled water";
        HasBottle = false;
    }

    public override void Interact(GameObject interactor)
    {
        if (collected) return;
        collected = true;
        canInteract = false;
        HasBottle = true;

        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.CollectItem(itemName);

        InventoryManager inv = InventoryManager.Instance;
        if (inv != null) inv.AddItem(itemName);

        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast("Bottled water collected.");

        if (SubtitleUI.Instance != null)
            SubtitleUI.Instance.Show(
                "Use bottled or properly protected water.\nAsh contamination can affect water supplies.",
                5f);

        ShowEducationalHint();

        InvokeInteractionComplete();
        gameObject.SetActive(false);
    }
}