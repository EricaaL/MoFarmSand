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
    [Tooltip("Crop growth multiplier while owned.")]
    public float growthMult = 1f;
    public int bonusMaxHp;
    [Tooltip("Chance (0-1) that each harvest also drops one random seed of any kind. Chances from several modifiers add up.")]
    [Range(0f, 1f)] public float harvestSeedChance;

    [Header("Availability")]
    [Tooltip("Can the merchant sell this? Turn off for modifiers that only come from permanent unlocks.")]
    public bool shopItem = true;

    public string Id => modifierId;
}
