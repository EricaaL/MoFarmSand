using System;
using System.Collections.Generic;

/// <summary>What the passive trinkets (RunModifierData) add up to. Pure: reads the run's owned ids and the database.</summary>
public static class Charms
{
    private static int Sum(RunState run, GameDatabase db, Func<RunModifierData, int> pick)
    {
        if (db.modifiers == null) return 0;
        int total = 0;
        foreach (string id in run.modifierIds)
            if (db.modifiers.TryGet(id, out RunModifierData m)) total += Math.Max(0, pick(m));
        return total;
    }

    private static float Sum(RunState run, GameDatabase db, Func<RunModifierData, float> pick)
    {
        if (db.modifiers == null) return 0f;
        float total = 0f;
        foreach (string id in run.modifierIds)
            if (db.modifiers.TryGet(id, out RunModifierData m)) total += Math.Max(0f, pick(m));
        return total;
    }

    /// <summary>Extra coins that may be banked per season (Doggy Bank).</summary>
    public static int DepositLimitBonus(RunState run, GameDatabase db) => Sum(run, db, m => m.depositLimitBonus);

    /// <summary>Extra coins on every crop sold (Shiny Rock).</summary>
    public static int SellBonus(RunState run, GameDatabase db) => Sum(run, db, m => m.sellBonusCoins);

    /// <summary>Strength factor for crops planted now: 1.2 means 20% stronger (Puppy Charm for support crops, Puppy Ring for attack crops).</summary>
    public static float CropStrength(RunState run, GameDatabase db, CropType type) =>
        1f + (type == CropType.Support ? Sum(run, db, m => m.supportCropBoost) : Sum(run, db, m => m.attackCropBoost));

    /// <summary>The seeds a Blind Box gives: <paramref name="count"/> picks, any kind that is available this year.</summary>
    public static List<SeedData> RollSeeds(GameDatabase db, RunState run, int count, SeededRng rng)
    {
        List<SeedData> seeds = new List<SeedData>();
        if (db.items != null)
            foreach (ItemData item in db.items.All)
                if (item is SeedData seed && CropAvailability.IsSeedAvailable(seed, run.unlockedIds, db)) seeds.Add(seed);
        List<SeedData> result = new List<SeedData>();
        if (seeds.Count == 0) return result;
        for (int i = 0; i < count; i++) result.Add(seeds[rng.NextInt(0, seeds.Count)]);
        return result;
    }
}
