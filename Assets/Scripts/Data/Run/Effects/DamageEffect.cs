using UnityEngine;

/// <summary>Costs hearts but never kills: HP stops at 1.</summary>
[CreateAssetMenu(fileName = "Effect_Damage", menuName = "MoFarmSand/Run Effects/Damage")]
public class DamageEffect : RunEffect
{
    public int amount = 1;

    public override void Apply(RunState run, GameDatabase db) { run.hp = Mathf.Max(1, run.hp - amount); }
    public override string Describe() => $"-{amount} HP";
}
