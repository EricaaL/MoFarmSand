using UnityEngine;

[CreateAssetMenu(fileName = "Effect_AddCoins", menuName = "MoFarmSand/Run Effects/Add Coins")]
public class AddCoinsEffect : RunEffect
{
    public int amount = 10;

    public override void Apply(RunState run, GameDatabase db) { run.coins = Mathf.Max(0, run.coins + amount); }
    public override string Describe() => amount >= 0 ? $"+{amount} coins" : $"{amount} coins";
}
