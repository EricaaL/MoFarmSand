using System.Collections.Generic;
using UnityEngine;

/// <summary>Pure climate math: samples a day's weather and converts it to a crop growth multiplier.</summary>
public static class ClimateModel
{
    public static DayWeather SampleDay(CalendarDate date, ClimateConfig cfg, SeededRng rng)
    {
        SeasonData s = date.season;
        DayWeather w = new DayWeather { kind = cfg != null ? cfg.fallbackKind : WeatherKind.Clear };
        if (s == null) return w;

        w.temperature = s.temperatureCurve.Evaluate(date.seasonProgress01) + rng.NextSigned() * s.temperatureVariance;
        w.precipitation = Mathf.Clamp01(s.precipitationCurve.Evaluate(date.seasonProgress01) + rng.NextSigned() * s.precipitationVariance);

        if (cfg != null)
        {
            for (int i = 0; i < cfg.rules.Count; i++)
            {
                ClimateConfig.WeatherKindRule r = cfg.rules[i];
                if (w.temperature >= r.minTemp && w.temperature <= r.maxTemp
                    && w.precipitation >= r.minPrecip && w.precipitation <= r.maxPrecip)
                {
                    w.kind = r.kind;
                    break;
                }
            }
        }
        return w;
    }

    public static float GrowthMultiplier(CropData crop, CalendarDate date, DayWeather weather, ClimateConfig cfg)
    {
        float min = cfg != null ? cfg.minGrowth : 0.12f;
        float max = cfg != null ? cfg.maxGrowth : 1.6f;
        if (crop == null) return 1f;

        CropClimate c = crop.climate;
        float affinity = AffinityFor(crop, date.season);

        // Wrong-season penalty: below-1 affinities are squared (or more) so they hurt far more than they used to.
        float exponent = cfg != null ? cfg.seasonPenaltyExponent : 2f;
        if (affinity < 1f) affinity = Mathf.Pow(Mathf.Max(0f, affinity), Mathf.Max(1f, exponent));

        float t = Bell(weather.temperature, c.idealTemp, c.tempTolerance);
        float m = Bell(weather.precipitation, c.idealMoisture, c.moistureTolerance);
        return Mathf.Clamp(affinity * t * m, min, max);
    }

    /// <summary>The crop's own affinity for a season, before the wrong-season penalty: 1 = in season, below 1 = off season.</summary>
    public static float AffinityFor(CropData crop, SeasonData season)
    {
        List<SeasonAffinity> list = crop != null ? crop.climate.seasonAffinity : null;
        if (list == null || season == null) return 1f;
        for (int i = 0; i < list.Count; i++)
            if (list[i].season == season) return list[i].multiplier;
        return 1f;
    }

    public static bool IsOffSeason(CropData crop, SeasonData season) => AffinityFor(crop, season) < 1f;

    /// <summary>
    /// The growth speed a farm tile uses: <see cref="GrowthMultiplier"/>, except that an off-season crop is never slower than
    /// <see cref="OffSeasonFloor"/>. Strength (<see cref="Quality"/>) still uses the raw multiplier, so off-season crops stay weak.
    /// </summary>
    public static float GrowthRate(CropData crop, CalendarDate date, DayWeather weather, ClimateConfig cfg, float dayLengthSeconds)
        => OffSeasonFloor(crop, date.season, GrowthMultiplier(crop, date, weather, cfg), dayLengthSeconds, cfg);

    /// <summary>
    /// Keeps an off-season crop from outlasting the daylight: it takes at most <see cref="ClimateConfig.offSeasonDayFraction"/> of the day
    /// (a 60 s day and 0.83 gives 50 s), and never longer than its normal grow time. In-season crops and days with no clock are left alone.
    /// </summary>
    public static float OffSeasonFloor(CropData crop, SeasonData season, float rate, float dayLengthSeconds, ClimateConfig cfg)
    {
        float fraction = cfg != null ? cfg.offSeasonDayFraction : 0f;
        if (fraction <= 0f || dayLengthSeconds <= 0f || crop == null || crop.growTime <= 0f || !IsOffSeason(crop, season)) return rate;
        float floor = Mathf.Min(1f, crop.growTime / (dayLengthSeconds * fraction));
        return Mathf.Max(rate, floor);
    }

    /// <summary>Caps a growth rate so a crop never finishes faster than <see cref="ClimateConfig.minGrowSeconds"/>.</summary>
    public static float CapRate(float growTime, float rate, ClimateConfig cfg)
    {
        float minSeconds = cfg != null ? cfg.minGrowSeconds : 0f;
        if (minSeconds <= 0f || growTime <= 0f) return rate;
        return Mathf.Min(rate, growTime / minSeconds);
    }

    /// <summary>
    /// How strong a crop grown in these conditions turns out (1 = normal). Out-of-season crops are weaker:
    /// less damage to the boss and a shorter Bark Shield. Uses the climate and season only, not temporary
    /// boosts like Fertilizer.
    /// </summary>
    public static float Quality(CropData crop, CalendarDate date, DayWeather weather, ClimateConfig cfg)
    {
        float min = cfg != null ? cfg.minQuality : 0.25f;
        float max = cfg != null ? cfg.maxQuality : 1f;
        return Mathf.Clamp(GrowthMultiplier(crop, date, weather, cfg), min, max);
    }

    /// <summary>Shield charges a support crop grants: always the crop's own count, whatever its quality.</summary>
    public static int ShieldCharges(CropData crop, float quality)
        => Mathf.Clamp(Mathf.Max(1, crop.shieldCharges), 1, PlayerHealth.MaxShieldCharges);

    /// <summary>How long a support crop's Bark Shield lasts: full <see cref="CropData.shieldSeconds"/> at 100% quality, 10% of it at 10%.</summary>
    public static float ShieldSeconds(CropData crop, float quality) => crop.shieldSeconds * Mathf.Max(0f, quality);

    /// <summary>1 at the ideal value, falling smoothly with distance (Gaussian-like).</summary>
    private static float Bell(float value, float ideal, float tolerance)
    {
        float tol = Mathf.Max(0.0001f, tolerance);
        float z = (value - ideal) / tol;
        return Mathf.Exp(-z * z);
    }
}
