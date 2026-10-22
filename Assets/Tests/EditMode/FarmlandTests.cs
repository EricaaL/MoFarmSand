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

    // ------------------------------------------------------------------ placing

    private static List<Vector3> TwoByTwoField()
    {
        List<Vector3> list = new List<Vector3>();
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 2; c++) list.Add(new Vector3(-1.1f + c * 2.2f, 0.1f, 3f + r * 2.2f));
        return list;
    }

    [Test]
    public void EachFarmlandIsAColumnOfPlotsToPlace()
    {
        Assert.AreEqual(3, FarmlandLogic.PlotsPerFarmland(BaseField()), "a farmland gives as many plots as the field is tall");
        Assert.AreEqual(2, FarmlandLogic.PlotsPerFarmland(TwoByTwoField()));
        Assert.AreEqual(0, FarmlandLogic.PlotsPerFarmland(null));
    }

    [Test]
    public void UnplacedPlotsAreOwnedMinusPlacedAndNeverNegative()
    {
        Assert.AreEqual(4, FarmlandLogic.UnplacedPlots(2, 2, 0));
        Assert.AreEqual(1, FarmlandLogic.UnplacedPlots(2, 2, 3));
        Assert.AreEqual(0, FarmlandLogic.UnplacedPlots(2, 2, 4));
        Assert.AreEqual(0, FarmlandLogic.UnplacedPlots(1, 2, 9), "more placed than owned reads as none waiting");
        Assert.AreEqual(0, FarmlandLogic.UnplacedPlots(0, 2, 0));
    }

    [Test]
    public void SnapPutsPlotsOnTheFieldsGrid()
    {
        List<Vector3> field = TwoByTwoField();
        Vector3 snapped = FarmlandLogic.SnapToField(new Vector3(4.6f, 5f, 8.1f), field);
        Assert.AreEqual(-1.1f + 2.2f * 3f, snapped.x, 1e-3f);
        Assert.AreEqual(3f + 2.2f * 2f, snapped.z, 1e-3f);
        Assert.AreEqual(0.1f, snapped.y, 1e-4f, "keeps the field's ground height");

        Vector3 loose = new Vector3(1f, 2f, 3f);
        Assert.AreEqual(loose, FarmlandLogic.SnapToField(loose, null), "no field: left alone");
    }

    [Test]
    public void ClearMeansNothingCloserThanTheSpacing()
    {
        List<Vector3> field = TwoByTwoField();
        Assert.IsFalse(FarmlandLogic.IsClear(field[0], field, 1.8f), "on top of a plot");
        Assert.IsFalse(FarmlandLogic.IsClear(field[0] + new Vector3(1f, 0f, 0f), field, 1.8f));
        Assert.IsTrue(FarmlandLogic.IsClear(field[0] + new Vector3(-2.2f, 0f, 0f), field, 1.8f), "one grid step away fits");
        Assert.IsTrue(FarmlandLogic.IsClear(Vector3.zero, null, 1.8f));
    }

    [Test]
    public void PlacingIsLimitedToThePlotsYouOwn()
    {
        MetaProgress meta = new MetaProgress();
        Assert.IsFalse(FarmlandLogic.TryPlace(meta, Vector3.zero, 0), "nothing owned yet");
        Assert.IsTrue(FarmlandLogic.TryPlace(meta, new Vector3(1f, 0.1f, 1f), 2));
        Assert.IsTrue(FarmlandLogic.TryPlace(meta, new Vector3(5f, 0.1f, 1f), 2));
        Assert.IsFalse(FarmlandLogic.TryPlace(meta, new Vector3(9f, 0.1f, 1f), 2), "both owned plots are already down");
        Assert.AreEqual(2, meta.placedPlots.Count);
    }

    [Test]
    public void PickingUpForgetsOnlyThePlotThatWasThere()
    {
        MetaProgress meta = new MetaProgress();
        Vector3 a = new Vector3(1f, 0.1f, 1f), b = new Vector3(5f, 0.1f, 1f);
        FarmlandLogic.TryPlace(meta, a, 5);
        FarmlandLogic.TryPlace(meta, b, 5);

        Assert.IsFalse(FarmlandLogic.TryPickUp(meta, new Vector3(3f, 0.1f, 1f)), "nothing placed there");
        Assert.IsTrue(FarmlandLogic.TryPickUp(meta, a));
        Assert.AreEqual(1, meta.placedPlots.Count);
        Assert.AreEqual(b, meta.placedPlots[0]);
        Assert.IsFalse(FarmlandLogic.TryPickUp(null, a));
    }

    [Test]
    public void PlacedPlotsSurviveSavingAndSanitizeDropsBrokenOnes()
    {
        SaveFile file = new SaveFile { hasRun = true, run = RunLogic.NewRun(3, db, new MetaProgress()) };
        file.meta.placedPlots.Add(new Vector3(4.4f, 0.1f, 3f));
        file.meta.placedPlots.Add(new Vector3(float.NaN, 0.1f, 3f));

        SaveFile back = JsonUtility.FromJson<SaveFile>(JsonUtility.ToJson(file));
        Assert.AreEqual(2, back.meta.placedPlots.Count);
        Assert.AreEqual(4.4f, back.meta.placedPlots[0].x, 1e-3f);

        SaveService.Sanitize(file, db);
        Assert.AreEqual(1, file.meta.placedPlots.Count, "the NaN plot is dropped");
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
    public void SpawnerSetsAPlacedPlotDownWhereAsked()
    {
        Transform parent = Track(new GameObject("FarmTiles")).transform;
        FarmTile template = MakeTile(Vector3.zero, parent);
        FarmConditions conditions = Track(new GameObject("Conditions")).AddComponent<FarmConditions>();
        Vector3 spot = new Vector3(8.8f, 0.1f, 3f);

        FarmTile tile = FarmlandSpawner.SpawnAt(spot, template, parent, conditions);
        created.Add(tile.gameObject);

        Assert.AreEqual(spot, tile.transform.position);
        Assert.AreSame(parent, tile.transform.parent);
        Assert.IsTrue(tile.IsPlaced);
        Assert.IsFalse(template.IsPlaced, "the starting field is never a placed plot");
        Assert.AreEqual(FarmTile.TileState.Empty, tile.State);
        Assert.IsTrue(tile.CanPickUp, "an empty placed plot can be picked up");
        Assert.IsFalse(template.CanPickUp, "the starting field stays put");
    }

    [Test]
    public void SpawnerDoesNothingWithoutATemplate()
    {
        Transform parent = Track(new GameObject("FarmTiles")).transform;
        Assert.IsNull(FarmlandSpawner.SpawnAt(Vector3.zero, null, parent, null));
    }

    [Test]
    public void PlacedPlotsPlantSeedsLikeTheOriginals()
    {
        Transform parent = Track(new GameObject("FarmTiles")).transform;
        FarmTile template = MakeTile(Vector3.zero, parent);
        template.SetFreePlanting(true); // the template starts out in legacy mode

        FarmTile tile = FarmlandSpawner.SpawnAt(new Vector3(4.4f, 0.1f, 3f), template, parent, null);
        created.Add(tile.gameObject);

        // A plot with no seed in hand can't be planted for free.
        GameObject player = Track(new GameObject("Player"));
        player.AddComponent<PlayerInventory>();
        tile.Interact(player);
        Assert.AreEqual(FarmTile.TileState.Empty, tile.State, "placed plots need seeds, same as the rest of the field");
    }
}
