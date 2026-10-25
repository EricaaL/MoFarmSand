using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GrowTimeTests
{
    private GameDatabase db;
    private readonly List<Object> created = new List<Object>();

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    [Test]
    public void CapRateNeverLetsACropFinishFasterThanTheMinimum()
    {
        ClimateConfig cfg = TestUtil.Make<ClimateConfig>();
        created.Add(cfg);
        cfg.minGrowSeconds = 10f;

        Assert.AreEqual(1.1f, ClimateModel.CapRate(11f, 1.6f, cfg), 1e-4f, "11s at x1.6 would be 6.9s: capped to 10s");
        Assert.AreEqual(0.5f, ClimateModel.CapRate(11f, 0.5f, cfg), 1e-4f, "slow growth is left alone");
        Assert.AreEqual(3f, ClimateModel.CapRate(11f, 3f, null) , 1e-4f, "no config means no cap");
    }

    // ------------------------------------------------------------------ off-season ceiling

    private static readonly DayWeather IdealWeather = new DayWeather { temperature = 20f, precipitation = 0.4f };

    private T Track<T>(T o) where T : Object { created.Add(o); return o; }

    /// <summary>A 28 s crop that is only at home in summer; <paramref name="springAffinity"/> is how it copes with spring.</summary>
    private CropData SummerCrop(SeasonData spring, float springAffinity)
    {
        CropData crop = Track(TestUtil.Make<CropData>());
        crop.growTime = 28f;
        crop.climate = CropClimate.Default;
        crop.climate.seasonAffinity.Add(new SeasonAffinity { season = spring, multiplier = springAffinity });
        return crop;
    }

    private ClimateConfig Climate(float fraction)
    {
        ClimateConfig cfg = Track(TestUtil.Make<ClimateConfig>());
        cfg.offSeasonDayFraction = fraction;
        cfg.minGrowSeconds = 20f;
        return cfg;
    }

    private static float Seconds(CropData crop, float rate, ClimateConfig cfg) => crop.growTime / ClimateModel.CapRate(crop.growTime, rate, cfg);

    [Test]
    public void OffSeasonCropTakesJustUnderTheDaylight()
    {
        SeasonData spring = Track(TestUtil.Season("spring"));
        CropData corn = SummerCrop(spring, 0.4f);
        ClimateConfig cfg = Climate(0.83f);
        CalendarDate date = new CalendarDate { season = spring, seasonProgress01 = 0.5f };

        float raw = ClimateModel.GrowthMultiplier(corn, date, IdealWeather, cfg);
        Assert.Greater(Seconds(corn, raw, cfg), 100f, "without the ceiling a wrong-season crop crawls");

        float rate = ClimateModel.GrowthRate(corn, date, IdealWeather, cfg, 60f);
        Assert.AreEqual(50f, Seconds(corn, rate, cfg), 0.5f, "a 60 s day gives about 50 s");
    }

    [Test]
    public void OffSeasonCropIsStillWeak()
    {
        SeasonData spring = Track(TestUtil.Season("spring"));
        CropData corn = SummerCrop(spring, 0.4f);
        ClimateConfig cfg = Climate(0.83f);
        CalendarDate date = new CalendarDate { season = spring, seasonProgress01 = 0.5f };

        Assert.AreEqual(cfg.minQuality, ClimateModel.Quality(corn, date, IdealWeather, cfg), 1e-4f, "growing sooner does not make it stronger");
    }

    [Test]
    public void InSeasonCropAndCropsWithoutAClockAreLeftAlone()
    {
        SeasonData spring = Track(TestUtil.Season("spring")), summer = Track(TestUtil.Season("summer"));
        CropData corn = SummerCrop(spring, 0.4f);
        ClimateConfig cfg = Climate(0.83f);
        CalendarDate inSeason = new CalendarDate { season = summer, seasonProgress01 = 0.5f };
        CalendarDate offSeason = new CalendarDate { season = spring, seasonProgress01 = 0.5f };

        Assert.AreEqual(ClimateModel.GrowthMultiplier(corn, inSeason, IdealWeather, cfg), ClimateModel.GrowthRate(corn, inSeason, IdealWeather, cfg, 60f), 1e-5f);
        Assert.AreEqual(ClimateModel.GrowthMultiplier(corn, offSeason, IdealWeather, cfg), ClimateModel.GrowthRate(corn, offSeason, IdealWeather, cfg, 0f), 1e-5f, "no clock, no ceiling");
        Assert.AreEqual(ClimateModel.GrowthMultiplier(corn, offSeason, IdealWeather, cfg),
            ClimateModel.GrowthRate(corn, offSeason, IdealWeather, Climate(0f), 60f), 1e-5f, "fraction 0 turns the ceiling off");
    }

    [Test]
    public void CeilingNeverMakesACropFasterThanItsNormalTimeOrSlowsAFastOne()
    {
        SeasonData spring = Track(TestUtil.Season("spring"));
        ClimateConfig cfg = Climate(0.83f);
        CalendarDate date = new CalendarDate { season = spring, seasonProgress01 = 0.5f };

        CropData slow = SummerCrop(spring, 0.4f);
        Assert.AreEqual(1f, ClimateModel.GrowthRate(slow, date, IdealWeather, cfg, 20f), 1e-5f, "a very short day cannot beat the normal time");

        CropData mild = SummerCrop(spring, 0.9f); // 0.81 speed: 34.6 s, already under 50 s
        Assert.AreEqual(ClimateModel.GrowthMultiplier(mild, date, IdealWeather, cfg), ClimateModel.GrowthRate(mild, date, IdealWeather, cfg, 60f), 1e-5f);
    }

    [Test]
    public void EveryRealOffSeasonCropFinishesWithinTheDaylightWhenItCan()
    {
        foreach (ItemData item in db.items.All)
        {
            if (!(item is CropData crop)) continue;
            foreach (SeasonData season in db.calendar.seasons)
            {
                if (!ClimateModel.IsOffSeason(crop, season)) continue;
                CalendarDate date = new CalendarDate { season = season, seasonProgress01 = 0.5f };
                DayWeather typical = new DayWeather
                {
                    temperature = season.temperatureCurve.Evaluate(0.5f),
                    precipitation = Mathf.Clamp01(season.precipitationCurve.Evaluate(0.5f))
                };
                float dayLength = db.run.DayLengthFor(season);
                float rate = ClimateModel.GrowthRate(crop, date, typical, db.climate, dayLength);
                float limit = Mathf.Max(crop.growTime, dayLength * db.climate.offSeasonDayFraction);
                Assert.LessOrEqual(Seconds(crop, rate, db.climate), limit + 0.01f, $"{crop.displayName} in {season.displayName} ({dayLength:0.#}s of daylight)");
            }
        }
    }

    [Test]
    public void EveryRealCropTakesAtLeastTenSecondsEvenWithEveryBoost()
    {
        Assert.GreaterOrEqual(db.climate.minGrowSeconds, 10f);
        float bestPossibleRate = db.climate.maxGrowth * 1.4f * 1.5f; // best season, rest-stop bonus, fertilizer-style boost
        foreach (ItemData item in db.items.All)
        {
            if (!(item is CropData crop)) continue;
            float seconds = crop.growTime / ClimateModel.CapRate(crop.growTime, bestPossibleRate, db.climate);
            Assert.GreaterOrEqual(seconds, 10f - 1e-3f, $"{crop.displayName} could be harvested in {seconds:0.0}s");
            Assert.GreaterOrEqual(crop.growTime, 10f, $"{crop.displayName}'s base time should already be 10s or more");
        }
    }
}
