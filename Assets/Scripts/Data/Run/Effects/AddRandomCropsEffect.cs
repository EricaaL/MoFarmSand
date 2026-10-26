using System.Collections.Generic;
using UnityEngine;

/// <summary>Gives random harvested crops of any kind available this year (Cozy Campfire). Seeded by the run and the day, so reloading can't re-roll them.</summary>
[CreateAssetMenu(fileName = "Effect_RandomCrops", menuName = "MoFarmSand/Run Effects/Add Random Crops")]
public class AddRandomCropsEffect : RunEffect
{
    public int count = 2;

    public override void Apply(RunState run, GameDatabase db)
    {
        if (db.items == null) return;
        List<CropData> crops = new List<CropData>();
        foreach (ItemData item in db.items.All)
            if (item is CropData crop && CropAvailability.IsItemAvailable(crop, run.unlockedIds, db)) crops.Add(crop);
        if (crops.Count == 0) return;

        SeededRng rng = SeededRng.Derive(run.runSeed, 1000 + run.DayIndex, RngStreams.Loadout);
        int capacity = InventoryOps.BaseSlots + DayPlanner.BonusSlots(run, db);
        for (int i = 0; i < count; i++) InventoryOps.Add(run.inventory, crops[rng.NextInt(0, crops.Count)].Id, 1, capacity);
    }

    public override string Describe() => $"+{count} random crops";
}
