using UnityEngine;

[CreateAssetMenu(fileName = "Boss_New", menuName = "MoFarmSand/Boss Data")]
public class BossData : ScriptableObject
{
    public string displayName = "Caterpillar";
    public float maxFullness = 100f;
    public float chaseSpeed = 3.5f;
    public int damage = 1;
    [Tooltip("Stage this boss evolves into when full. Leave empty for the final stage.")]
    public BossData nextStage;

    [Header("Fullness reactions (BossReactions)")]
    [Tooltip("Fullness fraction where the boss starts to blush and sweat.")]
    [Range(0f, 1f)] public float blushTier = 0.5f;
    [Tooltip("Fullness fraction where it is visibly stuffed (deeper blush, more sweat).")]
    [Range(0f, 1f)] public float stuffedTier = 0.8f;

    private void OnValidate()
    {
        stuffedTier = Mathf.Max(blushTier, stuffedTier);
        maxFullness = Mathf.Max(1f, maxFullness);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        damage = Mathf.Max(0, damage);
    }
}
