using UnityEngine;

/// <summary>Wolf Necklace: blocks the next fatal hit this year.</summary>
[CreateAssetMenu(fileName = "Effect_FatalSave", menuName = "MoFarmSand/Run Effects/Fatal Save")]
public class FatalSaveEffect : RunEffect
{
    public int saves = 1;

    public override void Apply(RunState run, GameDatabase db) { run.fatalSaves += Mathf.Max(0, saves); }

    public override string Describe() => saves == 1 ? "survive one fatal hit" : $"survive {saves} fatal hits";
}
