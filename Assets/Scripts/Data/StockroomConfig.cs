using UnityEngine;

/// <summary>The merchant's Bigger Stockroom upgrade (bought with vault coins). Each level stocks more, and costs more.</summary>
[CreateAssetMenu(fileName = "StockroomConfig", menuName = "MoFarmSand/Stockroom Config")]
public class StockroomConfig : ScriptableObject
{
    [Header("Price")]
    [Min(1)] public int basePrice = 25;
    [Tooltip("Each level you own multiplies the next price by this.")]
    [Min(1f)] public float priceGrowth = 1.8f;
    [Min(1)] public int roundTo = 5;
    [Tooltip("Highest level that can be bought. 0 = unlimited.")]
    [Min(0)] public int maxLevel = 0;

    [Header("What each level adds")]
    [Tooltip("Extra units of EVERY seed after a Merchant-day restock.")]
    [Min(0)] public int unitsPerLevel = 2;
    [Tooltip("Extra units of every seed on the shelf at the start of a year.")]
    [Min(0)] public int starterUnitsPerLevel = 1;
    [Tooltip("At this level the merchant stocks one more kind of seed (0 = never).")]
    [Min(0)] public int extraSeedKindAtLevel = 2;
    [Tooltip("At this level he also stocks one more charm (0 = never).")]
    [Min(0)] public int extraCharmAtLevel = 3;
}
