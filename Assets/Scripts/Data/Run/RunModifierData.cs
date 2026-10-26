using System.Collections.Generic;
using UnityEngine;

/// <summary>A passive buff bought from the merchant and kept for the rest of the run.</summary>
[CreateAssetMenu(fileName = "Modifier_New", menuName = "MoFarmSand/Run/Run Modifier")]
public class RunModifierData : ScriptableObject, IHasId
{
    public string modifierId;
    public string displayName;
    [TextArea] public string description;
    public int price = 30;

    [Header("Effects")]
    [Tooltip("Disasters whose severity this reduces.")]
    public List<string> resistDisasterIds = new List<string>();
    [Range(0f, 1f)] public float resistAmount = 0.5f;
    [Tooltip("Apply the resist amount to every disaster, not just the ones listed.")]
    public bool resistAllDisasters;
    [Tooltip("Subtracted from the chance that a day has a disaster (0.05 = 5 percentage points). Days that are always disasters are unaffected.")]
    [Range(0f, 1f)] public float disasterChanceCut;
    [Tooltip("Crop growth multiplier while owned.")]
    public float growthMult = 1f;
    public int bonusMaxHp;
    [Tooltip("Extra hotbar slots while owned.")]
    public int bonusInventorySlots;
    [Tooltip("Multiplies how often harmful random events (EventData.harmful) come up. 1 = unchanged, 0.5 = half as often. Several modifiers multiply.")]
    [Range(0f, 1f)] public float harmfulEventChanceMult = 1f;
    [Tooltip("Chance (0-1) that each harvest also gives one more of the crop. Chances from several modifiers add up.")]
    [Range(0f, 1f)] public float extraCropChance;
    [Tooltip("Chance (0-1) that each harvest also drops one random seed of any kind. Chances from several modifiers add up.")]
    [Range(0f, 1f)] public float harvestSeedChance;

    [Tooltip("Extra coins you may bank each season end (Doggy Bank).")]
    public int depositLimitBonus;
    [Tooltip("Extra coins every crop sells for (Shiny Rock).")]
    public int sellBonusCoins;
    [Tooltip("Support crops grown are this much stronger (0.2 = 20%).")]
    [Range(0f, 1f)] public float supportCropBoost;
    [Tooltip("Attack crops (thrown and shooter) grown are this much stronger (0.2 = 20%).")]
    [Range(0f, 1f)] public float attackCropBoost;
    [Tooltip("More than 0 makes this a one-off box instead of a passive trinket: buying it gives this many random seeds, and it is never 'owned'.")]
    public int randomSeedsOnBuy;

    [Header("Availability")]
    [Tooltip("Units on the merchant's shelf when stocked (trinkets are normally 1; boxes can be bought several times).")]
    public int shopStock = 1;
    [Tooltip("Can the merchant sell this? Turn off for modifiers that only come from permanent unlocks.")]
    public bool shopItem = true;

    public string Id => modifierId;
}
