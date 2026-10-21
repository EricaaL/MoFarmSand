using UnityEngine;

/// <summary>How much stronger the bosses get each year. Year 1 is the baseline. Single owner of the scaling numbers.</summary>
[CreateAssetMenu(fileName = "DifficultyConfig", menuName = "MoFarmSand/Difficulty Config")]
public class DifficultyConfig : ScriptableObject
{
    [Header("Fullness (how much feeding the boss needs)")]
    [Tooltip("Added to the fullness multiplier for every year after Year 1 (0.25 = +25% per year).")]
    [Min(0f)] public float fullnessGrowthPerYear = 0.25f;
    [Min(1f)] public float maxFullnessMult = 3f;

    [Header("Speed (how fast the boss chases)")]
    [Min(0f)] public float speedGrowthPerYear = 0.05f;
    [Tooltip("Keep this low enough that no stage can outrun the farmer (walks at 5). The Butterfly starts at 4.5, so 1.1 is its limit.")]
    [Min(1f)] public float maxSpeedMult = 1.1f;

    [Header("Damage (hearts lost per hit)")]
    [Tooltip("The boss hits one heart harder every this many years (0 = never).")]
    [Min(0)] public int damageEveryYears = 3;
    [Min(0)] public int maxDamageBonus = 1;
}
