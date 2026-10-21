using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extra rewards rolled when a crop is harvested. Pure and seeded: the same day with the same harvests in the
/// same order always rolls the same luck, so reloading a day can't be used to fish for drops.
/// </summary>
public sealed class HarvestBonus
{
    private readonly List<SeedData> seeds = new List<SeedData>();
    private readonly SeededRng rng;

    /// <summary>Per-harvest chance (0-1) of a bonus seed, summed over the run's modifiers.</summary>
    public float SeedChance { get; }

    public HarvestBonus(GameDatabase db, RunState run, SeededRng rng)
    {
        this.rng = rng;
        SeedChance = ChanceFor(db, run);
        if (db.items != null)
            foreach (ItemData item in db.items.All)
                if (item is SeedData seed && CropAvailability.IsSeedAvailable(seed, run.unlockedIds, db)) seeds.Add(seed);
    }

    public static float ChanceFor(GameDatabase db, RunState run)
    {
        if (db.modifiers == null) return 0f;
        float chance = 0f;
        foreach (string id in run.modifierIds)
            if (db.modifiers.TryGet(id, out RunModifierData m)) chance += Mathf.Max(0f, m.harvestSeedChance);
        return Mathf.Clamp01(chance);
    }

    /// <summary>Rolls once. Returns a uniformly random seed of any kind, or null for no bonus.</summary>
    public SeedData Roll()
    {
        // Always draw the same number of values per roll so later rolls stay stable.
        float hit = rng.NextFloat();
        int pick = rng.NextInt(0, Mathf.Max(1, seeds.Count));
        if (seeds.Count == 0 || hit >= SeedChance) return null;
        return seeds[pick];
    }
}
