using System.Collections.Generic;
using NUnit.Framework;

public class RunLogicTests
{
    private GameDatabase db;

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    private static int CountSeeds(RunState run, GameDatabase d)
    {
        int n = 0;
        foreach (SlotSave s in run.inventory)
            if (s.count > 0 && d.items.Get(s.itemId) is SeedData) n += s.count;
        return n;
    }

    [Test]
    public void StartingLoadoutIsRandomSeedsOnlyAndDeterministicPerSeed()
    {
        RunState a = RunLogic.NewRun(77, db, new MetaProgress());
        RunState b = RunLogic.NewRun(77, db, new MetaProgress());

        Assert.AreEqual(UnityEngine.JsonUtility.ToJson(new Wrapper { slots = a.inventory }), UnityEngine.JsonUtility.ToJson(new Wrapper { slots = b.inventory }));
        Assert.Greater(CountSeeds(a, db), 0, "a new run starts with seeds");
        foreach (SlotSave s in a.inventory)
            if (s.count > 0) Assert.IsInstanceOf<SeedData>(db.items.Get(s.itemId), "no crops at the start");

        // Different seeds should not all give the same loadout.
        HashSet<string> variety = new HashSet<string>();
        for (int seed = 0; seed < 40; seed++)
            variety.Add(UnityEngine.JsonUtility.ToJson(new Wrapper { slots = RunLogic.NewRun(seed, db, new MetaProgress()).inventory }));
        Assert.Greater(variety.Count, 5, "loadouts vary between runs");
    }

    [System.Serializable]
    private class Wrapper { public List<SlotSave> slots; }

    [Test]
    public void StartingSeedsAreSoldInTheFirstSeason()
    {
        CalendarDate day0 = db.Calendar.DateOf(0);
        for (int seed = 0; seed < 50; seed++)
        {
            RunState run = RunLogic.NewRun(seed, db, new MetaProgress());
            foreach (SlotSave s in run.inventory)
            {
                if (s.count <= 0) continue;
                Assert.IsTrue(db.prices.RowForSeed(s.itemId).StockedIn(day0.season), $"{s.itemId} is not stocked in {day0.season.displayName}");
            }
        }
    }

    [Test]
    public void OnlyChildrenOfTheCurrentNodeCanBeEntered()
    {
        MetaProgress meta = new MetaProgress();
        RunState run = RunLogic.NewRun(5, db, meta);

        List<MapNode> starts = run.map.NodesInLayer(0);
        foreach (MapNode n in starts) Assert.IsTrue(RunLogic.CanEnter(run, n.id));
        foreach (MapNode n in run.map.NodesInLayer(1)) Assert.IsFalse(RunLogic.CanEnter(run, n.id), "can't skip ahead");

        Assert.IsTrue(RunLogic.BeginNode(run, starts[0].id));
        Assert.AreEqual(RunPhase.InDay, run.phase);
        Assert.IsFalse(RunLogic.BeginNode(run, starts[0].id), "can't start a second day while one is running");
    }

    [Test]
    public void FullRunVisitsEverySeasonAndStopsAtTheBankOncePerSeason()
    {
        MetaProgress meta = new MetaProgress();
        RunState run = RunLogic.NewRun(21, db, meta);
        int banks = 0, guard = 0;

        while (run.phase != RunPhase.Won && run.phase != RunPhase.Lost && guard++ < 60)
        {
            if (run.phase == RunPhase.Banking)
            {
                banks++;
                RunLogic.FinishBanking(run, meta);
                continue;
            }
            MapNode cur = run.currentNodeId < 0 ? null : run.map.Get(run.currentNodeId);
            int next = cur == null ? run.map.NodesInLayer(0)[0].id : cur.next[0];
            Assert.IsTrue(RunLogic.BeginNode(run, next));
            DayPlan plan = DayPlanner.Plan(run, next, db);
            DayOutcome outcome = plan.encounter != null ? DayOutcome.BossDefeated : DayOutcome.Survived;
            RunLogic.CompleteNode(run, meta, outcome, plan, db);
        }

        Assert.AreEqual(RunPhase.Won, run.phase);
        Assert.AreEqual(db.calendar.seasons.Count, banks, "one bank stop at the end of each season");
        Assert.AreEqual(db.Calendar.TotalDays, run.visited.Count, "every layer plus the final boss day");
        Assert.AreEqual(1, meta.runsWon);
    }

