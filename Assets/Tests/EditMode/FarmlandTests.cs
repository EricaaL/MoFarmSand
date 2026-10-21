using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class FarmlandTests
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

    /// <summary>The scene's real field: 3 columns x 3 rows, 2.2 apart, rows at z = 3, 5.2, 7.4.</summary>
    private static List<Vector3> BaseField()
    {
        List<Vector3> list = new List<Vector3>();
        for (int r = 0; r < 3; r++)
            for (int c = -1; c <= 1; c++) list.Add(new Vector3(c * 2.2f, 0.1f, 3f + r * 2.2f));
        return list;
    }

    private FarmlandConfig Config(int basePrice = 30, float growth = 1.7f, int roundTo = 5, int max = 4)
    {
        FarmlandConfig c = Track(TestUtil.Make<FarmlandConfig>());
        c.basePrice = basePrice; c.priceGrowth = growth; c.roundTo = roundTo; c.maxFarmlands = max;
        return c;
    }

    // ------------------------------------------------------------------ pricing

    [Test]
    public void EachFarmlandCostsMoreThanTheLast()
    {
        FarmlandConfig cfg = Config();
        int previous = 0;
        for (int owned = 0; owned < cfg.maxFarmlands; owned++)
        {
            int price = FarmlandLogic.PriceFor(owned, cfg);
            Assert.Greater(price, previous, $"farmland #{owned + 1} should cost more than #{owned}");
            previous = price;
        }
        Assert.AreEqual(30, FarmlandLogic.PriceFor(0, cfg), "the first one costs the base price");
    }

    [Test]
    public void PricesRoundToCleanNumbers()
    {
        FarmlandConfig cfg = Config(basePrice: 30, growth: 1.7f, roundTo: 5);
        for (int owned = 0; owned < 6; owned++)
            Assert.AreEqual(0, FarmlandLogic.PriceFor(owned, cfg) % 5, $"price after {owned} owned");
        Assert.AreEqual(50, FarmlandLogic.PriceFor(1, cfg), "30 x 1.7 = 51 rounds to 50");
        Assert.AreEqual(85, FarmlandLogic.PriceFor(2, cfg), "30 x 1.7^2 = 86.7 rounds to 85");
    }

    [Test]
    public void TheRealConfigKeepsGettingPricierAndStaysReachableThroughBiggerVaults()
    {
        Assert.IsNotNull(db.farmland, "run MoFarmSand > Add Run Systems to create FarmlandConfig");
        // Unlimited farmland: every purchase costs strictly more, and some Bigger Vault level always holds that price.
        for (int owned = 0; owned < 25; owned++)
        {
            int price = FarmlandLogic.PriceFor(owned, db.farmland);
            if (owned > 0) Assert.Greater(price, FarmlandLogic.PriceFor(owned - 1, db.farmland), $"farmland #{owned + 1}");
            int level = 0;
            while (VaultLogic.CapFor(level, db.bank) < price) level++;
            Assert.Less(level, TierPricing.SanityCap, $"farmland #{owned + 1} costs {price}, no vault level holds that");
        }
        Assert.Greater(FarmlandLogic.PriceFor(3, db.farmland), FarmlandLogic.PriceFor(0, db.farmland));
        Assert.IsFalse(FarmlandLogic.IsMaxed(50, db.farmland), "farmland has no top end");
    }

    // ------------------------------------------------------------------ buying

    [Test]
    public void BuyingSpendsTheVaultAndRaisesTheNextPrice()
    {
        FarmlandConfig cfg = Config();
        MetaProgress meta = new MetaProgress { vaultCoins = 100 };

        Assert.IsTrue(FarmlandLogic.TryBuy(meta, cfg));
        Assert.AreEqual(70, meta.vaultCoins);
        Assert.AreEqual(1, meta.farmlands);

        Assert.IsTrue(FarmlandLogic.TryBuy(meta, cfg), "second one costs 50");
        Assert.AreEqual(20, meta.vaultCoins);
        Assert.AreEqual(2, meta.farmlands);

        Assert.IsFalse(FarmlandLogic.TryBuy(meta, cfg), "third costs 85: can't afford it");
        Assert.AreEqual(20, meta.vaultCoins);
        Assert.AreEqual(2, meta.farmlands);
    }

    [Test]
    public void CannotBuyPastTheMaximum()
    {
        FarmlandConfig cfg = Config(max: 2);
        MetaProgress meta = new MetaProgress { vaultCoins = 9999 };
        Assert.IsTrue(FarmlandLogic.TryBuy(meta, cfg));
        Assert.IsTrue(FarmlandLogic.TryBuy(meta, cfg));
        int vault = meta.vaultCoins;

        Assert.IsTrue(FarmlandLogic.IsMaxed(meta.farmlands, cfg));
        Assert.IsFalse(FarmlandLogic.TryBuy(meta, cfg));
        Assert.AreEqual(vault, meta.vaultCoins, "nothing is charged once maxed");
    }

    [Test]
    public void FarmlandsSurviveSavingAndStartEveryYearWithWhatYouOwn()
    {
        MetaProgress meta = new MetaProgress { vaultCoins = 500 };
        FarmlandLogic.TryBuy(meta, db.farmland);
        FarmlandLogic.TryBuy(meta, db.farmland);

        RunState run = RunLogic.NewRun(4, db, meta);
        Assert.AreEqual(2, run.farmlands, "the year starts with your farmlands");
        Assert.AreEqual(db.run.startingCoins, run.coins, "buying farmland doesn't touch the purse");

        SaveFile file = new SaveFile { meta = meta, run = run, hasRun = true };
        SaveFile back = JsonUtility.FromJson<SaveFile>(JsonUtility.ToJson(file));
        Assert.AreEqual(2, back.meta.farmlands);
        Assert.AreEqual(2, back.run.farmlands);
    }

    [Test]
    public void EditedSavesCannotGrantAbsurdAmountsOfFarmland()
    {
        SaveFile file = new SaveFile { hasRun = true, run = RunLogic.NewRun(3, db, new MetaProgress()) };
        file.meta.farmlands = 5000;
        file.run.farmlands = 5000;
        file.run.map = file.run.map; // keep it valid

        SaveService.Sanitize(file, db);

        int expected = db.farmland.maxFarmlands > 0 ? db.farmland.maxFarmlands : TierPricing.SanityCap;
        Assert.AreEqual(expected, file.meta.farmlands);
        Assert.AreEqual(expected, file.run.farmlands);
    }

    // ------------------------------------------------------------------ layout

    [Test]
    public void EachFarmlandAddsOneFullColumnOfPlots()
    {
        List<Vector3> field = BaseField();
        Assert.AreEqual(3, FarmlandLogic.PlotsPerFarmland(field), "a column is as tall as the field");

        for (int n = 0; n <= 4; n++)
            Assert.AreEqual(n * 3, FarmlandLogic.PositionsFor(n, field).Count, $"{n} farmlands");
    }

    [Test]
    public void NewPlotsGoToTheRightThenTheLeftAndNeverOverlapAnything()
    {
        List<Vector3> field = BaseField();
        List<Vector3> extra = FarmlandLogic.PositionsFor(4, field);

        // First farmland: right of the field. Second: left. Then further out.
        Assert.AreEqual(4.4f, extra[0].x, 1e-3f);
        Assert.AreEqual(-4.4f, extra[3].x, 1e-3f);
        Assert.AreEqual(6.6f, extra[6].x, 1e-3f);
        Assert.AreEqual(-6.6f, extra[9].x, 1e-3f);

        List<Vector3> all = new List<Vector3>(field);
        all.AddRange(extra);
        for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
                Assert.GreaterOrEqual(Vector3.Distance(all[i], all[j]), 2.1f, $"plots {i} and {j} overlap");

        foreach (Vector3 p in extra)
            Assert.AreEqual(0.1f, p.y, 1e-4f, "same ground height");
    }

    [Test]
    public void LayoutRepeatsTheFieldsRowsAndStaysOffTheMerchantAndBell()
    {
        List<Vector3> field = BaseField();
        List<Vector3> extra = FarmlandLogic.PositionsFor(4, field);

        HashSet<float> rows = new HashSet<float>();
        foreach (Vector3 p in extra) rows.Add(Mathf.Round(p.z * 10f) / 10f);
        Assert.AreEqual(3, rows.Count, "no new rows: the field only grows sideways");

        Vector3 merchant = new Vector3(0f, 0f, 11f), bell = new Vector3(-6f, 0f, 0f);
        foreach (Vector3 p in extra)
        {
            Assert.Greater(Vector3.Distance(p, merchant), 3f, "plots stay clear of the merchant's booth");
            Assert.Greater(Vector3.Distance(p, bell), 2.5f, "and the bell");
        }
    }

    [Test]
    public void ManyFarmlandsFillOutSideColumnsThenStartNewRowsBehindTheField()
    {
        List<Vector3> field = TwoByTwoField();
        List<Vector3> extra = FarmlandLogic.PositionsFor(14, field, maxColumnsPerSide: 4);
        Assert.AreEqual(14 * 2, extra.Count, "every farmland adds one column of 2 plots");

        List<Vector3> all = new List<Vector3>(field);
        all.AddRange(extra);
        for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
                Assert.GreaterOrEqual(Vector3.Distance(all[i], all[j]), 2.1f, $"plots {i} and {j} overlap");

        float maxSideX = 0f;
        for (int i = 0; i < 16; i++) maxSideX = Mathf.Max(maxSideX, Mathf.Abs(extra[i].x));
        Assert.AreEqual(9.9f, maxSideX, 1e-3f, "the first 8 columns sit beside the field (4 per side)");
        Assert.Greater(extra[16].z, 5.2f + 0.1f, "the 9th column starts a new band of rows behind the field");
    }

    [Test]
    public void ColumnsThatWouldLandOnTheMerchantOrBellAreSkippedNotLost()
    {
        List<Vector3> field = TwoByTwoField();
        List<Vector3> keepClear = new List<Vector3> { new Vector3(0f, 0f, 9.6f), new Vector3(-6f, 0f, 0f) };
        List<Vector3> extra = FarmlandLogic.PositionsFor(30, field, 4, keepClear);

        Assert.AreEqual(30 * 2, extra.Count, "the full number of columns is still delivered");
        foreach (Vector3 p in extra)
        {
            Assert.GreaterOrEqual(Vector3.Distance(new Vector3(p.x, 0f, p.z), keepClear[0]), 2.8f);
            Assert.GreaterOrEqual(Vector3.Distance(new Vector3(p.x, 0f, p.z), keepClear[1]), 2.8f);
        }
    }

    private static List<Vector3> TwoByTwoField()
    {
        List<Vector3> list = new List<Vector3>();
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 2; c++) list.Add(new Vector3(-1.1f + c * 2.2f, 0.1f, 3f + r * 2.2f));
        return list;
    }

    [Test]
    public void LayoutIsDeterministicAndSafeWithNoField()
    {
        List<Vector3> field = BaseField();
        CollectionAssert.AreEqual(FarmlandLogic.PositionsFor(3, field), FarmlandLogic.PositionsFor(3, field));
        Assert.IsEmpty(FarmlandLogic.PositionsFor(0, field));
        Assert.IsEmpty(FarmlandLogic.PositionsFor(3, new List<Vector3>()));
        Assert.IsEmpty(FarmlandLogic.PositionsFor(3, null));
        Assert.AreEqual(0, FarmlandLogic.PlotsPerFarmland(null));
    }

    // ------------------------------------------------------------------ spawning

    private FarmTile MakeTile(Vector3 position, Transform parent = null)
    {
        GameObject go = Track(new GameObject("Tile"));
        go.transform.position = position;
        if (parent != null) go.transform.SetParent(parent, true);
        return go.AddComponent<FarmTile>();
    }

    [Test]
    public void SpawnerBuildsConfiguredPlotsNextToTheExistingField()
    {
        Transform parent = Track(new GameObject("FarmTiles")).transform;
        List<FarmTile> existing = new List<FarmTile>();
        foreach (Vector3 p in BaseField()) existing.Add(MakeTile(p, parent));
        FarmConditions conditions = Track(new GameObject("Conditions")).AddComponent<FarmConditions>();

        List<FarmTile> spawned = FarmlandSpawner.Spawn(2, existing[0], existing, parent, conditions);
        foreach (FarmTile t in spawned) created.Add(t.gameObject);

        Assert.AreEqual(6, spawned.Count, "two farmlands x three plots");
        foreach (FarmTile t in spawned)
        {
            Assert.AreSame(parent, t.transform.parent);
            Assert.AreEqual(FarmTile.TileState.Empty, t.State);
            Assert.That(t.name, Does.StartWith("FarmTile_Extra_"));
        }
        Assert.AreEqual(existing.Count + 6, parent.GetComponentsInChildren<FarmTile>().Length);
    }

    [Test]
    public void SpawnerDoesNothingWithoutFarmlandsOrAField()
    {
        Transform parent = Track(new GameObject("FarmTiles")).transform;
        FarmTile tile = MakeTile(Vector3.zero, parent);
        List<FarmTile> field = new List<FarmTile> { tile };

        Assert.IsEmpty(FarmlandSpawner.Spawn(0, tile, field, parent, null));
        Assert.IsEmpty(FarmlandSpawner.Spawn(2, null, field, parent, null));
        Assert.IsEmpty(FarmlandSpawner.Spawn(2, tile, new List<FarmTile>(), parent, null));
    }

    [Test]
    public void NewPlotsPlantSeedsLikeTheOriginals()
    {
        Transform parent = Track(new GameObject("FarmTiles")).transform;
        List<FarmTile> existing = new List<FarmTile>();
        foreach (Vector3 p in BaseField()) existing.Add(MakeTile(p, parent));
        existing[0].SetFreePlanting(true); // the template starts out in legacy mode

        List<FarmTile> spawned = FarmlandSpawner.Spawn(1, existing[0], existing, parent, null);
        foreach (FarmTile t in spawned) created.Add(t.gameObject);

        // A plot with no seed in hand can't be planted for free.
        GameObject player = Track(new GameObject("Player"));
        player.AddComponent<PlayerInventory>();
        spawned[0].Interact(player);
        Assert.AreEqual(FarmTile.TileState.Empty, spawned[0].State, "extra plots need seeds, same as the rest of the field");
    }
}
