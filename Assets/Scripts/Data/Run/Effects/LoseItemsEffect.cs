using UnityEngine;

/// <summary>Takes random seeds and/or crops out of the inventory (a thief, pests, a toll). Never touches farmland plot items.</summary>
[CreateAssetMenu(fileName = "Effect_LoseItems", menuName = "MoFarmSand/Run Effects/Lose Items")]
public class LoseItemsEffect : RunEffect
{
    public enum Kind { SeedsOrCrops, SeedsOnly, CropsOnly }

    public int count = 2;
    public Kind kind = Kind.SeedsOrCrops;

    public override void Apply(RunState run, GameDatabase db)
    {
        // Seeded by the day's node, so reloading the event can't reroll what is lost.
        SeededRng rng = SeededRng.Derive(run.runSeed, run.pendingNodeId, RngStreams.EventLoss);
        InventoryOps.RemoveRandom(run.inventory, count, id => IsTarget(db, id), rng);
    }

    private bool IsTarget(GameDatabase db, string itemId)
    {
        if (db.items == null || !db.items.TryGet(itemId, out ItemData item)) return false;
        bool seed = item is SeedData, crop = item is CropData;
        switch (kind)
        {
            case Kind.SeedsOnly: return seed;
            case Kind.CropsOnly: return crop;
            default: return seed || crop;
        }
    }

    public override string Describe()
    {
        string what = kind == Kind.SeedsOnly ? "seeds" : kind == Kind.CropsOnly ? "crops" : "seeds or crops";
        return $"lose {count} random {what}";
    }
}
