using System.Collections.Generic;
using UnityEngine;

public enum DayOutcome { Survived, BossDefeated, Failed }

/// <summary>Pure run transitions. No scene access; everything is driven by RunDirector.</summary>
public static class RunLogic
{
    public static RunState NewRun(int seed, GameDatabase db, MetaProgress meta, RunState previous = null)
    {
        RunState run = new RunState { runSeed = seed };
        run.map = MapGenerator.GenerateValidated(db.mapGen, db.Calendar.MapLayerCount, db.nodeTypes, seed, db.Calendar.SeasonBossDays());
        run.maxHp = db.run.startingMaxHp;
        run.hp = run.maxHp;
        InventoryOps.EnsureSize(run.inventory);

        // Remember which permanent unlocks (including new crops) apply to this year.
        run.unlockedIds = new List<string>(meta.unlockedIds);
        run.farmlands = TierPricing.ClampOwned(meta.farmlands, db.farmland != null ? db.farmland.maxFarmlands : 0);
        run.stockroomLevel = TierPricing.ClampOwned(meta.stockroomLevel, db.stockroom != null ? db.stockroom.maxLevel : 0);

        // Every year starts with the same small purse. Vault coins are upgrade currency, never run money.
        run.coins = db.run.startingCoins;

        GrantStartingSeeds(run, db);

        // A small starter shelf so the merchant is usable from day one, until the first Merchant day restocks him.
        run.shopStock = ShopStock.FromOffers(ShopGenerator.Generate(db, run, db.Calendar.DateOf(0),
            SeededRng.Derive(seed, 0, RngStreams.Shop), includeModifiers: false,
            seedStock: StockroomLogic.StarterUnits(db.economy.starterSeedStock, run.stockroomLevel, db.stockroom)));
        foreach (string id in meta.unlockedIds)
        {
            if (db.unlocks == null || !db.unlocks.TryGet(id, out UnlockData unlock)) continue;
            foreach (RunEffect e in unlock.onRunStart) if (e != null) e.ApplyAtYearStart(run, db, previous);
        }

        meta.runsStarted++;
        run.year = meta.runsStarted; // "Year 1", "Year 2", ...
        run.phase = RunPhase.ChoosingNode;
        return run;
    }

    /// <summary>A few random seed types, deterministic for the run seed and limited to seeds sold on day 0.</summary>
    public static void GrantStartingSeeds(RunState run, GameDatabase db)
    {
        SeededRng rng = SeededRng.Derive(run.runSeed, 0, RngStreams.Loadout);
        CalendarDate day0 = db.Calendar.DateOf(0);

        List<RunConfig.SeedPoolEntry> candidates = new List<RunConfig.SeedPoolEntry>(db.run.startingSeedPool);

        // Crop variants you've unlocked can turn up in your starting seeds too.
        if (db.unlocks != null)
            foreach (UnlockData u in db.unlocks.All)
            {
                if (u == null || !u.UnlocksCrops || !run.unlockedIds.Contains(u.Id)) continue;
                foreach (SeedData s in u.unlocksSeeds)
                    if (s != null && !candidates.Exists(c => c.seed == s))
                        candidates.Add(new RunConfig.SeedPoolEntry { seed = s, weight = db.run.unlockedSeedWeight, countRange = db.run.unlockedSeedCount });
            }

        List<RunConfig.SeedPoolEntry> pool = new List<RunConfig.SeedPoolEntry>();
        foreach (RunConfig.SeedPoolEntry e in candidates)
        {
            if (e.seed == null || e.weight <= 0f) continue;
            if (!CropAvailability.IsSeedAvailable(e.seed, run.unlockedIds, db)) continue;
            PriceTable.PriceRow row = db.prices != null ? db.prices.RowForSeed(e.seed.Id) : null;
            if (row != null && !row.StockedIn(day0.season)) continue;
            pool.Add(e);
        }

        int picks = Mathf.Min(rng.NextInt(db.run.startingSeedPicks.x, db.run.startingSeedPicks.y + 1), pool.Count);
        for (int i = 0; i < picks; i++)
        {
            RunConfig.SeedPoolEntry pick = rng.PickWeighted(pool, e => e.weight);
            pool.Remove(pick);
            int count = Mathf.Max(1, rng.NextInt(pick.countRange.x, pick.countRange.y + 1));
            InventoryOps.Add(run.inventory, pick.seed.Id, count);
        }
    }

    public static bool CanEnter(RunState run, int nodeId)
    {
        if (run.phase != RunPhase.ChoosingNode) return false;
        MapNode target = run.map.Get(nodeId);
        if (target == null) return false;
        if (run.freeRoam) return true;
        if (run.currentNodeId < 0) return target.layer == 0;
        MapNode current = run.map.Get(run.currentNodeId);
        return current != null && current.next.Contains(nodeId);
    }