    [Test]
    public void BankStopHappensExactlyOnTheLastDayOfASeason()
    {
        MetaProgress meta = new MetaProgress();
        RunState run = RunLogic.NewRun(33, db, meta);

        for (int day = 0; day < db.Calendar.TotalDays; day++)
        {
            MapNode cur = run.currentNodeId < 0 ? null : run.map.Get(run.currentNodeId);
            int next = cur == null ? run.map.NodesInLayer(0)[0].id : cur.next[0];
            RunLogic.BeginNode(run, next);
            DayPlan plan = DayPlanner.Plan(run, next, db);
            RunLogic.CompleteNode(run, meta, DayOutcome.Survived, plan, db);

            bool lastOfSeason = db.Calendar.DateOf(day).IsLastDayOfSeason;
            Assert.AreEqual(lastOfSeason ? RunPhase.Banking : RunPhase.ChoosingNode, run.phase, $"day {day}");
            if (lastOfSeason) RunLogic.FinishBanking(run, meta);
        }
    }

    private (RunState run, MetaProgress meta) RunAtSeasonBoss(int seasonIndex)
    {
        MetaProgress meta = new MetaProgress();
        RunState run = RunLogic.NewRun(11, db, meta);
        int bossDay = db.Calendar.SeasonBossDays()[seasonIndex];
        for (int day = 0; day < bossDay; day++)
        {
            MapNode cur = run.currentNodeId < 0 ? null : run.map.Get(run.currentNodeId);
            int n = cur == null ? run.map.NodesInLayer(0)[0].id : cur.next[0];
            RunLogic.BeginNode(run, n);
            RunLogic.CompleteNode(run, meta, DayOutcome.Survived, DayPlanner.Plan(run, n, db), db);
            if (run.phase == RunPhase.Banking) RunLogic.FinishBanking(run, meta);
        }
        return (run, meta);
    }

    [Test]
    public void LosingASeasonBossEndsTheYear()
    {
        (RunState run, MetaProgress meta) = RunAtSeasonBoss(0);
        MapNode cur = run.currentNodeId < 0 ? null : run.map.Get(run.currentNodeId);
        int bossNode = cur.next[0];
        Assert.AreEqual("season_boss", run.map.Get(bossNode).nodeTypeId);
        RunLogic.BeginNode(run, bossNode);
        DayPlan plan = DayPlanner.Plan(run, bossNode, db);
        run.hp = 0;

        RunLogic.CompleteNode(run, meta, DayOutcome.Failed, plan, db);

        Assert.AreEqual(RunPhase.Lost, run.phase, "no soft landing: the year is over");
        Assert.AreEqual(-1, run.growthDebuffSeason, "nothing carries over to a next season");
    }

    [Test]
    public void TheDebuffOnlyLastsOneSeasonAndWinningArmsNothing()
    {
        (RunState run, MetaProgress meta) = RunAtSeasonBoss(0);
        MapNode cur = run.map.Get(run.currentNodeId);
        int bossNode = cur.next[0];
        RunLogic.BeginNode(run, bossNode);
        RunLogic.CompleteNode(run, meta, DayOutcome.BossDefeated, DayPlanner.Plan(run, bossNode, db), db);
        Assert.AreEqual(-1, run.growthDebuffSeason, "winning leaves the soil alone");

        run.growthDebuffSeason = 1; run.growthDebuffMult = 0.65f;
        RunLogic.FinishBanking(run, meta);
        for (int day = db.Calendar.DateOf(0).daysInSeason; day < db.Calendar.DateOf(0).daysInSeason * 2; day++)
        {
            MapNode c = run.map.Get(run.currentNodeId);
            int n = c.next[0];
            RunLogic.BeginNode(run, n);
            DayPlan p = DayPlanner.Plan(run, n, db);
            Assert.AreEqual(1, p.date.seasonIndex);
            RunLogic.CompleteNode(run, meta, DayOutcome.Survived, p, db);
            if (run.phase == RunPhase.Banking) RunLogic.FinishBanking(run, meta);
        }
        Assert.AreEqual(-1, run.growthDebuffSeason, "cleared when its season ended");
    }

