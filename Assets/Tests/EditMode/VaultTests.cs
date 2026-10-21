using NUnit.Framework;
using UnityEngine;

public class VaultTests
{
    private GameDatabase db;

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    [Test]
    public void LevelZeroHoldsTheBaseCapAndEveryLevelHoldsMore()
    {
        Assert.AreEqual(db.bank.vaultCap, VaultLogic.CapFor(0, db.bank));
        for (int level = 1; level < 25; level++)
            Assert.Greater(VaultLogic.CapFor(level, db.bank), VaultLogic.CapFor(level - 1, db.bank), $"level {level}");
    }

    [Test]
    public void EachLevelCostsMoreAndIsAlwaysAffordableWithinTheCurrentVault()
    {
        for (int level = 0; level < 25; level++)
        {
            int price = VaultLogic.PriceFor(level, db.bank);
            if (level > 0) Assert.Greater(price, VaultLogic.PriceFor(level - 1, db.bank), $"level {level + 1}");
            Assert.Less(price, VaultLogic.CapFor(level, db.bank), $"level {level + 1} could never be bought");
        }
    }

    [Test]
    public void BuyingSpendsTheVaultAndRaisesTheLimit()
    {
        int price = VaultLogic.PriceFor(0, db.bank);
        MetaProgress meta = new MetaProgress { vaultCoins = price + 7 };

        Assert.IsTrue(VaultLogic.TryBuy(meta, db.bank));
        Assert.AreEqual(7, meta.vaultCoins);
        Assert.AreEqual(1, meta.vaultLevel);
        Assert.AreEqual(VaultLogic.CapFor(1, db.bank), VaultLogic.Cap(meta, db.bank));

        Assert.IsFalse(VaultLogic.TryBuy(meta, db.bank), "can't afford the next level");
        Assert.AreEqual(1, meta.vaultLevel);
    }

    [Test]
    public void DepositsFillUpToTheUpgradedLimit()
    {
        MetaProgress meta = new MetaProgress { vaultCoins = db.bank.vaultCap, vaultLevel = 1 };
        RunState run = new RunState { coins = 1000 };
        BankConfig cfg = TestUtil.Make<BankConfig>();
        cfg.depositFeePercent = 0f; cfg.maxDepositPerSeason = 0; cfg.vaultCap = db.bank.vaultCap; cfg.capGrowth = db.bank.capGrowth;

        DepositResult r = BankLogic.Deposit(run, meta, 1000, cfg);

        Assert.Greater(r.credited, 0, "the bigger vault has room");
        Assert.AreEqual(VaultLogic.CapFor(1, cfg), meta.vaultCoins);
        Object.DestroyImmediate(cfg);
    }

    [Test]
    public void EditedSavesCannotOverfillOrAskForAbsurdLevels()
    {
        SaveFile file = new SaveFile { hasRun = true, run = RunLogic.NewRun(3, db, new MetaProgress()) };
        file.meta.vaultLevel = 5000;
        file.meta.vaultCoins = int.MaxValue;

        SaveService.Sanitize(file, db);

        Assert.AreEqual(TierPricing.SanityCap, file.meta.vaultLevel);
        Assert.AreEqual(VaultLogic.Cap(file.meta, db.bank), file.meta.vaultCoins);

        file.meta.vaultLevel = 0;
        file.meta.vaultCoins = 99999;
        SaveService.Sanitize(file, db);
        Assert.AreEqual(db.bank.vaultCap, file.meta.vaultCoins, "without the upgrade the base cap applies");
    }

    [Test]
    public void VaultLevelSurvivesSaving()
    {
        SaveFile file = new SaveFile { meta = new MetaProgress { vaultLevel = 3 } };
        SaveFile back = JsonUtility.FromJson<SaveFile>(JsonUtility.ToJson(file));
        Assert.AreEqual(3, back.meta.vaultLevel);
    }
}
