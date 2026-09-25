/// <summary>
/// Life-cycle of a single inventory entry:
/// Collected (picked up, present in the inventory) -&gt; Available (usable) -&gt;
/// Equipped (worn / held, reusable) or Used / Consumed (single-use items).
/// </summary>
public enum InventoryItemState
{
    Collected,
    Available,
    Equipped,
    Used,
    Consumed
}

/// <summary>
/// Runtime instance of an owned item in the inventory. The world object is just a pickup;
/// ownership + state lives entirely here.
/// </summary>
public class InventoryEntry
{
    public readonly InventoryItemData data;
    public int quantity;
    public InventoryItemState state;

    public InventoryEntry(InventoryItemData data, int quantity)
    {
        this.data = data;
        this.quantity = quantity;
        state = InventoryItemState.Collected;
    }
}