    [Test]
    public void LosingTheFinalBossOrAnOrdinaryDayStillEndsTheYear()
    {
        MetaProgress meta = new MetaProgress();
        RunState run = RunLogic.NewRun(12, db, meta);
        int start = run.map.NodesInLayer(0)[0].id;
        RunLogic.BeginNode(run, start);
        RunLogic.CompleteNode(run, meta, DayOutcome.Failed, DayPlanner.Plan(run, start, db), db);
        Assert.AreEqual(RunPhase.Lost, run.phase);
        Assert.IsFalse(db.nodeTypes.Get("boss").survivableLoss);
    }

    [Test]
    public void FailingTheRunLosesTheWalletAndRecordsStats()
    {
        MetaProgress meta = new MetaProgress();
        RunState run = RunLogic.NewRun(8, db, meta);
        run.coins = 37;
        run.depositsThisRun = 12;

        RunLogic.FailRun(run, meta);

        Assert.AreEqual(RunPhase.Lost, run.phase);
        Assert.AreEqual(0, run.coins);
        Assert.AreEqual(37, run.stats.coinsLost);
        Assert.AreEqual(37, meta.lastRunCoinsLost);
        Assert.AreEqual(12, meta.lastRunDeposited);
        Assert.AreEqual(0, meta.runsWon);
    }

    [Test]
    public void FailedOutcomeInCompleteNodeEndsTheRun()
    {
        MetaProgress meta = new MetaProgress();
        RunState run = RunLogic.NewRun(9, db, meta);
        int start = run.map.NodesInLayer(0)[0].id;
        RunLogic.BeginNode(run, start);
        RunLogic.CompleteNode(run, meta, DayOutcome.Failed, DayPlanner.Plan(run, start, db), db);
        Assert.AreEqual(RunPhase.Lost, run.phase);
        Assert.AreEqual(0, run.visited.Count, "a failed day doesn't count as visited");
    }

    [Test]
    public void DayPlanIsDeterministicForTheSameSeedAndNode()
    {
        RunState run = RunLogic.NewRun(404, db, new MetaProgress());
        foreach (MapNode n in run.map.nodes)
        {
            DayPlan a = DayPlanner.Plan(run, n.id, db), b = DayPlanner.Plan(run, n.id, db);
            Assert.AreEqual(a.weather.temperature, b.weather.temperature);
            Assert.AreEqual(a.weather.kind, b.weather.kind);
            Assert.AreEqual(a.disaster.disasterId, b.disaster.disasterId);
            Assert.AreEqual(a.disaster.severity01, b.disaster.severity01);
            Assert.AreEqual(a.coinReward, b.coinReward);
            Assert.AreEqual(a.offers.Count, b.offers.Count);
            Assert.AreSame(a.evt, b.evt);
            for (int i = 0; i < a.offers.Count; i++) Assert.AreEqual(a.offers[i].id, b.offers[i].id);
        }
    }

    [Test]
    public void DayPlanFollowsTheNodeTypeFlags()
    {
        RunState run = RunLogic.NewRun(2, db, new MetaProgress());
        bool sawMerchant = false, sawDisaster = false, sawEncounter = false;
        for (int seed = 0; seed < 30; seed++)
        {
            run = RunLogic.NewRun(seed, db, new MetaProgress());
            foreach (MapNode n in run.map.nodes)
            {
                DayPlan p = DayPlanner.Plan(run, n.id, db);
                Assert.IsNotEmpty(p.offers.FindAll(o => o.kind == OfferKind.Seed), "seeds are on sale every day");
                Assert.AreEqual(p.node.merchantActive, p.restocked, "only merchant days restock");
                if (p.node.merchantActive) { sawMerchant = true; Assert.IsNotEmpty(p.offers.FindAll(o => o.kind == OfferKind.Modifier), "merchant days also stock charms"); }
                else Assert.IsEmpty(p.offers.FindAll(o => o.kind == OfferKind.Modifier), "charms are only sold on merchant days");
                if (p.node.disasterChance >= 1f) { sawDisaster = true; Assert.IsFalse(p.disaster.IsNone, "disaster nodes always roll a disaster"); }
                if (p.node.encounter != null) { sawEncounter = true; Assert.AreSame(p.node.encounter, p.encounter); }
                if (p.node.endsRun) Assert.IsTrue(p.node.encounter != null);
            }
        }
        Assert.IsTrue(sawMerchant && sawDisaster && sawEncounter);
    }

