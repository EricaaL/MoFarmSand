using UnityEngine;

/// <summary>Pure rules for the Bigger Stockroom upgrade: what it costs and how it changes the merchant's shelf.</summary>
public static class StockroomLogic
{
    // ------------------------------------------------------------------ buying

    public static int PriceFor(int level, StockroomConfig cfg)
        => cfg == null ? 0 : TierPricing.Price(cfg.basePrice, cfg.priceGrowth, cfg.roundTo, level);

    public static bool IsMaxed(int level, StockroomConfig cfg) => cfg == null || TierPricing.AtCap(level, cfg.maxLevel);

    public static bool TryBuy(MetaProgress meta, StockroomConfig cfg)
    {
        if (meta == null || IsMaxed(meta.stockroomLevel, cfg)) return false;
        int price = PriceFor(meta.stockroomLevel, cfg);
        if (meta.vaultCoins < price) return false;
        meta.vaultCoins -= price;
        meta.stockroomLevel++;
        return true;
    }

    // ------------------------------------------------------------------ effects on the shelf

    /// <summary>Units of each seed after a Merchant-day restock.</summary>
    public static int RestockUnits(int baseUnits, int level, StockroomConfig cfg)
        => baseUnits + (cfg == null ? 0 : cfg.unitsPerLevel * Mathf.Max(0, level));

    /// <summary>Units of each seed on the shelf at the start of a year.</summary>
    public static int StarterUnits(int baseUnits, int level, StockroomConfig cfg)
        => baseUnits + (cfg == null ? 0 : cfg.starterUnitsPerLevel * Mathf.Max(0, level));

    /// <summary>How many kinds of seed the merchant stocks.</summary>
    public static int SeedKinds(int baseKinds, int level, StockroomConfig cfg)
        => baseKinds + (cfg != null && cfg.extraSeedKindAtLevel > 0 && level >= cfg.extraSeedKindAtLevel ? 1 : 0);

    /// <summary>How many trinkets he stocks on Merchant days.</summary>
    public static int CharmSlots(int baseSlots, int level, StockroomConfig cfg)
        => baseSlots + (cfg != null && cfg.extraCharmAtLevel > 0 && level >= cfg.extraCharmAtLevel ? 1 : 0);

    /// <summary>What reaching <paramref name="level"/> gives you, for the shop text. Empty for level 0.</summary>
    public static string DescribeLevel(int level, StockroomConfig cfg)
    {
        if (cfg == null || level <= 0) return string.Empty;
        string text = $"+{cfg.unitsPerLevel} of every seed per restock";
        if (cfg.extraSeedKindAtLevel > 0 && level == cfg.extraSeedKindAtLevel) text += ", +1 kind of seed";
        if (cfg.extraCharmAtLevel > 0 && level == cfg.extraCharmAtLevel) text += ", +1 trinket";
        return text;
    }
}
