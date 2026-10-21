using UnityEngine;

/// <summary>Pure price math. The PriceTable asset is the only owner of base numbers.</summary>
public static class EconomyModel
{
    /// <summary>What the merchant pays for one crop.</summary>
    public static int SellPrice(PriceTable.PriceRow row, SeasonData season, float demand, float shock = 1f)
    {
        if (row == null) return 0;
        return Mathf.Max(1, Mathf.RoundToInt(row.baseSell * row.SeasonMultFor(season) * demand * shock));
    }

    /// <summary>What the merchant charges for one seed.</summary>
    public static int SeedPrice(PriceTable.PriceRow row, SeasonData season, EconomyConfig cfg, float shock = 1f)
    {
        if (row == null) return 0;
        float markup = cfg != null ? cfg.buyMarkup : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(row.seedCost * row.SeasonMultFor(season) * markup * shock));
    }

    /// <summary>Selling floods the market: demand falls for each unit sold.</summary>
    public static void ApplySale(MarketState market, string cropId, int quantity, EconomyConfig cfg)
    {
        float d = market.GetDemand(cropId);
        d *= Mathf.Pow(1f - cfg.saturationPerUnit, Mathf.Max(0, quantity));
        market.SetDemand(cropId, Mathf.Clamp(d, cfg.minDemand, cfg.maxDemand));
    }

    /// <summary>Overnight: demand recovers toward 1 and random-walks a little.</summary>
    public static void AdvanceDay(MarketState market, EconomyConfig cfg, SeededRng rng)
    {
        for (int i = 0; i < market.entries.Count; i++)
        {
            MarketEntry e = market.entries[i];
            float d = Mathf.Lerp(e.demand, 1f, cfg.recoveryPerDay);
            d += rng.NextSigned() * cfg.driftAmplitude;
            e.demand = Mathf.Clamp(d, cfg.minDemand, cfg.maxDemand);
        }
    }
}
