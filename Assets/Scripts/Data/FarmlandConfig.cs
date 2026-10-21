using UnityEngine;

/// <summary>Extra farmland you can buy with vault coins. Each purchase adds a new column of plots; every one costs more.</summary>
[CreateAssetMenu(fileName = "FarmlandConfig", menuName = "MoFarmSand/Farmland Config")]
public class FarmlandConfig : ScriptableObject
{
    [Tooltip("Vault coins for the first extra farmland.")]
    [Min(1)] public int basePrice = 30;
    [Tooltip("Each farmland you already own multiplies the next price by this (1.7 = +70%).")]
    [Min(1f)] public float priceGrowth = 1.7f;
    [Tooltip("Prices are rounded to a multiple of this so they read cleanly.")]
    [Min(1)] public int roundTo = 5;
    [Tooltip("Most extra farmlands that can be bought. 0 = unlimited.")]
    [Min(0)] public int maxFarmlands = 0;
    [Tooltip("Columns placed to each side of the starting field before new columns start a new band of rows behind it.")]
    [Min(1)] public int maxColumnsPerSide = 4;
}
