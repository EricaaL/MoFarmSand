using UnityEngine;

[CreateAssetMenu(fileName = "Effect_AddModifier", menuName = "MoFarmSand/Run Effects/Add Modifier")]
public class AddModifierEffect : RunEffect
{
    public RunModifierData modifier;

    public override void Apply(RunState run, GameDatabase db)
    {
        if (modifier == null || run.modifierIds.Contains(modifier.Id)) return;
        run.modifierIds.Add(modifier.Id);
        if (modifier.bonusMaxHp != 0)
        {
            run.maxHp = Mathf.Max(1, run.maxHp + modifier.bonusMaxHp);
            run.hp = Mathf.Clamp(run.hp + Mathf.Max(0, modifier.bonusMaxHp), 1, run.maxHp);
        }
    }

    public override string Describe() => modifier != null ? modifier.displayName : "nothing";
}
