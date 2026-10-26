using UnityEngine;

/// <summary>Rules for the season-end bank. Single owner of fee and caps.</summary>
[CreateAssetMenu(fileName = "BankConfig", menuName = "MoFarmSand/Economy/Bank Config")]
public class BankConfig : ScriptableObject
{
    [Tooltip("Percent of every deposit the bank keeps.")]
    [Range(0f, 100f)] public float depositFeePercent = 10f;
    [Tooltip("Max coins that can be deposited per season (0 = no cap). You can never bank your whole purse.")]
    public int maxDepositPerSeason = 25;
    [Tooltip("Max coins the vault (upgrade money) can hold at vault level 0.")]
    public int vaultCap = 200;

    [Header("Withdrawals (vault -> purse at season-end bank stops)")]
    [Tooltip("Fee percent for a tiny withdrawal. The percent then rises with the amount, so taking more costs a bigger share.")]
    [Range(0f, 100f)] public float withdrawFeeBasePercent = 7.5f;
    [Tooltip("Extra fee percent per coin withdrawn. With base 7.5 and slope 0.25: 10 coins -> 10% (fee 1), 50 coins -> 20% (fee 10).")]
    [Min(0f)] public float withdrawFeeSlopePercent = 0.25f;
    [Tooltip("The fee percent never goes above this.")]
    [Range(0f, 100f)] public float withdrawFeeMaxPercent = 90f;

    [Header("Bigger Vault upgrade (repeatable, unlimited)")]
    [Tooltip("Each vault level multiplies the vault limit by this.")]
    [Min(1f)] public float capGrowth = 1.8f;
    [Tooltip("Vault coins for the first Bigger Vault level.")]
    [Min(1)] public int upgradeBasePrice = 150;
    [Tooltip("Each level owned multiplies the next price by this. Keep it below capGrowth so every level stays affordable.")]
    [Min(1f)] public float upgradePriceGrowth = 1.6f;
    [Min(1)] public int upgradeRoundTo = 5;

    [Header("Bigger Deposits upgrade (repeatable, unlimited)")]
    [Tooltip("Extra coins per season the bank accepts for each level.")]
    [Min(1)] public int depositUpgradeStep = 10;
    [Tooltip("Vault coins for the first Bigger Deposits level.")]
    [Min(1)] public int depositUpgradeBasePrice = 50;
    [Tooltip("Each level owned multiplies the next price by this.")]
    [Min(1f)] public float depositUpgradePriceGrowth = 1.5f;
}
