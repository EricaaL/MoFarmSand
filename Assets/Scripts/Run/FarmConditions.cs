using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Today's date, weather and growth modifiers. FarmTiles ask it for their crop's growth rate.</summary>
public class FarmConditions : MonoBehaviour
{
    [SerializeField] private ClimateConfig climate;

    private readonly Dictionary<CropData, float> cache = new Dictionary<CropData, float>();
    private int cachedVersion = -1;

    public ModifierStack Growth { get; } = new ModifierStack();
    public CalendarDate Date { get; private set; }
    public DayWeather Weather { get; private set; }

    /// <summary>Set by RunDirector each day. Null in the original trailer scene, so harvests give no extras there.</summary>
    public HarvestBonus Bonus { get; set; }

    /// <summary>Seam event for the HUD: a harvest turned up a bonus seed.</summary>
    public event Action<SeedData> OnBonusSeed;

    /// <summary>Rolls the harvest bonus and, if it hits and there's room, adds the seed. Returns whether a seed was granted.</summary>
    public bool TryGrantBonusSeed(PlayerInventory inventory)
    {
        if (Bonus == null || inventory == null) return false;
        SeedData seed = Bonus.Roll();
        if (seed == null || !inventory.AddItem(seed, 1)) return false;
        OnBonusSeed?.Invoke(seed);
        return true;
    }

    public void Configure(CalendarDate date, DayWeather weather)
    {
        Date = date;
        Weather = weather;
        cache.Clear();
    }

    /// <summary>Strength of a crop planted now (1 = normal). Weak in the wrong season.</summary>
    public float QualityFor(CropData crop)
    {
        if (crop == null || Date.season == null) return 1f;
        return ClimateModel.Quality(crop, Date, Weather, climate);
    }

    public float GrowthRateFor(CropData crop)
    {
        if (crop == null) return 1f;
        if (Date.season == null) return ClimateModel.CapRate(crop.growTime, Growth.Product, climate);
        if (cachedVersion != Growth.Version) { cache.Clear(); cachedVersion = Growth.Version; }
        if (!cache.TryGetValue(crop, out float rate))
        {
            rate = ClimateModel.CapRate(crop.growTime, ClimateModel.GrowthMultiplier(crop, Date, Weather, climate) * Growth.Product, climate);
            cache[crop] = rate;
        }
        return rate;
    }
}
