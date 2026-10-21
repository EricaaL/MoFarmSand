using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class QualityAndBossDayTests
{
    private GameDatabase db;
    private readonly List<Object> created = new List<Object>();

    private T Track<T>(T o) where T : Object { created.Add(o); return o; }

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    [TearDown]
    public void TearDown()
    {
        typeof(GameManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SetValue(null, null);
        foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    private CropData Crop(string id) => (CropData)db.items.Get(id);
    private SeasonData Season(string id) => db.calendar.seasons.Find(s => s.seasonId == id);

    private static void SetField(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);

    private (float growth, float quality) Typical(CropData crop, string seasonId)
    {
        SeasonData s = Season(seasonId);
        CalendarDate d = new CalendarDate { season = s, seasonProgress01 = 0.5f };
        DayWeather w = new DayWeather { temperature = s.temperatureCurve.Evaluate(0.5f), precipitation = s.precipitationCurve.Evaluate(0.5f) };
        return (ClimateModel.GrowthMultiplier(crop, d, w, db.climate), ClimateModel.Quality(crop, d, w, db.climate));
    }

    // ------------------------------------------------------------------ wrong-season penalty

    [Test]
    public void PlantingInTheWrongSeasonIsMuchSlowerAndWeaker()
    {
        CropData tomato = Crop("tomato");
        var best = Typical(tomato, "summer");
        var wrong = Typical(tomato, "winter");

        float bestSeconds = tomato.growTime / best.growth, wrongSeconds = tomato.growTime / wrong.growth;
        Assert.GreaterOrEqual(wrongSeconds, bestSeconds * 5f, $"winter tomato should take 5x+ longer ({wrongSeconds:0}s vs {bestSeconds:0}s)");
        Assert.LessOrEqual(wrong.quality, 0.3f, "and be at most 30% as strong");
        Assert.GreaterOrEqual(best.quality, 1f, "best season is at least full strength");
    }

    [Test]
    public void EveryCropIsClearlyWorseInItsWorstSeasonThanItsBest()
    {
        foreach (ItemData item in db.items.All)
        {
            if (!(item is CropData crop)) continue;
            float bestQ = 0f, worstQ = float.MaxValue, bestG = 0f, worstG = float.MaxValue;
            foreach (SeasonData s in db.calendar.seasons)
            {
                var t = Typical(crop, s.seasonId);
                bestQ = Mathf.Max(bestQ, t.quality); worstQ = Mathf.Min(worstQ, t.quality);
                bestG = Mathf.Max(bestG, t.growth); worstG = Mathf.Min(worstG, t.growth);
            }
            Assert.LessOrEqual(worstQ, bestQ * 0.5f, $"{crop.Id}: worst-season strength should be at most half of the best");
            Assert.LessOrEqual(worstG, bestG * 0.35f, $"{crop.Id}: worst-season growth should be at most ~1/3 of the best");
        }
    }

    [Test]
    public void StrongerPenaltyExponentMakesOffSeasonWorse()
    {
        ClimateConfig linear = Track(Object.Instantiate(db.climate));
        linear.seasonPenaltyExponent = 1f;
        CropData tomato = Crop("tomato");
        SeasonData s = Season("autumn");
        CalendarDate d = new CalendarDate { season = s, seasonProgress01 = 0.5f };
        DayWeather w = new DayWeather { temperature = 20f, precipitation = 0.4f };

        Assert.Less(ClimateModel.GrowthMultiplier(tomato, d, w, db.climate), ClimateModel.GrowthMultiplier(tomato, d, w, linear));
    }

    [Test]
    public void QualityStaysWithinTheConfiguredBounds()
    {
        foreach (ItemData item in db.items.All)
        {
            if (!(item is CropData crop)) continue;
            foreach (SeasonData s in db.calendar.seasons)
            {
                float q = Typical(crop, s.seasonId).quality;
                Assert.That(q, Is.InRange(db.climate.minQuality - 1e-5f, db.climate.maxQuality + 1e-5f), $"{crop.Id} in {s.displayName}");
            }
        }
    }

    [Test]
    public void ShieldChargesScaleWithQualityButNeverBelowOne()
    {
        CropData turnip = Crop("turnip"), spinach = Crop("spinach");

        Assert.AreEqual(2, ClimateModel.ShieldCharges(turnip, 1f));
        Assert.AreEqual(1, ClimateModel.ShieldCharges(turnip, 0.4f));
        Assert.AreEqual(3, ClimateModel.ShieldCharges(spinach, 1f));
        Assert.AreEqual(2, ClimateModel.ShieldCharges(spinach, 0.5f));
        Assert.AreEqual(1, ClimateModel.ShieldCharges(spinach, 0.25f));
        Assert.AreEqual(1, ClimateModel.ShieldCharges(spinach, 0f), "an eaten crop always gives some shield");
    }

    // ------------------------------------------------------------------ quality is carried by the harvested crops

    private PlayerInventory NewInventory(out GameObject go)
    {
        go = Track(new GameObject("Player"));
        return go.AddComponent<PlayerInventory>();
    }

    [Test]
    public void StackingBlendsQualityByCount()
    {
        CropData tomato = Crop("tomato");
        PlayerInventory inv = NewInventory(out _);

        inv.AddItem(tomato, 2, 1f);
        inv.AddItem(tomato, 2, 0.5f);

        Assert.AreEqual(4, inv.CountOf(tomato));
        Assert.AreEqual(0.75f, inv.QualityOf(tomato), 1e-5f);
        Assert.AreEqual(0.75f, inv.GetSlot(0).EffectiveQuality, 1e-5f);
    }

    [Test]
    public void EmptiedSlotDoesNotKeepTheOldQuality()
    {
        CropData tomato = Crop("tomato");
        PlayerInventory inv = NewInventory(out _);
        inv.AddItem(tomato, 1, 0.3f);
        Assert.IsTrue(inv.TryRemove(tomato, 1));

        inv.AddItem(tomato, 1, 1f);

        Assert.AreEqual(1f, inv.QualityOf(tomato), 1e-5f, "a fresh crop in an emptied slot isn't dragged down by the old stack");
    }

    [Test]
    public void QualitySurvivesSnapshotRestoreAndOldSavesReadAsFullStrength()
    {
        CropData tomato = Crop("tomato");
        PlayerInventory a = NewInventory(out _);
        a.AddItem(tomato, 3, 0.4f);
        List<SlotSave> snap = a.Snapshot();

        PlayerInventory b = NewInventory(out _);
        b.Restore(snap, 0, db.items);
        Assert.AreEqual(0.4f, b.QualityOf(tomato), 1e-5f);

        // A save from before quality existed has quality 0 in every slot.
        List<SlotSave> old = new List<SlotSave> { new SlotSave("tomato", 2, 0f) };
        PlayerInventory c = NewInventory(out _);
        c.Restore(old, 0, db.items);
        Assert.AreEqual(1f, c.QualityOf(tomato), 1e-5f);
    }

    [Test]
    public void JsonRoundTripKeepsSlotQuality()
    {
        SlotSave s = new SlotSave("tomato", 2, 0.6f);
        SlotSave back = JsonUtility.FromJson<SlotSave>(JsonUtility.ToJson(s));
        Assert.AreEqual(0.6f, back.quality, 1e-5f);
    }

    private FarmConditions Conditions(string seasonId)
    {
        FarmConditions cond = Track(new GameObject("Conditions")).AddComponent<FarmConditions>();
        SetField(cond, "climate", db.climate);
        SeasonData s = Season(seasonId);
        cond.Configure(new CalendarDate { season = s, seasonProgress01 = 0.5f },
            new DayWeather { temperature = s.temperatureCurve.Evaluate(0.5f), precipitation = s.precipitationCurve.Evaluate(0.5f) });
        return cond;
    }

    private static void RunUpdate(FarmTile tile)
        => typeof(FarmTile).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tile, null);

    [Test]
    public void CropPlantedOutOfSeasonIsHarvestedWeak()
    {
        CropData tomato = Crop("tomato");
        SeedData seed = (SeedData)db.items.Get("seed_tomato");
        PlayerInventory inv = NewInventory(out GameObject player);
        inv.AddItem(seed, 2);

        FarmTile tile = Track(new GameObject("Tile")).AddComponent<FarmTile>();
        tile.SetFreePlanting(false);
        tile.SetConditions(Conditions("winter"));

        tile.Interact(player);
        Assert.AreEqual(FarmTile.TileState.Growing, tile.State);
        Assert.Less(tile.Quality, 0.35f, "winter tomatoes are weak");
        Assert.That(tile.Prompt, Does.Contain("weak crop"));

        SetField(tile, "timer", 0f);
        RunUpdate(tile);
        tile.Interact(player);

        Assert.AreEqual(tomato.harvestYield, inv.CountOf(tomato));
        Assert.AreEqual(tile.Quality, inv.QualityOf(tomato), 1e-5f, "the harvested stack carries the planting quality");
    }

    [Test]
    public void CropPlantedInSeasonIsFullStrengthAndLegacyTilesAreAlways()
    {
        SeedData seed = (SeedData)db.items.Get("seed_tomato");
        PlayerInventory inv = NewInventory(out GameObject player);
        inv.AddItem(seed, 2);

        FarmTile good = Track(new GameObject("Good")).AddComponent<FarmTile>();
        good.SetFreePlanting(false);
        good.SetConditions(Conditions("summer"));
        good.Interact(player);
        Assert.GreaterOrEqual(good.Quality, 1f);

        // No conditions (the original trailer scene): always full strength.
        FarmTile legacy = Track(new GameObject("Legacy")).AddComponent<FarmTile>();
        legacy.SetFreePlanting(false);
        legacy.Interact(player);
        Assert.AreEqual(1f, legacy.Quality);
    }

    [Test]
    public void PlantPromptWarnsAboutTheWrongSeason()
    {
        SeedData seed = (SeedData)db.items.Get("seed_tomato");
        PlayerInventory inv = NewInventory(out GameObject player);
        inv.AddItem(seed, 2);

        FarmTile tile = Track(new GameObject("Tile")).AddComponent<FarmTile>();
        tile.SetFreePlanting(false);
        SetField(tile, "playerInventory", inv);

        tile.SetConditions(Conditions("winter"));
        Assert.That(tile.Prompt, Does.Contain("wrong season"));

        tile.SetConditions(Conditions("summer"));
        Assert.That(tile.Prompt, Does.Not.Contain("wrong season"));
    }

    // ------------------------------------------------------------------ shield charges

    [Test]
    public void ShieldBlocksOneHitPerChargeAndIsCapped()
    {
        PlayerHealth h = Track(new GameObject("Hero")).AddComponent<PlayerHealth>();
        h.Restore(3, 3);

        h.GrantShield(2);
        Assert.AreEqual(2, h.ShieldCharges);
        h.TakeDamage(1); h.TakeDamage(1);
        Assert.AreEqual(3, h.Hp, "two charges absorbed two hits");
        Assert.IsFalse(h.HasShield);
        h.TakeDamage(1);
        Assert.AreEqual(2, h.Hp);

        h.GrantShield(99);
        Assert.AreEqual(PlayerHealth.MaxShieldCharges, h.ShieldCharges);
    }

    // ------------------------------------------------------------------ boss days skip the daytime

    [Test]
    public void OnlyTheBossNodeStartsAtNightAndNeedsAnEncounter()
    {
        RunState run = RunLogic.NewRun(5, db, new MetaProgress());
        bool sawBoss = false;
        foreach (MapNode n in run.map.nodes)
        {
            DayPlan p = DayPlanner.Plan(run, n.id, db);
            if (p.node.endsRun || p.node.nodeTypeId == "season_boss")
            {
                sawBoss = true;
                Assert.IsTrue(p.startsAtNight, "the boss day skips the daytime");
                Assert.IsNotNull(p.encounter);
            }
            else Assert.IsFalse(p.startsAtNight, $"{p.node.nodeTypeId} keeps its daytime");
        }
        Assert.IsTrue(sawBoss);
    }

    [Test]
    public void StartsAtNightIsIgnoredWithoutAnEncounter()
    {
        NodeTypeData farm = db.nodeTypes.Get("farm");
        bool before = farm.startsAtNight;
        try
        {
            farm.startsAtNight = true;
            RunState run = RunLogic.NewRun(6, db, new MetaProgress());
            MapNode node = run.map.nodes.Find(n => n.nodeTypeId == "farm");
            Assert.IsFalse(DayPlanner.Plan(run, node.id, db).startsAtNight, "no fight means no point starting at night");
        }
        finally { farm.startsAtNight = before; }
    }

    [Test]
    public void StartingTheNightImmediatelyBeginsTheFight()
    {
        GameManager gm = Track(new GameObject("GM")).AddComponent<GameManager>();
        typeof(GameManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SetValue(null, gm);
        Assert.AreEqual(GamePhase.Day, gm.Phase);

        gm.StartNight(instant: true);

        Assert.AreEqual(GamePhase.Night, gm.Phase);
        Assert.IsFalse(gm.CanFarm, "no farming on a night-start day");
    }

    // ------------------------------------------------------------------ descriptions show strength

    [Test]
    public void ItemTabShowsStrengthPerSeasonAndForYourStack()
    {
        CalendarDate date = db.Calendar.DateOf(0);
        ItemInfoContext ctx = new ItemInfoContext
        {
            db = db, date = date, weather = ClimateModel.SampleDay(date, db.climate, new SeededRng(1)),
            owned = 3, ownedQuality = 0.5f
        };

        ItemInfo seed = ItemInfoBuilder.Build(db.items.Get("seed_tomato"), ctx);
        Assert.IsTrue(seed.lines.Exists(l => l.label.StartsWith("Crop strength if planted now")));
        foreach (SeasonData s in db.calendar.seasons)
            Assert.That(seed.lines.Find(l => l.label == "  " + s.displayName).value, Does.Contain("strength"));

        ItemInfo crop = ItemInfoBuilder.Build(db.items.Get("tomato"), ctx);
        InfoLine mine = crop.lines.Find(l => l.label.StartsWith("Your crops' strength"));
        Assert.That(mine.value, Does.StartWith("50%"));
        Assert.That(mine.value, Does.Contain("fullness per hit"));

        ItemInfo turnip = ItemInfoBuilder.Build(db.items.Get("turnip"), ctx);
        Assert.That(turnip.lines.Find(l => l.label.StartsWith("Your crops' strength")).value, Does.Contain("shield charge"));
    }
}
