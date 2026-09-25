using UnityEngine;

/// <summary>
/// Definition of a collectible inventory item. Authorable as a ScriptableObject asset in the
/// editor (Create &gt; Game &gt; Inventory Item); the game also builds the standard survival-item
/// set at runtime through InventoryCatalog so the flying start / tests always have a consistent set.
/// </summary>
[CreateAssetMenu(fileName = "InventoryItem", menuName = "Game/Inventory Item")]
public class InventoryItemData : ScriptableObject
{
    [Tooltip("Unique id used to look items up in InventoryManager (must match pickup itemName).")]
    public string id;

    [Tooltip("Display name shown in the inventory UI.")]
    public string itemName;

    [TextArea]
    [Tooltip("Short description shown in the inventory UI.")]
    public string description;

    [Tooltip("Colour swatch used as the item icon in the inventory UI.")]
    public Color iconColor = Color.white;

    [Tooltip("Consumable items (e.g. bottled water) are removed from the usable pool when used.")]
    public bool consumable;

    [Tooltip("Wearable items (mask, goggles, protective clothing) are worn instead of held.")]
    public bool wearable;

    [Tooltip("Equippable items (flashlight) are toggled equip/unequip.")]
    public bool equippable;

    [Tooltip("Label of the inventory action button, e.g. DRINK / WEAR / EQUIP / PACK.")]
    public string useLabel = "USE";

    /// <summary>Convenience factory for runtime-defined catalog entries.</summary>
    public static InventoryItemData Create(string id, string itemName, string description,
        Color iconColor, string useLabel, bool consumable, bool wearable, bool equippable)
    {
        InventoryItemData data = CreateInstance<InventoryItemData>();
        data.id = id;
        data.itemName = itemName;
        data.description = description;
        data.iconColor = iconColor;
        data.useLabel = useLabel;
        data.consumable = consumable;
        data.wearable = wearable;
        data.equippable = equippable;
        return data;
    }
}