    private static MapNode FindNode(RunState run, bool merchant)
    {
        foreach (MapNode n in run.map.nodes)
            if ((n.nodeTypeId == "merchant") == merchant && n.nodeTypeId != "boss") return n;
        return null;
    }

    private static RunState RunWithBothNodeKinds(GameDatabase db)
    {
        for (int seed = 0; seed < 100; seed++)
        {
            RunState run = RunLogic.NewRun(seed, db, new MetaProgress());
            if (FindNode(run, true) != null && FindNode(run, false) != null) return run;
        }
        Assert.Fail("no seed produced both a merchant and a normal node");
        return null;
    }

    [Test]
    public void NewRunStartsWithASmallShelfAndNoCharms()
    {
        RunState run = RunLogic.NewRun(12, db, new MetaProgress());

        Assert.IsNotEmpty(run.shopStock, "the merchant is usable from day one");
        foreach (ShopStockEntry e in run.shopStock)
        {
            Assert.AreEqual(OfferKind.Seed, e.kind, "charms only arrive on merchant days");
            Assert.AreEqual(db.economy.starterSeedStock, e.stock);
        }
        Assert.Less(db.economy.starterSeedStock, db.economy.restockSeedStock, "starter shelf is smaller than a restock");
    }

    [Test]
    public void NormalDaysSellFromTheShelfAndMerchantDaysRestockWithCharms()
    {
        RunState run = RunWithBothNodeKinds(db);

        DayPlan normal = DayPlanner.Plan(run, FindNode(run, false).id, db);
        Assert.IsFalse(normal.restocked);
        Assert.IsNotEmpty(normal.offers);
        foreach (ShopOffer o in normal.offers)
        {
            Assert.AreEqual(OfferKind.Seed, o.kind);
            Assert.AreEqual(db.economy.starterSeedStock, o.stock);
        }

        DayPlan merchant = DayPlanner.Plan(run, FindNode(run, true).id, db);
        Assert.IsTrue(merchant.restocked);
        Assert.IsTrue(merchant.offers.Exists(o => o.kind == OfferKind.Modifier), "charms are stocked on merchant days");
        foreach (ShopOffer o in merchant.offers)
            if (o.kind == OfferKind.Seed) Assert.AreEqual(db.economy.restockSeedStock, o.stock);
    }

    [Test]
    public void LeftoversCarryOverAndOnlyMerchantDaysRefill()
    {
        RunState run = RunWithBothNodeKinds(db);
        MapNode normalNode = FindNode(run, false), merchantNode = FindNode(run, true);

        // Buy out the first seed on a normal day; the shelf is saved at the end of the day.
        DayPlan day1 = DayPlanner.Plan(run, normalNode.id, db);
        string soldOut = day1.offers[0].id;
        ShopOffer first = day1.offers[0];
        first.stock = 0;
        day1.offers[0] = first;
        run.shopStock = ShopStock.FromOffers(day1.offers);

        DayPlan day2 = DayPlanner.Plan(run, normalNode.id, db);
        Assert.AreEqual(day1.offers.Count - 1, day2.offers.Count);
        Assert.IsFalse(day2.offers.Exists(o => o.id == soldOut), "it stays sold out until a merchant day");

        // Partial purchase also carries over.
        ShopOffer second = day2.offers[0];
        second.stock = 1;
        day2.offers[0] = second;
        run.shopStock = ShopStock.FromOffers(day2.offers);
        Assert.AreEqual(1, DayPlanner.Plan(run, normalNode.id, db).offers[0].stock);

        // A merchant day brings fresh stock regardless of the leftover shelf.
        DayPlan restock = DayPlanner.Plan(run, merchantNode.id, db);
        Assert.IsTrue(restock.restocked);
        foreach (ShopOffer o in restock.offers)
            if (o.kind == OfferKind.Seed) Assert.AreEqual(db.economy.restockSeedStock, o.stock);

        run.shopStock = ShopStock.FromOffers(restock.offers);
        Assert.IsTrue(DayPlanner.Plan(run, normalNode.id, db).offers.Exists(o => o.kind == OfferKind.Modifier), "unbought charms stay on the shelf");
    }

