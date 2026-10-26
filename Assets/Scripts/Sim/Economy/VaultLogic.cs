using UnityEngine;

/// <summary>Pure rules for the Bigger Vault upgrade: how many coins the vault holds and what the next level costs.</summary>
public static class VaultLogic
{
    /// <summary>Vault limit at <paramref name="level"/>. Level 0 is <see cref="BankConfig.vaultCap"/>.</summary>
    public static int CapFor(int level, BankConfig cfg)
        => TierPricing.Price(cfg.vaultCap, cfg.capGrowth, 1, level);

    public static int Cap(MetaProgress meta, BankConfig cfg) => CapFor(meta.vaultLevel, cfg);

    public static int PriceFor(int level, BankConfig cfg)
        => cfg == null ? 0 : TierPricing.Price(cfg.upgradeBasePrice, cfg.upgradePriceGrowth, cfg.upgradeRoundTo, level);

    /// <summary>Extra coins per season that may be banked at <paramref name="level"/> of Bigger Deposits.</summary>
    public static int DepositBonus(int level, BankConfig cfg)
        => cfg == null ? 0 : Mathf.Max(0, level) * cfg.depositUpgradeStep;

    public static int DepositPriceFor(int level, BankConfig cfg)
        => cfg == null ? 0 : TierPricing.Price(cfg.depositUpgradeBasePrice, cfg.depositUpgradePriceGrowth, cfg.upgradeRoundTo, level);

    public static bool TryBuyDeposit(MetaProgress meta, BankConfig cfg)
    {
        if (meta == null || cfg == null || meta.depositLevel >= TierPricing.SanityCap) return false;
        int price = DepositPriceFor(meta.depositLevel, cfg);
        if (meta.vaultCoins < price) return false;
        meta.vaultCoins -= price;
        meta.depositLevel++;
        return true;
    }

    public static bool TryBuy(MetaProgress meta, BankConfig cfg)
    {
        if (meta == null || cfg == null || meta.vaultLevel >= TierPricing.SanityCap) return false;
        int price = PriceFor(meta.vaultLevel, cfg);
        if (meta.vaultCoins < price) return false;
        meta.vaultCoins -= price;
        meta.vaultLevel++;
        return true;
    }
}
