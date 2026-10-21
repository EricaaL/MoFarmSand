using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class StockroomTests
{
    private GameDatabase db;
    private readonly List<Object> created = new List<Object>();

    private T Track<T>(T o) where T : Object { created.Add(o); return o; }

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    /// <summary>The level where every stockroom bonus has kicked in (the upgrade itself has no cap).</summary>
    private int TopLevel => Mathf.Max(3, db.stockroom.extraSeedKindAtLevel, db.stockroom.extraCharmAtLevel);

    private StockroomConfig Config(int basePrice = 25, float growth = 1.8f, int max = 3, int units = 2, int starter = 1, int kindAt = 2, int charmAt = 3)
    {
        StockroomConfig c = Track(TestUtil.Make<StockroomConfig>());
        c.basePrice = basePrice; c.priceGrowth = growth; c.roundTo = 5; c.maxLevel = max;
        c.unitsPerLevel = units; c.starterUnitsPerLevel = starter; c.extraSeedKindAtLevel = kindAt; c.extraCharmAtLevel = charmAt;
        return c;
    }

    // ------------------------------------------------------------------ pricing and buying

    [Test]
    public void EachLevelCostsMoreAndTheFirstIsTheBasePrice()
    {
        StockroomConfig cfg = Config();
        int previous = 0;
        for (int level = 0; level < cfg.maxLevel; level++)
        {
            int price = StockroomLogic.PriceFor(level, cfg);
            Assert.Greater(price, previous, $"level {level + 1}");
            previous = price;
        }
        Assert.AreEqual(25, StockroomLogic.PriceFor(0, cfg));
        Assert.AreEqual(45, StockroomLogic.PriceFor(1, cfg), "25 x 1.8 = 45");
        Assert.AreEqual(80, StockroomLogic.PriceFor(2, cfg), "25 x 1.8^2 = 81 rounds to 80");
    }

    [Test]
    public void FarmlandAndStockroomShareTheSamePricingFormula()
    {
        FarmlandConfig farm = Track(TestUtil.Make<FarmlandConfig>());
        farm.basePrice = 25; farm.priceGrowth = 1.8f; farm.roundTo = 5;
        for (int owned = 0; owned < 5; owned++)
            Assert.AreEqual(StockroomLogic.PriceFor(owned, Config(max: 9)), FarmlandLogic.PriceFor(owned, farm), $"tier {owned}");
    }

    [Test]
    public void TheRealConfigKeepsGettingPricierAndStaysReachableThroughBiggerVaults()
    {
        Assert.IsNotNull(db.stockroom, "run MoFarmSand > Add Run Systems to create StockroomConfig");
        // Unlimited levels: every level costs strictly more, and some Bigger Vault level always holds that price.
        for (int level = 0; level < 25; level++)
        {
            int price = StockroomLogic.PriceFor(level, db.stockroom);
            if (level > 0) Assert.Greater(price, StockroomLogic.PriceFor(level - 1, db.stockroom), $"level {level + 1}");
            int vault = 0;
            while (VaultLogic.CapFor(vault, db.bank) < price) vault++;
            Assert.Less(vault, TierPricing.SanityCap, $"level {level + 1} costs {price}, no vault level holds that");
        }
        Assert.IsFalse(StockroomLogic.IsMaxed(50, db.stockroom), "the stockroom upgrade has no top level");
    }

    [Test]
    public void BuyingSpendsTheVaultStopsAtMaxAndRefusesWhenBroke()
    {
        StockroomConfig cfg = Config(max: 2);
        MetaProgress meta = new MetaProgress { vaultCoins = 60 };

        Assert.IsTrue(StockroomLogic.TryBuy(meta, cfg));
        Assert.AreEqual(35, meta.vaultCoins);
        Assert.AreEqual(1, meta.stockroomLevel);

        Assert.IsFalse(StockroomLogic.TryBuy(meta, cfg), "level 2 costs 45 and you have 35");
        Assert.AreEqual(35, meta.vaultCoins);

        meta.vaultCoins = 500;
        Assert.IsTrue(StockroomLogic.TryBuy(meta, cfg));
        Assert.IsTrue(StockroomLogic.IsMaxed(meta.stockroomLevel, cfg));
        int vault = meta.vaultCoins;
        Assert.IsFalse(StockroomLogic.TryBuy(meta, cfg));
        Assert.AreEqual(vault, meta.vaultCoins, "nothing charged once maxed");
    }

    // ------------------------------------------------------------------ what it does to the shelf

    [Test]
    public void LevelsAddUnitsKindsAndCharmsAtTheConfiguredLevels()
    {
        StockroomConfig cfg = Config();
        Assert.AreEqual(6, StockroomLogic.RestockUnits(6, 0, cfg));
        Assert.AreEqual(8, StockroomLogic.RestockUnits(6, 1, cfg));
        Assert.AreEqual(12, StockroomLogic.RestockUnits(6, 3, cfg));
        Assert.AreEqual(2, StockroomLogic.StarterUnits(2, 0, cfg));
        Assert.AreEqual(5, StockroomLogic.StarterUnits(2, 3, cfg));

        Assert.AreEqual(3, StockroomLogic.SeedKinds(3, 1, cfg), "no extra kind before level 2");
        Assert.AreEqual(4, StockroomLogic.SeedKinds(3, 2, cfg));
        Assert.AreEqual(4, StockroomLogic.SeedKinds(3, 3, cfg), "it's one extra kind, not one per level");
        Assert.AreEqual(2, StockroomLogic.CharmSlots(2, 2, cfg));
        Assert.AreEqual(3, StockroomLogic.CharmSlots(2, 3, cfg));
    }

    [Test]
    public void NullConfigOrLevelZeroChangesNothing()
    {
        Assert.AreEqual(6, StockroomLogic.RestockUnits(6, 3, null));
        Assert.AreEqual(3, StockroomLogic.SeedKinds(3, 3, null));
        Assert.AreEqual(2, StockroomLogic.CharmSlots(2, 3, null));
        Assert.AreEqual(string.Empty, StockroomLogic.DescribeLevel(0, db.stockroom));
    }

    private RunState RunAtLevel(int level, int seed = 7)
    {
        MetaProgress meta = new MetaProgress { stockroomLevel = level };
        return RunLogic.NewRun(seed, db, meta);
    }

    [Test]
    public void MerchantDaysStockMoreOfEverySeedAtHigherLevels()
    {
        CalendarDate summer = new CalendarDate { season = db.calendar.seasons[1] };
        int baseUnits = db.economy.restockSeedStock;

        for (int level = 0; level <= TopLevel; level++)
        {
            RunState run = RunAtLevel(level);
            List<ShopOffer> offers = ShopGenerator.Generate(db, run, summer, new SeededRng(3));
            int expected = StockroomLogic.RestockUnits(baseUnits, level, db.stockroom);
            foreach (ShopOffer o in offers)
                if (o.kind == OfferKind.Seed) Assert.AreEqual(expected, o.stock, $"level {level}");
        }
        Assert.Greater(StockroomLogic.RestockUnits(baseUnits, 1, db.stockroom), baseUnits);
    }

    [Test]
    public void HigherLevelsStockMoreKindsOfSeedAndCharms()
    {
        // Spring stocks four seeds, so the extra kind has something to show (Summer only has three in season).
        CalendarDate summer = new CalendarDate { season = db.calendar.seasons[0] };
        int baseKinds = db.economy.shopSeedSlots, baseCharms = db.economy.shopModifierSlots;

        // Use several seeds and take the best case so a short list of in-season seeds doesn't hide the effect.
        int maxKinds0 = 0, maxKinds2 = 0, charms0 = 0, charms3 = 0;
        for (int s = 0; s < 30; s++)
        {
            List<ShopOffer> low = ShopGenerator.Generate(db, RunAtLevel(0), summer, new SeededRng(s));
            List<ShopOffer> mid = ShopGenerator.Generate(db, RunAtLevel(db.stockroom.extraSeedKindAtLevel), summer, new SeededRng(s));
            List<ShopOffer> top = ShopGenerator.Generate(db, RunAtLevel(TopLevel), summer, new SeededRng(s));
            maxKinds0 = Mathf.Max(maxKinds0, low.FindAll(o => o.kind == OfferKind.Seed).Count);
            maxKinds2 = Mathf.Max(maxKinds2, mid.FindAll(o => o.kind == OfferKind.Seed).Count);
            charms0 = Mathf.Max(charms0, low.FindAll(o => o.kind == OfferKind.Modifier).Count);
            charms3 = Mathf.Max(charms3, top.FindAll(o => o.kind == OfferKind.Modifier).Count);
        }
        Assert.AreEqual(baseKinds, maxKinds0);
        Assert.AreEqual(baseKinds + 1, maxKinds2, "level 2 adds one more kind of seed");
        Assert.AreEqual(baseCharms, charms0);
        Assert.AreEqual(baseCharms + 1, charms3, "the top level adds one more charm");
    }

    [Test]
    public void TheStarterShelfIsBiggerToo()
    {
        RunState plain = RunAtLevel(0), upgraded = RunAtLevel(TopLevel);
        foreach (ShopStockEntry e in plain.shopStock)
            Assert.AreEqual(db.economy.starterSeedStock, e.stock);
        foreach (ShopStockEntry e in upgraded.shopStock)
            Assert.AreEqual(StockroomLogic.StarterUnits(db.economy.starterSeedStock, TopLevel, db.stockroom), e.stock);
        Assert.Greater(upgraded.shopStock[0].stock, plain.shopStock[0].stock);
    }

    // ------------------------------------------------------------------ saves

    [Test]
    public void LevelIsPermanentAndSnapshottedPerYear()
    {
        MetaProgress meta = new MetaProgress { vaultCoins = 500 };
        StockroomLogic.TryBuy(meta, db.stockroom);
        StockroomLogic.TryBuy(meta, db.stockroom);

        RunState run = RunLogic.NewRun(2, db, meta);
        Assert.AreEqual(2, run.stockroomLevel);

        SaveFile back = JsonUtility.FromJson<SaveFile>(JsonUtility.ToJson(new SaveFile { meta = meta, run = run, hasRun = true }));
        Assert.AreEqual(2, back.meta.stockroomLevel);
        Assert.AreEqual(2, back.run.stockroomLevel);
    }

    [Test]
    public void EditedSavesCannotAskForAbsurdLevels()
    {
        SaveFile file = new SaveFile { hasRun = true, run = RunLogic.NewRun(3, db, new MetaProgress()) };
        file.meta.stockroomLevel = 5000;
        file.run.stockroomLevel = 5000;

        SaveService.Sanitize(file, db);

        int expected = db.stockroom.maxLevel > 0 ? db.stockroom.maxLevel : TierPricing.SanityCap;
        Assert.AreEqual(expected, file.meta.stockroomLevel);
        Assert.AreEqual(expected, file.run.stockroomLevel);
    }

    [Test]
    public void ShopTextDescribesWhatEachLevelAdds()
    {
        StockroomConfig cfg = Config();
        Assert.That(StockroomLogic.DescribeLevel(1, cfg), Does.Contain("+2 of every seed"));
        Assert.That(StockroomLogic.DescribeLevel(2, cfg), Does.Contain("+1 kind of seed"));
        Assert.That(StockroomLogic.DescribeLevel(3, cfg), Does.Contain("+1 charm"));
        Assert.That(StockroomLogic.DescribeLevel(1, cfg), Does.Not.Contain("charm"));
    }
}
