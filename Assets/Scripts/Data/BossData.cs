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

    private void OnValidate()
    {
        maxFullness = Mathf.Max(1f, maxFullness);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        damage = Mathf.Max(0, damage);
    }
}
