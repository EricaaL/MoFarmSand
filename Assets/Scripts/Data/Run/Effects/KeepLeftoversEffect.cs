using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Granary: the new year starts with a few random crops and seeds left over from the last one.
/// Picks single units at random (bigger stacks come up more often) from the hotbar the last year ended with.
/// </summary>
[CreateAssetMenu(fileName = "Effect_KeepLeftovers", menuName = "MoFarmSand/Run Effects/Keep Leftovers")]
public class KeepLeftoversEffect : RunEffect
{
    public int crops = 3;
    public int seeds = 3;

    public override void Apply(RunState run, GameDatabase db) { }

    public override void ApplyAtYearStart(RunState run, GameDatabase db, RunState previous)
    {
        if (previous == null || previous.inventory == null) return;
        SeededRng rng = SeededRng.Derive(run.runSeed, 1, RngStreams.Granary);
        foreach (SlotSave kept in Pick(previous.inventory, db, crops, seeds, rng))
            InventoryOps.Add(run.inventory, kept.itemId, kept.count, InventoryOps.BaseSlots, kept.EffectiveQuality);
    }

    /// <summary>Up to <paramref name="cropCount"/> crop units and <paramref name="seedCount"/> seed units from <paramref name="source"/>, merged by item.</summary>
    public static List<SlotSave> Pick(List<SlotSave> source, GameDatabase db, int cropCount, int seedCount, SeededRng rng)
    {
        List<SlotSave> pool = new List<SlotSave>(source);
        List<SlotSave> result = new List<SlotSave>();
        TakeUnits(pool, result, db, cropCount, rng, wantCrops: true);
        TakeUnits(pool, result, db, seedCount, rng, wantCrops: false);
        return result;
    }

    private static void TakeUnits(List<SlotSave> pool, List<SlotSave> result, GameDatabase db, int units, SeededRng rng, bool wantCrops)
    {
        for (int n = 0; n < units; n++)
        {
            int total = 0;
            for (int i = 0; i < pool.Count; i++) if (Eligible(pool[i], db, wantCrops)) total += pool[i].count;
            if (total <= 0) return;

            int roll = rng.NextInt(0, total);
            for (int i = 0; i < pool.Count; i++)
            {
                if (!Eligible(pool[i], db, wantCrops)) continue;
                if (roll >= pool[i].count) { roll -= pool[i].count; continue; }

                SlotSave slot = pool[i];
                pool[i] = new SlotSave(slot.itemId, slot.count - 1, slot.EffectiveQuality);
                int at = result.FindIndex(r => r.itemId == slot.itemId);
                if (at < 0) result.Add(new SlotSave(slot.itemId, 1, slot.EffectiveQuality));
                else result[at] = new SlotSave(slot.itemId, result[at].count + 1, (result[at].EffectiveQuality * result[at].count + slot.EffectiveQuality) / (result[at].count + 1));
                break;
            }
        }
    }

    private static bool Eligible(SlotSave slot, GameDatabase db, bool wantCrops)
    {
        if (slot.count <= 0 || string.IsNullOrEmpty(slot.itemId) || db.items == null || !db.items.TryGet(slot.itemId, out ItemData item)) return false;
        return wantCrops ? item is CropData : item is SeedData;
    }

    public override string Describe() => $"keep {crops} crops and {seeds} seeds";
}
