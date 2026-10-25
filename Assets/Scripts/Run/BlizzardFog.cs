using DistantLands.Cozy;
using UnityEngine;

/// <summary>
/// Thickens COZY's fog and pulls it toward a cold white while this object is alive, so a blizzard cuts the view down.
/// Lives on the blizzard's ambient FX prefab (spawned and destroyed with the disaster by <see cref="CozyBridge"/>).
/// Hooks <see cref="CozyWeather.CozyUpdateLoop"/> like <see cref="SeasonAtmosphere"/>, so it stacks with the season look.
/// </summary>
public class BlizzardFog : MonoBehaviour
{
    [Tooltip("Multiplies COZY's fog density once fully faded in (1 = no change).")]
    [SerializeField] private float fogBoost = 6f;
    [SerializeField] private Color fogTint = new Color(0.88f, 0.93f, 1f);
    [Range(0f, 1f)] [SerializeField] private float tintStrength = 0.85f;
    [Tooltip("Seconds for the whiteout to roll in.")]
    [SerializeField] private float fadeInSeconds = 3f;

    private float startedAt;

    private void OnEnable()
    {
        startedAt = Time.unscaledTime;
        CozyWeather.CozyUpdateLoop += Apply;
    }

    private void OnDisable() { CozyWeather.CozyUpdateLoop -= Apply; }

    private void Apply()
    {
        CozyWeather cozy = CozyWeather.instance;
        if (cozy == null) return;

        float k = Mathf.Clamp01((Time.unscaledTime - startedAt) / Mathf.Max(0.01f, fadeInSeconds));
        float w = tintStrength * k;
        cozy.fogDensityMultiplier *= Mathf.Lerp(1f, fogBoost, k);
        cozy.fogColor1 = Pull(cozy.fogColor1, w);
        cozy.fogColor2 = Pull(cozy.fogColor2, w);
        cozy.fogColor3 = Pull(cozy.fogColor3, w);
        cozy.fogColor4 = Pull(cozy.fogColor4, w);
        cozy.fogColor5 = Pull(cozy.fogColor5, w);
        cozy.fogFlareColor = Pull(cozy.fogFlareColor, w);
        cozy.heightFogColor = Pull(cozy.heightFogColor, w);
    }

    /// <summary>Lerps toward the tint but keeps the original brightness, so a night blizzard stays dark.</summary>
    private Color Pull(Color original, float w)
    {
        float peak = Mathf.Max(original.r, original.g, original.b);
        float tintPeak = Mathf.Max(fogTint.r, fogTint.g, fogTint.b, 0.0001f);
        Color target = fogTint * (peak / tintPeak);
        target.a = original.a;
        return Color.Lerp(original, target, w);
    }
}
