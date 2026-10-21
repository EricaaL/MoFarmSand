using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ClimateAndDisasterTests
{
    private readonly List<Object> created = new List<Object>();

    private T Track<T>(T o) where T : Object { created.Add(o); return o; }

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    // ------------------------------------------------------------------ calendar and climate

    [Test]
    public void CalendarMapsDaysToSeasonsAtBoundaries()
    {
        SeasonData spring = Track(TestUtil.Season("spring")), summer = Track(TestUtil.Season("summer"));
        SeasonCalendar cal = new SeasonCalendar(Track(TestUtil.Calendar(spring, summer)));

        Assert.AreEqual(6, cal.TotalDays);
        Assert.AreSame(spring, cal.DateOf(0).season);
        CalendarDate lastSpring = cal.DateOf(2);
        Assert.AreSame(spring, lastSpring.season);
        Assert.IsTrue(lastSpring.IsLastDayOfSeason);
        Assert.AreSame(summer, cal.DateOf(3).season);
        Assert.AreEqual(0, cal.DateOf(3).dayInSeason);
        Assert.AreSame(summer, cal.DateOf(99).season, "out-of-range days clamp to the last day");
        Assert.IsTrue(cal.IsLastDayOfYear(5));
    }

    private static CropData CropWith(float idealTemp, float tol, float idealMoist, float moistTol, SeasonData season, float affinity)
    {
        CropData c = TestUtil.Make<CropData>();
        c.climate = new CropClimate
        {
            idealTemp = idealTemp, tempTolerance = tol, idealMoisture = idealMoist, moistureTolerance = moistTol,
            seasonAffinity = new List<SeasonAffinity> { new SeasonAffinity { season = season, multiplier = affinity } }
        };
        return c;
    }

    [Test]
    public void GrowthPeaksAtIdealAndFallsOffMonotonically()
    {
        SeasonData spring = Track(TestUtil.Season("spring"));
        ClimateConfig cfg = Track(TestUtil.Make<ClimateConfig>());
        cfg.minGrowth = 0.05f; cfg.maxGrowth = 3f;
        CropData crop = Track(CropWith(20f, 10f, 0.4f, 0.5f, spring, 1f));
        CalendarDate date = new SeasonCalendar(Track(TestUtil.Calendar(spring))).DateOf(0);

        float ideal = ClimateModel.GrowthMultiplier(crop, date, new DayWeather { temperature = 20f, precipitation = 0.4f }, cfg);
        float off1 = ClimateModel.GrowthMultiplier(crop, date, new DayWeather { temperature = 25f, precipitation = 0.4f }, cfg);
        float off2 = ClimateModel.GrowthMultiplier(crop, date, new DayWeather { temperature = 32f, precipitation = 0.4f }, cfg);

        Assert.AreEqual(1f, ideal, 1e-4f);
        Assert.Less(off1, ideal);
        Assert.Less(off2, off1);
    }

    [Test]
    public void GrowthIsClampedAndUsesSeasonAffinity()
    {
        SeasonData spring = Track(TestUtil.Season("spring"));
        ClimateConfig cfg = Track(TestUtil.Make<ClimateConfig>());
        cfg.minGrowth = 0.35f; cfg.maxGrowth = 1.6f;
        CalendarDate date = new SeasonCalendar(Track(TestUtil.Calendar(spring))).DateOf(0);
        DayWeather ideal = new DayWeather { temperature = 20f, precipitation = 0.4f };

        CropData boosted = Track(CropWith(20f, 10f, 0.4f, 0.5f, spring, 5f));
        Assert.AreEqual(1.6f, ClimateModel.GrowthMultiplier(boosted, date, ideal, cfg), 1e-4f, "max clamp");

        CropData terrible = Track(CropWith(-30f, 2f, 0.4f, 0.5f, spring, 1f));
        Assert.AreEqual(0.35f, ClimateModel.GrowthMultiplier(terrible, date, ideal, cfg), 1e-4f, "min clamp");

        // A 0.5 affinity is squared by the wrong-season penalty (exponent 2), so it grows at a quarter speed, not half.
        cfg.minGrowth = 0.05f; // lower the floor so the penalty itself is visible
        CropData half = Track(CropWith(20f, 10f, 0.4f, 0.5f, spring, 0.5f));
        Assert.AreEqual(0.25f, ClimateModel.GrowthMultiplier(half, date, ideal, cfg), 1e-4f);
        cfg.seasonPenaltyExponent = 1f;
        Assert.AreEqual(0.5f, ClimateModel.GrowthMultiplier(half, date, ideal, cfg), 1e-4f, "exponent 1 restores the old linear penalty");
    }

    [Test]
    public void SampleDayIsDeterministicAndPicksWeatherKindFromRules()
    {
        SeasonData winter = Track(TestUtil.Season("winter"));
        winter.temperatureCurve = AnimationCurve.Linear(0f, -5f, 1f, -5f);
        winter.temperatureVariance = 0f;
        winter.precipitationCurve = AnimationCurve.Linear(0f, 0.6f, 1f, 0.6f);
        winter.precipitationVariance = 0f;
        ClimateConfig cfg = Track(TestUtil.Make<ClimateConfig>());
        cfg.fallbackKind = WeatherKind.Clear;
        cfg.rules = new List<ClimateConfig.WeatherKindRule>
        {
            new ClimateConfig.WeatherKindRule { kind = WeatherKind.Snow, minTemp = -100f, maxTemp = 1f, minPrecip = 0.3f, maxPrecip = 1f },
            new ClimateConfig.WeatherKindRule { kind = WeatherKind.Rain, minTemp = -100f, maxTemp = 100f, minPrecip = 0.5f, maxPrecip = 1f },
        };
        CalendarDate date = new SeasonCalendar(Track(TestUtil.Calendar(winter))).DateOf(1);

        DayWeather a = ClimateModel.SampleDay(date, cfg, new SeededRng(5));
        DayWeather b = ClimateModel.SampleDay(date, cfg, new SeededRng(5));
        Assert.AreEqual(a.temperature, b.temperature);
        Assert.AreEqual(WeatherKind.Snow, a.kind, "first matching rule wins");
    }

    // ------------------------------------------------------------------ disaster roller

    private DisasterData MakeDisaster(string id, SeasonData season, float weight, int minDay = 0)
    {
        DisasterData d = Track(TestUtil.Make<DisasterData>());
        d.disasterId = id;
        d.seasonWeights = new List<DisasterData.SeasonWeight> { new DisasterData.SeasonWeight { season = season, weight = weight } };
        d.minDay = minDay;
        d.severityRange = new Vector2(0.4f, 1f);
        return d;
    }

    private DisasterRegistry MakeRegistry(params DisasterData[] items)
    {
        DisasterRegistry r = Track(TestUtil.Make<DisasterRegistry>());
        foreach (DisasterData d in items) r.Ensure(d);
        return r;
    }

    [Test]
    public void RollerIsDeterministicAndRespectsSeasonAndMinDay()
    {
        SeasonData summer = Track(TestUtil.Season("summer")), winter = Track(TestUtil.Season("winter"));
        DisasterData frost = MakeDisaster("frost", winter, 1f);
        DisasterData drought = MakeDisaster("drought", summer, 1f, minDay: 2);
        DisasterRegistry reg = MakeRegistry(frost, drought);
        SeasonCalendar cal = new SeasonCalendar(Track(TestUtil.Calendar(summer, winter)));

        DisasterRoll a = DisasterRoller.Roll(cal.DateOf(4), 1f, reg, new SeededRng(11));
        DisasterRoll b = DisasterRoller.Roll(cal.DateOf(4), 1f, reg, new SeededRng(11));
        Assert.AreEqual(a.disasterId, b.disasterId);
        Assert.AreEqual(a.severity01, b.severity01);

        // Day 1 is summer but earlier than drought's minDay (2), and frost never rolls in summer.
        for (int seed = 0; seed < 300; seed++)
            Assert.IsTrue(DisasterRoller.Roll(cal.DateOf(1), 1f, reg, new SeededRng(seed)).IsNone);

        // Day 2 is the last summer day and past drought's minDay: only drought can roll, never frost.
        for (int seed = 0; seed < 300; seed++)
        {
            DisasterRoll r = DisasterRoller.Roll(cal.DateOf(2), 1f, reg, new SeededRng(seed));
            Assert.AreEqual("drought", r.disasterId);
        }
        Assert.AreEqual("frost", a.disasterId, "day 4 is winter");
    }

    [Test]
    public void DisasterNodeAlwaysRollsAndZeroChanceNever()
    {
        SeasonData summer = Track(TestUtil.Season("summer"));
        DisasterRegistry reg = MakeRegistry(MakeDisaster("drought", summer, 1f));
        CalendarDate date = new SeasonCalendar(Track(TestUtil.Calendar(summer))).DateOf(0);

        for (int seed = 0; seed < 200; seed++)
        {
            Assert.IsFalse(DisasterRoller.Roll(date, 1f, reg, new SeededRng(seed)).IsNone, "chance 1 is forced");
            Assert.IsTrue(DisasterRoller.Roll(date, 0f, reg, new SeededRng(seed)).IsNone, "chance 0 never rolls");
        }
    }

    [Test]
    public void ResistanceLowersSeverityAndSeverityStaysInRange()
    {
        SeasonData summer = Track(TestUtil.Season("summer"));
        DisasterRegistry reg = MakeRegistry(MakeDisaster("drought", summer, 1f));
        CalendarDate date = new SeasonCalendar(Track(TestUtil.Calendar(summer))).DateOf(0);

        for (int seed = 0; seed < 200; seed++)
        {
            DisasterRoll plain = DisasterRoller.Roll(date, 1f, reg, new SeededRng(seed));
            DisasterRoll resisted = DisasterRoller.Roll(date, 1f, reg, new SeededRng(seed), id => 0.5f);
            Assert.That(plain.severity01, Is.InRange(0.4f, 1f));
            Assert.AreEqual(plain.severity01 * 0.5f, resisted.severity01, 1e-4f);
        }
    }

    // ------------------------------------------------------------------ effects against a fake world

    private sealed class FakePlot : IFarmPlot
    {
        public bool IsGrowing { get; set; }
        public Vector3 Position { get; set; }
        public int withered;
        public float blocked;
        public void Wither() { withered++; IsGrowing = false; }
        public void Block(float seconds) { blocked = seconds; }
    }

    private sealed class FakeWorld : IFarmWorld
    {
        public readonly List<FakePlot> plots = new List<FakePlot>();
        public IReadOnlyList<IFarmPlot> Plots => plots.ConvertAll(p => (IFarmPlot)p);
        public ModifierStack Growth { get; } = new ModifierStack();
        public ModifierStack SellPrice { get; } = new ModifierStack();
        public ModifierStack SeedPrice { get; } = new ModifierStack();
        public Vector3 PlayerPosition { get; set; }
        public SeededRng Rng { get; } = new SeededRng(3);
        public int damage;
        public float shake;
        public float speed = 1f;
        public int fxCount;
        public void DamagePlayer(int amount) { damage += amount; }
        public void SetPlayerSpeed(string source, float multiplier) { speed = multiplier; }
        public void SpawnFx(GameObject prefab, Vector3 position, float lifetime) { fxCount++; }
        public void Shake(float strength) { shake += strength; }
    }

    [Test]
    public void GrowthEffectAddsModifierOnBeginAndRemovesOnEnd()
    {
        GrowthMultiplierEffect fx = Track(TestUtil.Make<GrowthMultiplierEffect>());
        fx.multiplierAtFullSeverity = 0.5f;
        FakeWorld world = new FakeWorld();

        DisasterEffectRuntime rt = fx.CreateRuntime(1f);
        rt.Begin(world);
        Assert.AreEqual(0.5f, world.Growth.Product, 1e-5f);
        rt.End(world);
        Assert.AreEqual(1f, world.Growth.Product, 1e-5f);

        DisasterEffectRuntime mild = fx.CreateRuntime(0.5f);
        mild.Begin(world);
        Assert.AreEqual(0.75f, world.Growth.Product, 1e-5f, "half severity is halfway to the full multiplier");
    }

    [Test]
    public void WitherEffectOnlyHitsGrowingPlotsAndRespectsTheCap()
    {
        WitherPlotsEffect fx = Track(TestUtil.Make<WitherPlotsEffect>());
        fx.firstDelay = 0f; fx.interval = 100f; fx.maxPerTick = 2;
        FakeWorld world = new FakeWorld();
        for (int i = 0; i < 4; i++) world.plots.Add(new FakePlot { IsGrowing = i != 3 });

        DisasterEffectRuntime rt = fx.CreateRuntime(1f);
        rt.Tick(world, 0.1f);

        int total = 0;
        foreach (FakePlot p in world.plots) total += p.withered;
        Assert.AreEqual(2, total);
        Assert.AreEqual(0, world.plots[3].withered, "empty plots are untouched");
    }

    [Test]
    public void StrikeDamagesOnlyInsideRadiusAfterTheTelegraph()
    {
        StrikeHazardEffect fx = Track(TestUtil.Make<StrikeHazardEffect>());
        fx.firstDelay = 0f; fx.interval = 100f; fx.telegraphSeconds = 1f; fx.spawnRadius = 0f; fx.radius = 1.5f; fx.damage = 1;
        FakeWorld world = new FakeWorld { PlayerPosition = new Vector3(2f, 0f, 2f) };

        DisasterEffectRuntime rt = fx.CreateRuntime(1f);
        rt.Tick(world, 0.1f);
        Assert.AreEqual(0, world.damage, "telegraph first, no instant damage");
        rt.Tick(world, 1.5f);
        Assert.AreEqual(1, world.damage, "strike lands on the player's position");
        Assert.Greater(world.shake, 0f);

        // Player moved away before a second strike lands: no damage.
        FakeWorld dodged = new FakeWorld { PlayerPosition = Vector3.zero };
        StrikeHazardEffect fx2 = Track(TestUtil.Make<StrikeHazardEffect>());
        fx2.firstDelay = 0f; fx2.interval = 100f; fx2.telegraphSeconds = 1f; fx2.spawnRadius = 0f; fx2.radius = 1.5f;
        DisasterEffectRuntime rt2 = fx2.CreateRuntime(1f);
        rt2.Tick(dodged, 0.1f);
        dodged.PlayerPosition = new Vector3(10f, 0f, 0f);
        rt2.Tick(dodged, 1.5f);
        Assert.AreEqual(0, dodged.damage);
    }

    [Test]
    public void BlockEffectBlocksPlotsOnBegin()
    {
        BlockPlotsEffect fx = Track(TestUtil.Make<BlockPlotsEffect>());
        fx.fractionAtFullSeverity = 0.5f; fx.seconds = 20f;
        FakeWorld world = new FakeWorld();
        for (int i = 0; i < 4; i++) world.plots.Add(new FakePlot());

        fx.CreateRuntime(1f).Begin(world);

        int blocked = 0;
        foreach (FakePlot p in world.plots) if (p.blocked > 0f) blocked++;
        Assert.AreEqual(2, blocked);
    }

    [Test]
    public void PriceShockAndSlowAreAppliedAndRemoved()
    {
        PriceShockEffect shock = Track(TestUtil.Make<PriceShockEffect>());
        shock.sellMultiplier = 1.5f; shock.seedMultiplier = 1.3f;
        PlayerSlowEffect slow = Track(TestUtil.Make<PlayerSlowEffect>());
        slow.speedAtFullSeverity = 0.7f;
        FakeWorld world = new FakeWorld();

        DisasterEffectRuntime s = shock.CreateRuntime(1f);
        s.Begin(world);
        Assert.AreEqual(1.5f, world.SellPrice.Product, 1e-5f);
        Assert.AreEqual(1.3f, world.SeedPrice.Product, 1e-5f);
        s.End(world);
        Assert.AreEqual(1f, world.SellPrice.Product, 1e-5f);

        DisasterEffectRuntime sl = slow.CreateRuntime(1f);
        sl.Begin(world);
        Assert.AreEqual(0.7f, world.speed, 1e-5f);
        sl.End(world);
        Assert.AreEqual(1f, world.speed, 1e-5f);
    }
}
