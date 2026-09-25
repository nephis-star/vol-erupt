using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime definition table for the standard survival items. Kept in code so the inventory is
/// always deterministic (tests and the flying six-minute story both rely on it); each entry is a
/// ScriptableObject so the exact same definitions can later be authored as project assets instead.
/// </summary>
public static class InventoryCatalog
{
    static readonly Dictionary<string, InventoryItemData> cache = new Dictionary<string, InventoryItemData>();

    public static InventoryItemData Get(string id)
    {
        InventoryItemData existing;
        if (cache.TryGetValue(id, out existing)) return existing;

        InventoryItemData data = null;
        switch (id)
        {
            case "Bottled Water":
                data = InventoryItemData.Create(id, "Bottled Water",
                    "Sealed bottled water. Safe to drink even during ashfall.",
                    new Color(0.30f, 0.55f, 1f), "DRINK", true, false, false);
                break;
            case "Flashlight":
                data = InventoryItemData.Create(id, "Flashlight",
                    "Keep it ready; power can go out during ashfall.",
                    new Color(1f, 0.85f, 0.3f), "EQUIP", false, false, true);
                break;
            case "Face Mask":
                data = InventoryItemData.Create(id, "Face Mask",
                    "Wear a mask to keep ash out of your lungs.",
                    new Color(0.8f, 0.8f, 0.85f), "WEAR", false, true, false);
                break;
            case "Mask":
                data = InventoryItemData.Create(id, "Mask",
                    "Protective face mask found in the bedroom. Wear it to keep ash out of your lungs.",
                    new Color(0.75f, 0.78f, 0.82f), "WEAR", false, true, false);
                break;
            case "Shades":
                data = InventoryItemData.Create(id, "Shades",
                    "Sunglasses from the bedroom. Wear them to shield your eyes from airborne ash.",
                    new Color(0.18f, 0.18f, 0.22f), "WEAR", false, true, false);
                break;
            case "T-shirt":
                data = InventoryItemData.Create(id, "T-shirt",
                    "A long-sleeved shirt from the bedroom. Wear it to protect your skin from ash.",
                    new Color(0.45f, 0.62f, 0.8f), "WEAR", false, true, false);
                break;
            case "Pants":
                data = InventoryItemData.Create(id, "Pants",
                    "Protective pants from the bedroom. Wear them before heading outside.",
                    new Color(0.48f, 0.38f, 0.3f), "WEAR", false, true, false);
                break;
            case "Eye Protection":
                data = InventoryItemData.Create(id, "Eye Protection",
                    "Goggles protect your eyes from airborne ash.",
                    new Color(0.3f, 0.7f, 0.9f), "WEAR", false, true, false);
                break;
            case "Protective Clothing":
                data = InventoryItemData.Create(id, "Protective Clothing",
                    "Long pants and a long-sleeved shirt. Wear them before evacuating.",
                    new Color(1f, 0.7f, 0.2f), "WEAR", false, true, false);
                break;
            case "Emergency Bag":
                data = InventoryItemData.Create(id, "Emergency Bag",
                    "Your emergency supplies bag, ready to carry out.",
                    new Color(0.5f, 0.35f, 0.2f), "PACK", false, false, true);
                break;
            default:
                data = InventoryItemData.Create(id, id, "", new Color(0.6f, 0.6f, 0.6f),
                    "USE", false, false, false);
                break;
        }
        data.hideFlags = HideFlags.HideAndDontSave;
        cache[id] = data;
        return data;
    }
}