using UnityEngine;

/// <summary>Gives random seeds of any kind available this year (Seed Pouch). Seeded by the run, so it is the same every time the year loads.</summary>
[CreateAssetMenu(fileName = "Effect_RandomSeeds", menuName = "MoFarmSand/Run Effects/Add Random Seeds")]
public class AddRandomSeedsEffect : RunEffect
{
    public int count = 3;

    public override void Apply(RunState run, GameDatabase db)
    {
        foreach (SeedData seed in Charms.RollSeeds(db, run, count, SeededRng.Derive(run.runSeed, 1, RngStreams.Loadout)))
            InventoryOps.Add(run.inventory, seed.Id, 1);
    }

    public override string Describe() => $"+{count} random seeds";
}
