using UnityEngine;

/// <summary>
/// A season-long market swing (inflation, deflation): from the day it strikes until the season ends, every price the merchant
/// sets (seeds and charms he sells, crops he buys) is multiplied. Rolled per day by <see cref="MarketEventRoller"/>.
/// </summary>
[CreateAssetMenu(fileName = "MarketEvent_New", menuName = "MoFarmSand/Economy/Market Event")]
public class MarketEventData : ScriptableObject
{
    public string eventId;
    public string displayName = "Inflation";
    [TextArea] public string bannerText = "The merchant's prices are up for the rest of the season.";
    [Tooltip("Multiplies every merchant price while it lasts (1.2 = 20% more, 0.8 = 20% less).")]
    public float priceMultiplier = 1.2f;
    [Tooltip("Chance on each day of a season that this event strikes (when no market event is active yet). The events' chances add up.")]
    [Range(0f, 1f)] public float chancePerDay = 0.05f;
}
