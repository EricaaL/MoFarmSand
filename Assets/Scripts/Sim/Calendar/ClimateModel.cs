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
        float affinity = 1f;
        if (c.seasonAffinity != null && date.season != null)
        {
            for (int i = 0; i < c.seasonAffinity.Count; i++)
            {
                if (c.seasonAffinity[i].season == date.season) { affinity = c.seasonAffinity[i].multiplier; break; }
            }
        }

        // Wrong-season penalty: below-1 affinities are squared (or more) so they hurt far more than they used to.
        float exponent = cfg != null ? cfg.seasonPenaltyExponent : 2f;
        if (affinity < 1f) affinity = Mathf.Pow(Mathf.Max(0f, affinity), Mathf.Max(1f, exponent));

        float t = Bell(weather.temperature, c.idealTemp, c.tempTolerance);
        float m = Bell(weather.precipitation, c.idealMoisture, c.moistureTolerance);
        return Mathf.Clamp(affinity * t * m, min, max);
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
    /// less damage to the boss and fewer shield charges. Uses the climate and season only, not temporary
    /// boosts like Fertilizer.
    /// </summary>
    public static float Quality(CropData crop, CalendarDate date, DayWeather weather, ClimateConfig cfg)
    {
        float min = cfg != null ? cfg.minQuality : 0.25f;
        float max = cfg != null ? cfg.maxQuality : 1.25f;
        return Mathf.Clamp(GrowthMultiplier(crop, date, weather, cfg), min, max);
    }

    /// <summary>Shield charges a support crop of the given quality grants (always at least 1).</summary>
    public static int ShieldCharges(CropData crop, float quality)
        => Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(1, crop.shieldCharges) * Mathf.Max(0f, quality)), 1, PlayerHealth.MaxShieldCharges);

    /// <summary>1 at the ideal value, falling smoothly with distance (Gaussian-like).</summary>
    private static float Bell(float value, float ideal, float tolerance)
    {
        float tol = Mathf.Max(0.0001f, tolerance);
        float z = (value - ideal) / tol;
        return Mathf.Exp(-z * z);
    }
}