    public static bool BeginNode(RunState run, int nodeId)
    {
        if (!CanEnter(run, nodeId)) return false;
        run.pendingNodeId = nodeId;
        run.phase = RunPhase.InDay;
        return true;
    }

    /// <summary>Finishes the pending day. The live inventory/health/wallet must already be snapshotted into run.</summary>
    public static void CompleteNode(RunState run, MetaProgress meta, DayOutcome outcome, DayPlan plan, GameDatabase db)
    {
        if (run.freeRoam) { CompleteFreeRoamNode(run, outcome); return; }

        // Losing a season boss is survivable (it only slows next season's crops); every other loss ends the year.
        bool softLoss = outcome == DayOutcome.Failed && plan.node != null && plan.node.survivableLoss;
        if (outcome == DayOutcome.Failed && !softLoss) { FailRun(run, meta); return; }

        MapNode node = run.map.Get(run.pendingNodeId);
        run.visited.Add(run.pendingNodeId);
        run.currentNodeId = run.pendingNodeId;
        run.pendingNodeId = -1;

        run.stats.daysSurvived++;
        if (node != null) run.stats.layersReached = Mathf.Max(run.stats.layersReached, node.layer + 1);
        if (outcome == DayOutcome.BossDefeated) run.stats.bossesFed++;

        int reward = softLoss ? 0 : plan.coinReward;
        run.coins += reward;
        run.stats.coinsEarned += reward;
        EconomyModel.AdvanceDay(run.market, db.economy, SeededRng.Derive(run.runSeed, node != null ? node.id : 0, RngStreams.Market));

        // The slow-growth debuff lasts one season. Clear it when its season ends, then arm a new one for the next season.
        if (plan.date.IsLastDayOfSeason && run.growthDebuffSeason == plan.date.seasonIndex) ClearGrowthDebuff(run);
        if (softLoss)
        {
            run.growthDebuffSeason = plan.date.seasonIndex + 1;
            run.growthDebuffMult = plan.node.lossGrowthMultiplier;
            run.hp = Mathf.Max(1, run.hp);
        }

        bool finalBoss = plan.node != null && plan.node.endsRun;
        if (finalBoss && !plan.date.IsLastDayOfSeason)
        {
            EndRun(run, meta, true);
            return;
        }

        if (plan.date.IsLastDayOfSeason)
        {
            // Even the final boss leaves one last bank stop before the year ends.
            run.phase = RunPhase.Banking;
            run.bankSeasonIndex = plan.date.seasonIndex;
            run.depositedThisSeason = 0;
            run.endYearAfterBanking = finalBoss;
        }
        else
        {
            run.phase = RunPhase.ChoosingNode;
        }
    }

    /// <summary>Beta days: win or lose, record the visit, refill health and go straight back to the map. No rewards, banking or year end.</summary>
    private static void CompleteFreeRoamNode(RunState run, DayOutcome outcome)
    {
        MapNode node = run.map.Get(run.pendingNodeId);
        if (node != null && !run.visited.Contains(node.id)) run.visited.Add(node.id);
        run.currentNodeId = run.pendingNodeId;
        run.pendingNodeId = -1;
        run.stats.daysSurvived++;
        if (node != null) run.stats.layersReached = Mathf.Max(run.stats.layersReached, node.layer + 1);
        if (outcome == DayOutcome.BossDefeated) run.stats.bossesFed++;
        run.hp = run.maxHp;
        run.endYearAfterBanking = false;
        run.phase = RunPhase.ChoosingNode;
    }

    private static void ClearGrowthDebuff(RunState run)
    {
        run.growthDebuffSeason = -1;
        run.growthDebuffMult = 1f;
    }

    public static void FinishBanking(RunState run, MetaProgress meta)
    {
        if (run.phase != RunPhase.Banking) return;
        if (run.endYearAfterBanking)
        {
            run.endYearAfterBanking = false;
            EndRun(run, meta, true);
            return;
        }
        run.phase = RunPhase.ChoosingNode;
    }

    public static void FailRun(RunState run, MetaProgress meta)
    {
        run.pendingNodeId = -1;
        EndRun(run, meta, false);
    }

    private static void EndRun(RunState run, MetaProgress meta, bool won)
    {
        run.phase = won ? RunPhase.Won : RunPhase.Lost;
        // Anything still in the wallet is lost: that is the point of the bank.
        run.stats.coinsLost = run.coins;
        run.coins = 0;
        if (won) meta.runsWon++;
        meta.bestLayer = Mathf.Max(meta.bestLayer, run.stats.layersReached);
        meta.lastRunDeposited = run.depositsThisRun;
        meta.lastRunCoinsLost = run.stats.coinsLost;
    }
}
