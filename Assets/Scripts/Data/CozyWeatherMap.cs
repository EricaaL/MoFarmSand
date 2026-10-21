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
        [Tooltip("Multiplies COZY's sunlight color by day (1 = unchanged). Summer pushes this up for a stronger sun.")]
        public float sunIntensity;
        [Tooltip("Multiplies COZY's ambient light by day (1 = unchanged).")]
        public float ambientIntensity;
        [Tooltip("Particles that drift through the whole season (autumn leaves, winter snowfall). Not blended between seasons.")]
        public GameObject ambientFx;
        [Tooltip("Ground snow cover 0..1 at the start of the season.")]
        [Range(0f, 1f)] public float snowStart01;
        [Tooltip("Ground snow cover 0..1 at the end of the season.")]
        [Range(0f, 1f)] public float snowEnd01;

        public static SeasonLook Neutral => new SeasonLook
        {
            skyTint = Color.white, fogTint = Color.white, cloudTint = Color.white, lightTint = Color.white,
            ambientTint = Color.white, fogDensity = 1f, sunIntensity = 1f, ambientIntensity = 1f,
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
                sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
                ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t),
                ambientFx = b.ambientFx,
                snowStart01 = Mathf.Lerp(a.snowStart01, b.snowStart01, t),
                snowEnd01 = Mathf.Lerp(a.snowEnd01, b.snowEnd01, t),
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
        {
            if (seasonLooks[i].season != season) continue;
            // Looks saved before these fields existed hold 0; read that as "unchanged".
            SeasonLook look = seasonLooks[i];
            if (look.sunIntensity <= 0f) look.sunIntensity = 1f;
            if (look.ambientIntensity <= 0f) look.ambientIntensity = 1f;
            return look;
        }
        SeasonLook neutral = SeasonLook.Neutral;
        neutral.season = season;
        return neutral;
    }

    /// <summary>The season's drifting particles (leaves, snowfall), or null.</summary>
    public GameObject SeasonFxFor(SeasonData season) => LookFor(season).ambientFx;

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
