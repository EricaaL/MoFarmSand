using UnityEngine;

/// <summary>Market tuning. Single owner of markup, saturation and drift.</summary>
[CreateAssetMenu(fileName = "EconomyConfig", menuName = "MoFarmSand/Economy/Economy Config")]
public class EconomyConfig : ScriptableObject
{
    [Tooltip("Multiplier applied to seed prices.")]
    public float buyMarkup = 1f;
    [Tooltip("Each crop sold multiplies demand by (1 - this).")]
    [Range(0f, 0.5f)] public float saturationPerUnit = 0.08f;
    [Range(0.1f, 1f)] public float minDemand = 0.4f;
    [Range(1f, 2f)] public float maxDemand = 1.3f;
    [Tooltip("Fraction of the gap to 1.0 recovered each day.")]
    [Range(0f, 1f)] public float recoveryPerDay = 0.35f;
    [Tooltip("Random walk applied to demand each day.")]
    [Range(0f, 0.3f)] public float driftAmplitude = 0.06f;
    [Header("Merchant stock")]
    [Tooltip("Units of each seed on the shelf after a Merchant day restock.")]
    public int restockSeedStock = 6;
    [Tooltip("Units of each seed on the shelf at the start of a run.")]
    public int starterSeedStock = 2;
    public int shopSeedSlots = 3;
    public int shopModifierSlots = 2;
    [Header("Market events")]
    [Tooltip("Season-long price swings (inflation, deflation) that can strike on any day; see MarketEventRoller.")]
    public System.Collections.Generic.List<MarketEventData> marketEvents = new System.Collections.Generic.List<MarketEventData>();
}