    [Test]
    public void OwnedCharmsDisappearFromTheShelf()
    {
        RunState run = RunWithBothNodeKinds(db);
        DayPlan restock = DayPlanner.Plan(run, FindNode(run, true).id, db);
        run.shopStock = ShopStock.FromOffers(restock.offers);
        ShopOffer charm = restock.offers.Find(o => o.kind == OfferKind.Modifier);

        run.modifierIds.Add(charm.id);

        Assert.IsFalse(DayPlanner.Plan(run, FindNode(run, false).id, db).offers.Exists(o => o.id == charm.id));
    }

    [Test]
    public void ResistingModifiersLowerDisasterSeverityAndBoostGrowth()
    {
        RunState run = RunLogic.NewRun(3, db, new MetaProgress());
        Assert.AreEqual(0f, DayPlanner.ResistFor(run, db, "locust"));
        Assert.AreEqual(1f, DayPlanner.ModifierGrowth(run, db));

        run.modifierIds.Add("scarecrow");
        run.modifierIds.Add("fertilizer");
        Assert.AreEqual(0.5f, DayPlanner.ResistFor(run, db, "locust"), 1e-5f);
        Assert.AreEqual(0f, DayPlanner.ResistFor(run, db, "storm"));
        Assert.AreEqual(1.2f, DayPlanner.ModifierGrowth(run, db), 1e-5f);
    }

    [Test]
    public void FreeRoamCanEnterAnyNodeAndNeverEndsOrBanks()
    {
        MetaProgress meta = new MetaProgress();
        RunState run = RunLogic.NewRun(9, db, meta);
        run.freeRoam = true;
        foreach (MapNode n in run.map.nodes) Assert.IsTrue(RunLogic.CanEnter(run, n.id), "every node is open");

        // Jump straight to the final boss and lose: back on the map at full health, year still alive.
        Assert.IsTrue(RunLogic.BeginNode(run, run.map.bossId));
        DayPlan plan = DayPlanner.Plan(run, run.map.bossId, db);
        run.hp = 0;
        RunLogic.CompleteNode(run, meta, DayOutcome.Failed, plan, db);
        Assert.AreEqual(RunPhase.ChoosingNode, run.phase);
        Assert.AreEqual(run.maxHp, run.hp);

        // Beat a season boss: no bank stop, no year end.
        MapNode seasonBoss = run.map.nodes.Find(n => n.nodeTypeId == "season_boss");
        Assert.IsTrue(RunLogic.BeginNode(run, seasonBoss.id));
        RunLogic.CompleteNode(run, meta, DayOutcome.BossDefeated, DayPlanner.Plan(run, seasonBoss.id, db), db);
        Assert.AreEqual(RunPhase.ChoosingNode, run.phase);
        Assert.IsFalse(run.endYearAfterBanking);
    }

    [Test]
    public void BetaKitHoldsEverySeasonsCropsAndOneOfEachKind() // seeds only fill leftover slots
    {
        RunState run = RunLogic.NewRun(4, db, new MetaProgress());
        for (int layer = 0; layer < db.Calendar.TotalDays; layer += db.Calendar.DateOf(layer).season.lengthInDays)
        {
            SeasonData season = db.Calendar.DateOf(layer).season;
            BetaKit.Fill(run, db, season);
            HashSet<CropType> kinds = new HashSet<CropType>();
            foreach (SlotSave s in run.inventory)
                if (s.count > 0 && db.items.Get(s.itemId) is CropData crop) kinds.Add(crop.type);
            Assert.AreEqual(3, kinds.Count, $"{season.displayName} kit has projectile, shooter and support crops");
            int filled = 0;
            foreach (SlotSave s in run.inventory) if (s.count > 0) filled++;
            Assert.LessOrEqual(filled, InventoryOps.Slots - BetaKit.FreeSlots, $"{season.displayName} kit leaves a slot free for farmland plots");
            foreach (PriceTable.PriceRow row in db.prices.rows)
                if (row.StockedIn(season)) Assert.Greater(InventoryOps.CountOf(run.inventory, row.crop.Id), 0, $"{row.crop.Id} in {season.displayName}");
        }
    }
}
