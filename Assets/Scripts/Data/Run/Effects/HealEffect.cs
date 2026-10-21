using UnityEngine;

[CreateAssetMenu(fileName = "Effect_Heal", menuName = "MoFarmSand/Run Effects/Heal")]
public class HealEffect : RunEffect
{
    private const int FullHeal = 99;

    [Tooltip("Hearts restored. 99 or more heals fully.")]
    public int amount = FullHeal;

    public override void Apply(RunState run, GameDatabase db) { run.hp = Mathf.Min(run.maxHp, run.hp + amount); }
    public override string Describe() => amount >= FullHeal ? "Heal fully" : $"Heal {amount}";
}
