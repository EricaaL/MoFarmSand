using DistantLands.Cozy;
using UnityEngine;

/// <summary>
/// Exaggerates each season's sky by tinting COZY's computed colors every frame (winter whiter, autumn redder).
/// Hooks <see cref="CozyWeather.CozyUpdateLoop"/>, which fires after COZY writes its atmosphere and before it
/// pushes to shaders and lights, so any weather profile or disaster is tinted too. Null-safe without COZY.
/// </summary>
public class SeasonAtmosphere : MonoBehaviour
{
    private CozyWeatherMap.SeasonLook from = CozyWeatherMap.SeasonLook.Neutral;
    private CozyWeatherMap.SeasonLook to = CozyWeatherMap.SeasonLook.Neutral;
    private float blend = 1f;
    private float blendSeconds = 1f;

    private void OnEnable() { CozyWeather.CozyUpdateLoop += Tint; }
    private void OnDisable() { CozyWeather.CozyUpdateLoop -= Tint; }

    /// <summary>Fade from the current look to <paramref name="target"/>.</summary>
    public void SetLook(CozyWeatherMap.SeasonLook target, float seconds)
    {
        from = CozyWeatherMap.SeasonLook.Lerp(from, to, blend);
        to = target;
        blend = 0f;
        blendSeconds = Mathf.Max(0.01f, seconds);
    }

    private void Tint()
    {
        CozyWeather cozy = CozyWeather.instance;
        if (cozy == null) return;

        blend = Mathf.MoveTowards(blend, 1f, Time.unscaledDeltaTime / blendSeconds);
        CozyWeatherMap.SeasonLook look = CozyWeatherMap.SeasonLook.Lerp(from, to, blend);

        // 1 at noon, 0 below the horizon.
        float day01 = Mathf.Clamp01(Mathf.Sin((cozy.dayPercentage - 0.25f) * 2f * Mathf.PI) * 2f);
        float w = look.strength * Mathf.Lerp(look.nightStrength, 1f, day01);

        cozy.skyZenithColor = Pull(cozy.skyZenithColor, look.skyTint, w);
        cozy.skyHorizonColor = Pull(cozy.skyHorizonColor, look.skyTint, w);

        cozy.fogColor1 = Pull(cozy.fogColor1, look.fogTint, w);
        cozy.fogColor2 = Pull(cozy.fogColor2, look.fogTint, w);
        cozy.fogColor3 = Pull(cozy.fogColor3, look.fogTint, w);
        cozy.fogColor4 = Pull(cozy.fogColor4, look.fogTint, w);
        cozy.fogColor5 = Pull(cozy.fogColor5, look.fogTint, w);
        cozy.fogFlareColor = Pull(cozy.fogFlareColor, look.fogTint, w);
        cozy.heightFogColor = Pull(cozy.heightFogColor, look.fogTint, w);

        cozy.cloudColor = Pull(cozy.cloudColor, look.cloudTint, w);
        cozy.cloudHighlightColor = Pull(cozy.cloudHighlightColor, look.cloudTint, w);
        cozy.highAltitudeCloudColor = Pull(cozy.highAltitudeCloudColor, look.cloudTint, w);

        cozy.sunlightColor = Pull(cozy.sunlightColor, look.lightTint, w);
        cozy.ambientLightHorizonColor = Pull(cozy.ambientLightHorizonColor, look.ambientTint, w);
        cozy.ambientLightZenithColor = Pull(cozy.ambientLightZenithColor, look.ambientTint, w);

        cozy.filterSaturation += look.saturation * w;
        cozy.filterValue += look.brightness * w;
        cozy.fogDensityMultiplier *= Mathf.Lerp(1f, look.fogDensity, w);
    }

    /// <summary>
    /// Lerps toward the tint but keeps the original brightness, so dark night colors turn
    /// dark red / dark white instead of glowing at full tint brightness.
    /// </summary>
    private static Color Pull(Color original, Color tint, float w)
    {
        float peak = Mathf.Max(original.r, original.g, original.b);
        float tintPeak = Mathf.Max(tint.r, tint.g, tint.b, 0.0001f);
        Color target = tint * (peak / tintPeak);
        target.a = original.a;
        return Color.Lerp(original, target, w);
    }
}
