using UnityEngine;

/// <summary>Leaves today's event prop hungry (the wild chocobo): for the rest of this day it eats planted crops. See <see cref="ChocoboAppetite"/>.</summary>
[CreateAssetMenu(fileName = "Effect_Hungry", menuName = "MoFarmSand/Run Effects/Hungry Prop")]
public class HungryPropEffect : RunEffect
{
    public override void Apply(RunState run, GameDatabase db) { run.hungryNodeId = run.pendingNodeId; }
    public override string Describe() => "It stays hungry";
}
