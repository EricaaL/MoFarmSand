using UnityEngine;

public struct DepositResult
{
    /// <summary>Coins removed from the run wallet.</summary>
    public int spent;
    /// <summary>Coins credited to the vault after the fee.</summary>
    public int credited;
    public int fee => spent - credited;
}

public struct WithdrawResult
{
    /// <summary>Coins removed from the vault.</summary>
    public int taken;
    /// <summary>Coins added to the run purse after the fee.</summary>
    public int received;
    public int fee => taken - received;
}

/// <summary>Pure bank rules: deposits fill the vault (permanent upgrades); withdrawals move vault coins back to the purse for a fee.</summary>
public static class BankLogic
{
    /// <summary>
    /// Fee for withdrawing <paramref name="amount"/> coins. The percentage rises with the amount
    /// (base + slope * amount, capped), so 10 coins costs 1 and 50 costs 10 with the default config. At least 1.
    /// </summary>
    public static int WithdrawFee(int amount, BankConfig cfg)
    {
        if (amount <= 0) return 0;
        float percent = Mathf.Min(cfg.withdrawFeeMaxPercent, cfg.withdrawFeeBasePercent + cfg.withdrawFeeSlopePercent * amount);
        return Mathf.Max(1, Mathf.RoundToInt(amount * Mathf.Clamp01(percent / 100f)));
    }

    /// <summary>Takes up to <paramref name="amount"/> coins out of the vault; the purse gets that minus the fee.</summary>
    public static WithdrawResult Withdraw(RunState run, MetaProgress meta, int amount, BankConfig cfg)
    {
        WithdrawResult none = default;
        int taken = Mathf.Min(amount, meta.vaultCoins);
        if (taken <= 0) return none;

        int received = taken - WithdrawFee(taken, cfg);
        if (received <= 0) return none;

        meta.vaultCoins -= taken;
        run.coins += received;
        return new WithdrawResult { taken = taken, received = received };
    }

    public static DepositResult Deposit(RunState run, MetaProgress meta, int amount, BankConfig cfg)
    {
        DepositResult none = default;
        if (amount <= 0 || run.coins <= 0) return none;

        int usable = Mathf.Min(amount, run.coins);
        if (cfg.maxDepositPerSeason > 0) usable = Mathf.Min(usable, Mathf.Max(0, cfg.maxDepositPerSeason - run.depositedThisSeason));
        if (usable <= 0) return none;

        float keep = 1f - Mathf.Clamp01(cfg.depositFeePercent / 100f);
        if (keep <= 0f) return none;

        int room = Mathf.Max(0, VaultLogic.Cap(meta, cfg) - meta.vaultCoins);
        if (room <= 0) return none;

        int credited = Mathf.FloorToInt(usable * keep);
        if (credited > room)
        {
            credited = room;
            usable = Mathf.Min(usable, Mathf.CeilToInt(room / keep));
        }
        if (credited <= 0) return none;

        run.coins -= usable;
        run.depositedThisSeason += usable;
        run.depositsThisRun += credited;
        meta.vaultCoins += credited;
        return new DepositResult { spent = usable, credited = credited };
    }

    public static bool TryBuyUnlock(MetaProgress meta, UnlockData unlock)
    {
        if (unlock == null || meta.unlockedIds.Contains(unlock.Id) || meta.vaultCoins < unlock.cost) return false;
        meta.vaultCoins -= unlock.cost;
        meta.unlockedIds.Add(unlock.Id);
        return true;
    }
}
