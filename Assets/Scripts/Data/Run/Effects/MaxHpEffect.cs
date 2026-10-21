using UnityEngine;

[CreateAssetMenu(fileName = "Effect_MaxHp", menuName = "MoFarmSand/Run Effects/Max HP")]
public class MaxHpEffect : RunEffect
{
    public int delta = 1;

    public override void Apply(RunState run, GameDatabase db)
    {
        run.maxHp = Mathf.Max(1, run.maxHp + delta);
        run.hp = Mathf.Clamp(run.hp + Mathf.Max(0, delta), 1, run.maxHp);
    }

    public override string Describe() => delta >= 0 ? $"+{delta} max HP" : $"{delta} max HP";
}
