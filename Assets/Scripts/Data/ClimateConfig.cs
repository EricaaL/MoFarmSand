using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeatherKind { Clear, Cloudy, Rain, Storm, Snow, Heat }

/// <summary>Growth clamp and weather-kind thresholds. First matching rule wins (data, not a switch).</summary>
[CreateAssetMenu(fileName = "ClimateConfig", menuName = "MoFarmSand/Climate Config")]
public class ClimateConfig : ScriptableObject
{
    [Serializable]
    public struct WeatherKindRule
    {
        public WeatherKind kind;
        public float minTemp;
        public float maxTemp;
        public float minPrecip;
        public float maxPrecip;
    }

    [Tooltip("Slowest a crop can ever grow (0.12 = 8x longer than normal).")]
    [Range(0.05f, 1f)] public float minGrowth = 0.12f;
    [Range(1f, 3f)] public float maxGrowth = 1.6f;
    [Tooltip("No crop ever finishes faster than this, however good the season or boosts (seconds).")]
    [Min(0f)] public float minGrowSeconds = 10f;

    [Header("Wrong-season penalty")]
    [Tooltip("A crop's season affinity below 1 is raised to this power, so out-of-season planting is hit much harder (1 = old behavior).")]
    [Range(1f, 4f)] public float seasonPenaltyExponent = 2f;
    [Tooltip("An off-season crop takes at most this share of the day's daylight to grow (0.83 on a 60 s day = 50 s), and never longer than its normal time. 0 = no ceiling.")]
    [Range(0f, 1f)] public float offSeasonDayFraction = 0.83f;
    [Tooltip("Harvested crops are at least this effective, however bad the season was.")]
    [Range(0.05f, 1f)] public float minQuality = 0.25f;
    [Tooltip("Best-season crops can be this much stronger than normal.")]
    [Range(1f, 2f)] public float maxQuality = 1.25f;
    public WeatherKind fallbackKind = WeatherKind.Clear;
    public List<WeatherKindRule> rules = new List<WeatherKindRule>();
}
