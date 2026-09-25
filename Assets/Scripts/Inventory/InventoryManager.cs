using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns every collected item. Session-only: cleared whenever a (new) game session starts.
/// The world object is just a pickup; ownership is registered here. Use/equip/wear actions are
/// routed through here so the UI and tests always talk to one API.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    readonly List<InventoryEntry> items = new List<InventoryEntry>();

    public event Action Changed;

    void Awake()
    {
        Instance = this;
        Clear();
    }

    public void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public List<InventoryEntry> AllItems
    {
        get { return items; }
    }

    /// <summary>Session-persistence only: clear the whole inventory on a fresh session / restart.</summary>
    public void Clear()
    {
        items.Clear();
        BottledWater.ConsumeBottle();
        FireChanged();
    }

    public InventoryEntry AddItem(string id, int quantity = 1)
    {
        if (string.IsNullOrEmpty(id) || quantity <= 0) return null;

        InventoryEntry entry = Find(id);
        if (entry == null)
        {
            InventoryItemData data = InventoryCatalog.Get(id);
            if (data == null) return null;
            entry = new InventoryEntry(data, quantity);
            items.Add(entry);
        }
        else
        {
            entry.quantity += quantity;
        }

        FireChanged();
        return entry;
    }

    public bool HasItem(string id)
    {
        InventoryEntry e = Find(id);
        return e != null && e.quantity > 0;
    }

    /// <summary>True once the item has ever been collected (even if consumed), used by the final evacuation check.</summary>
    public bool HasCollected(string id)
    {
        return Find(id) != null;
    }

    public int Count(string id)
    {
        InventoryEntry e = Find(id);
        return e == null ? 0 : e.quantity;
    }

    public InventoryItemState StateOf(string id)
    {
        InventoryEntry e = Find(id);
        return e == null ? InventoryItemState.Collected : e.state;
    }

    public bool IsEquipped(string id)
    {
        InventoryEntry e = Find(id);
        return e != null && e.state == InventoryItemState.Equipped;
    }

    /// <summary>
    /// Routes the primary inventory action for an item. Which action (DRINK / WEAR / EQUIP / PACK)
    /// is decided by the item definition, never by the caller.
    /// </summary>
    public bool UseNamed(string id)
    {
        InventoryEntry e = Find(id);
        if (e == null || e.quantity <= 0) return false;
        if (e.state == InventoryItemState.Collected || e.state == InventoryItemState.Used)
            e.state = InventoryItemState.Available;

        InventoryItemData d = e.data;
        if (d.consumable) return DrinkNamed(id);
        if (d.wearable) return WearNamed(id);
        return EquipToggleNamed(id);
    }

    /// <summary>
    /// Drink the bottled water. During the thirst step this registers a SAFE water choice with
    /// ObjectiveManager, exactly like choosing the sealed world water, so the existing story
    /// (thirst prompt -&gt; water choice -&gt; second news) keeps working through the inventory.
    /// </summary>
    public bool DrinkNamed(string id)
    {
        InventoryEntry e = Find(id);
        if (e == null || e.quantity <= 0) return false;

        if (e.data != null && !e.data.consumable) return false;

        e.state = InventoryItemState.Consumed;
        e.quantity = 0;
        BottledWater.ConsumeBottle();

        ObjectiveManager om = ObjectiveManager.Instance;
        if (om != null && om.waterStepActive && !om.waterChoiceMade)
            om.RegisterWaterChoice(true);

        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast("You drank the bottled water.");
        if (SubtitleUI.Instance != null)
            SubtitleUI.Instance.Show("Sealed bottled water is safe to drink.", 3f);

        FireChanged();
        return true;
    }

    /// <summary>
    /// Wear a wearable item. Protective clothing is applied on the player body and reports the
    /// objective; mask / goggles just track the equipped state on the inventory entry.
    /// </summary>
    public bool WearNamed(string id)
    {
        InventoryEntry e = Find(id);
        if (e == null || e.quantity <= 0) return false;

        if (id == "Protective Clothing")
        {
            if (e.state == InventoryItemState.Equipped) return true;
            ProtectiveClothing pc = FindAnyObjectByType<ProtectiveClothing>(FindObjectsInactive.Include);
            if (pc == null || !pc.IsCollected) return false;
            e.state = InventoryItemState.Equipped;
            if (!pc.Wear())
            {
                e.state = InventoryItemState.Available;
                return false;
            }
            FireChanged();
            return true;
        }

        e.state = e.state == InventoryItemState.Equipped
            ? InventoryItemState.Available
            : InventoryItemState.Equipped;
        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast(id + " equipped.");
        FireChanged();
        return true;
    }

    /// <summary>Toggles equipped/unequipped for simple equippable items (flashlight, emergency bag).</summary>
    public bool EquipToggleNamed(string id)
    {
        InventoryEntry e = Find(id);
        if (e == null || e.quantity <= 0) return false;

        e.state = e.state == InventoryItemState.Equipped
            ? InventoryItemState.Available
            : InventoryItemState.Equipped;
        if (InteractionUI.Instance != null)
            InteractionUI.Instance.ShowToast(id + " equipped.");
        FireChanged();
        return true;
    }

    InventoryEntry Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (InventoryEntry e in items)
            if (e.data != null && e.data.id == id) return e;
        return null;
    }

    void FireChanged()
    {
        if (Changed != null) Changed();
    }
}