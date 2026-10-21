using System.Collections.Generic;

/// <summary>Beta testing loadout: the season's own crops, one crop of each kind so any boss can be fed and shielded, then seeds in the slots left over.</summary>
public static class BetaKit
{
    public const int CropCount = 20;
    public const int SeedCount = 5;

    /// <summary>Replaces the run's inventory with the kit for <paramref name="season"/>. Crops come first so they always fit the 8 slots.</summary>
    public static void Fill(RunState run, GameDatabase db, SeasonData season)
    {
        run.inventory = new List<SlotSave>();
        InventoryOps.EnsureSize(run.inventory);
        run.activeSlot = 0;
        if (db.prices == null) return;

        HashSet<CropType> covered = new HashSet<CropType>();
        foreach (PriceTable.PriceRow row in db.prices.rows)
        {
            if (row.crop == null || !row.StockedIn(season)) continue;
            InventoryOps.Add(run.inventory, row.crop.Id, CropCount);
            covered.Add(row.crop.type);
        }

        // Top up with any crop kind the season lacks.
        foreach (PriceTable.PriceRow row in db.prices.rows)
        {
            if (row.crop == null || covered.Contains(row.crop.type)) continue;
            InventoryOps.Add(run.inventory, row.crop.Id, CropCount);
            covered.Add(row.crop.type);
        }

        // Whatever slots remain hold seeds for planting.
        foreach (PriceTable.PriceRow row in db.prices.rows)
        {
            if (row.seed == null || !row.StockedIn(season)) continue;
            InventoryOps.Add(run.inventory, row.seed.Id, SeedCount);
        }
    }
}
