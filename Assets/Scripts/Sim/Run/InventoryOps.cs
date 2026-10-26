using System;
using System.Collections.Generic;

/// <summary>Inventory edits on the saved snapshot (the live PlayerInventory has its own equivalents).</summary>
public static class InventoryOps
{
    /// <summary>Slots saved in a snapshot: the base hotbar plus the one the Small Pouch adds.</summary>
    public static int Slots => PlayerInventory.SlotCount;
    /// <summary>Slots every run can use; charms add to it.</summary>
    public static int BaseSlots => PlayerInventory.BaseSlots;

    public static void EnsureSize(List<SlotSave> slots)
    {
        while (slots.Count < Slots) slots.Add(new SlotSave(string.Empty, 0));
        if (slots.Count > Slots) slots.RemoveRange(Slots, slots.Count - Slots);
    }

    /// <summary>Stacks onto an existing slot or fills the first empty one among the first <paramref name="capacity"/>. Returns false if full.</summary>
    public static bool Add(List<SlotSave> slots, string itemId, int count, int capacity = PlayerInventory.BaseSlots, float quality = 1f)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
        EnsureSize(slots);
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].count > 0 && slots[i].itemId == itemId)
            {
                // Rewards are normal strength: blend them into the existing stack.
                float blended = (slots[i].EffectiveQuality * slots[i].count + quality * count) / (slots[i].count + count);
                slots[i] = new SlotSave(itemId, slots[i].count + count, blended);
                return true;
            }
        for (int i = 0; i < slots.Count && i < capacity; i++)
            if (slots[i].count <= 0) { slots[i] = new SlotSave(itemId, count, quality); return true; }
        return false;
    }

    /// <summary>
    /// Takes <paramref name="count"/> single units from random eligible slots (bigger stacks lose more often).
    /// Returns how many were actually taken, which is fewer when the eligible items run out.
    /// </summary>
    public static int RemoveRandom(List<SlotSave> slots, int count, Func<string, bool> eligible, SeededRng rng)
    {
        int taken = 0;
        for (int n = 0; n < count; n++)
        {
            int total = 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].count > 0 && eligible(slots[i].itemId)) total += slots[i].count;
            if (total <= 0) break;

            int roll = rng.NextInt(0, total);
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].count <= 0 || !eligible(slots[i].itemId)) continue;
                if (roll < slots[i].count)
                {
                    int left = slots[i].count - 1;
                    slots[i] = left > 0 ? new SlotSave(slots[i].itemId, left, slots[i].EffectiveQuality) : new SlotSave(string.Empty, 0);
                    taken++;
                    break;
                }
                roll -= slots[i].count;
            }
        }
        return taken;
    }

    public static int CountOf(List<SlotSave> slots, string itemId)
    {
        int total = 0;
        for (int i = 0; i < slots.Count; i++) if (slots[i].itemId == itemId) total += slots[i].count;
        return total;
    }
}
