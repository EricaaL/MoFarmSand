using System.Collections.Generic;

/// <summary>Inventory edits on the saved snapshot (the live PlayerInventory has its own equivalents).</summary>
public static class InventoryOps
{
    public static int Slots => PlayerInventory.SlotCount;

    public static void EnsureSize(List<SlotSave> slots)
    {
        while (slots.Count < Slots) slots.Add(new SlotSave(string.Empty, 0));
        if (slots.Count > Slots) slots.RemoveRange(Slots, slots.Count - Slots);
    }

    /// <summary>Stacks onto an existing slot or fills the first empty one. Returns false if full.</summary>
    public static bool Add(List<SlotSave> slots, string itemId, int count)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
        EnsureSize(slots);
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].count > 0 && slots[i].itemId == itemId)
            {
                // Rewards are normal strength: blend them into the existing stack.
                float blended = (slots[i].EffectiveQuality * slots[i].count + count) / (slots[i].count + count);
                slots[i] = new SlotSave(itemId, slots[i].count + count, blended);
                return true;
            }
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].count <= 0) { slots[i] = new SlotSave(itemId, count); return true; }
        return false;
    }

    public static int CountOf(List<SlotSave> slots, string itemId)
    {
        int total = 0;
        for (int i = 0; i < slots.Count; i++) if (slots[i].itemId == itemId) total += slots[i].count;
        return total;
    }
}
