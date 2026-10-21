using UnityEngine;

/// <summary>One season. Single owner of its length, climate curves and COZY year mapping.</summary>
[CreateAssetMenu(fileName = "Season_New", menuName = "MoFarmSand/Season Data")]
public class SeasonData : ScriptableObject, IHasId
{
    public string seasonId;
    public string displayName;
    [Min(1)] public int lengthInDays = 3;
    public Color uiColor = Color.white;

    [Header("Climate (x = progress through the season 0..1)")]
    [Tooltip("Mean temperature in Celsius.")]
    public AnimationCurve temperatureCurve = AnimationCurve.Linear(0f, 15f, 1f, 15f);
    [Tooltip("Max random day-to-day swing in Celsius.")]
    public float temperatureVariance = 3f;
    [Tooltip("Mean moisture 0 (dry) .. 1 (soaked).")]
    public AnimationCurve precipitationCurve = AnimationCurve.Linear(0f, 0.4f, 1f, 0.4f);
    public float precipitationVariance = 0.2f;

    [Header("Clock")]
    [Tooltip("Scales RunConfig.dayLengthSeconds: winter days are shorter, summer days longer.")]
    [Range(0.5f, 1.5f)] public float dayLengthMultiplier = 1f;

    [Header("Disasters")]
    [Tooltip("Added to a node's base disaster chance during this season.")]
    [Range(-1f, 1f)] public float disasterChanceBonus;

    [Header("COZY mapping")]
    [Range(0f, 1f)] public float cozyYearStart01;
    [Range(0f, 1f)] public float cozyYearSpan01 = 0.25f;
    [Tooltip("Unused at runtime: ground snow now comes from CozyWeatherMap.SeasonLook.snowStart01/snowEnd01.")]
    [Range(0f, 1f)] public float initialSnow01;

    public string Id => seasonId;
}
