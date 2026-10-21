using System.Collections.Generic;

/// <summary>
/// Which seeds and crops the player has access to. A seed is locked if some <see cref="UnlockData"/> lists it in
/// <c>unlocksSeeds</c> and that unlock hasn't been bought. Everything else is always available.
/// </summary>
public static class CropAvailability
{
    /// <summary>The unlock that gates this seed, or null if it is available from the start.</summary>
    public static UnlockData GateFor(SeedData seed, GameDatabase db)
    {
        if (seed == null || db == null || db.unlocks == null) return null;
        foreach (UnlockData u in db.unlocks.All)
            if (u != null && u.unlocksSeeds != null && u.unlocksSeeds.Contains(seed)) return u;
        return null;
    }

    public static bool IsSeedAvailable(SeedData seed, ICollection<string> unlockedIds, GameDatabase db)
    {
        UnlockData gate = GateFor(seed, db);
        return gate == null || (unlockedIds != null && unlockedIds.Contains(gate.Id));
    }

    /// <summary>The seed that grows this crop, or null.</summary>
    public static SeedData SeedFor(CropData crop, GameDatabase db)
    {
        if (crop == null || db == null || db.items == null) return null;
        foreach (ItemData item in db.items.All)
            if (item is SeedData s && s.grows == crop) return s;
        return null;
    }

    /// <summary>Seeds and the crops they grow share one lock.</summary>
    public static bool IsItemAvailable(ItemData item, ICollection<string> unlockedIds, GameDatabase db)
    {
        if (item is SeedData seed) return IsSeedAvailable(seed, unlockedIds, db);
        if (item is CropData crop) return IsSeedAvailable(SeedFor(crop, db), unlockedIds, db);
        return true;
    }

    /// <summary>The unlock that would make this item available, or null if it already is (or has no gate).</summary>
    public static UnlockData LockedBehind(ItemData item, ICollection<string> unlockedIds, GameDatabase db)
    {
        SeedData seed = item as SeedData ?? SeedFor(item as CropData, db);
        UnlockData gate = GateFor(seed, db);
        return gate != null && (unlockedIds == null || !unlockedIds.Contains(gate.Id)) ? gate : null;
    }
}
