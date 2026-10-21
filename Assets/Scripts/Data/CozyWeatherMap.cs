using System;
using System.Collections.Generic;
using DistantLands.Cozy.Data;
using UnityEngine;

/// <summary>
/// Presentation mapping: gameplay (season, weather kind, disaster) to COZY weather profiles and ambient FX.
/// Kept separate so gameplay ScriptableObjects never hold COZY types.
/// </summary>
[CreateAssetMenu(fileName = "CozyWeatherMap", menuName = "MoFarmSand/Cozy Weather Map")]
public class CozyWeatherMap : ScriptableObject
{
    [Serializable]
    public struct DefaultEntry
    {
        [Tooltip("Leave empty to match any season.")] public SeasonData season;
        public WeatherKind kind;
        public WeatherProfile profile;
    }

    [Serializable]
    public struct DisasterEntry
    {
        public DisasterData disaster;
        public WeatherProfile profile;
        public GameObject ambientFx;
    }

    /// <summary>Exaggerated atmosphere tint for one season, applied on top of whatever COZY computes.</summary>
    [Serializable]
    public struct SeasonLook
    {
        public SeasonData season;
        [ColorUsage(false)] public Color skyTint;
        [ColorUsage(false)] public Color fogTint;
        [ColorUsage(false)] public Color cloudTint;
        [ColorUsage(false)] public Color lightTint;
        [ColorUsage(false)] public Color ambientTint;
        [Tooltip("How far COZY's colors are pulled toward the tints by day.")]
        [Range(0f, 1f)] public float strength;
        [Tooltip("Multiplier on strength at night, so boss nights stay spooky but still season-coloured.")]
        [Range(0f, 1f)] public float nightStrength;
        [Tooltip("Added to COZY's filter saturation.")] public float saturation;
        [Tooltip("Added to COZY's filter value.")] public float brightness;
        [Tooltip("Multiplies COZY's fog density.")] public float fogDensity;

        public static SeasonLook Neutral => new SeasonLook
        {
            skyTint = Color.white, fogTint = Color.white, cloudTint = Color.white, lightTint = Color.white,
            ambientTint = Color.white, fogDensity = 1f,
        };

        public static SeasonLook Lerp(SeasonLook a, SeasonLook b, float t)
        {
            return new SeasonLook
            {
                season = b.season,
                skyTint = Color.Lerp(a.skyTint, b.skyTint, t),
                fogTint = Color.Lerp(a.fogTint, b.fogTint, t),
                cloudTint = Color.Lerp(a.cloudTint, b.cloudTint, t),
                lightTint = Color.Lerp(a.lightTint, b.lightTint, t),
                ambientTint = Color.Lerp(a.ambientTint, b.ambientTint, t),
                strength = Mathf.Lerp(a.strength, b.strength, t),
                nightStrength = Mathf.Lerp(a.nightStrength, b.nightStrength, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t),
                brightness = Mathf.Lerp(a.brightness, b.brightness, t),
                fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
            };
        }
    }

    public float transitionSeconds = 4f;
    public List<DefaultEntry> defaults = new List<DefaultEntry>();
    public List<DisasterEntry> disasters = new List<DisasterEntry>();
    public List<SeasonLook> seasonLooks = new List<SeasonLook>();

    /// <summary>The season's look, or a neutral (no-op) look when none is configured.</summary>
    public SeasonLook LookFor(SeasonData season)
    {
        for (int i = 0; i < seasonLooks.Count; i++)
            if (seasonLooks[i].season == season) return seasonLooks[i];
        SeasonLook neutral = SeasonLook.Neutral;
        neutral.season = season;
        return neutral;
    }

    public WeatherProfile ProfileFor(SeasonData season, WeatherKind kind, DisasterData disaster)
    {
        if (disaster != null)
        {
            for (int i = 0; i < disasters.Count; i++)
                if (disasters[i].disaster == disaster && disasters[i].profile != null) return disasters[i].profile;
        }
        for (int i = 0; i < defaults.Count; i++)
            if (defaults[i].season == season && defaults[i].kind == kind && defaults[i].profile != null) return defaults[i].profile;
        for (int i = 0; i < defaults.Count; i++)
            if (defaults[i].season == null && defaults[i].kind == kind && defaults[i].profile != null) return defaults[i].profile;
        return null;
    }

    public GameObject AmbientFxFor(DisasterData disaster)
    {
        if (disaster == null) return null;
        for (int i = 0; i < disasters.Count; i++)
            if (disasters[i].disaster == disaster) return disasters[i].ambientFx;
        return null;
    }
}
