using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class EconomyAndBankTests
{
    private GameDatabase db;
    private readonly List<Object> created = new List<Object>();

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    // ------------------------------------------------------------------ economy

    private PriceTable.PriceRow Row(SeasonData season, float seasonMult)
    {
        PriceTable.PriceRow r = new PriceTable.PriceRow { seedCost = 4, baseSell = 10 };
        r.seasonMults.Add(new PriceTable.SeasonMult { season = season, mult = seasonMult });
        return r;
    }

    [Test]
    public void PriceFormulasUseSeasonDemandAndShock()
    {
        SeasonData s = TestUtil.Season("summer"); created.Add(s);
        PriceTable.PriceRow row = Row(s, 1.5f);
        EconomyConfig cfg = TestUtil.Make<EconomyConfig>(); created.Add(cfg);
        cfg.buyMarkup = 1f;

        Assert.AreEqual(15, EconomyModel.SellPrice(row, s, 1f));
        Assert.AreEqual(8, EconomyModel.SellPrice(row, s, 0.5f), "demand scales the price");
        Assert.AreEqual(30, EconomyModel.SellPrice(row, s, 1f, 2f), "shock scales the price");
        Assert.AreEqual(6, EconomyModel.SeedPrice(row, s, cfg));
        Assert.AreEqual(1, EconomyModel.SellPrice(new PriceTable.PriceRow { baseSell = 1 }, s, 0.01f), "never below 1");
    }

    [Test]
    public void SellingLowersDemandDownToTheFloorAndItRecoversOvernight()
    {
        EconomyConfig cfg = db.economy;
        MarketState market = new MarketState();
        float last = market.GetDemand("tomato");
        Assert.AreEqual(1f, last);

        for (int i = 0; i < 60; i++)
        {
            EconomyModel.ApplySale(market, "tomato", 1, cfg);
            float d = market.GetDemand("tomato");
            Assert.LessOrEqual(d, last + 1e-6f, "monotonic");
            Assert.GreaterOrEqual(d, cfg.minDemand - 1e-6f, "floored");
            last = d;
        }
        Assert.AreEqual(cfg.minDemand, last, 1e-4f);

        float before = market.GetDemand("tomato");
        EconomyModel.AdvanceDay(market, cfg, new SeededRng(1));
        Assert.Greater(market.GetDemand("tomato"), before, "demand recovers toward 1");
    }

    [Test]
    public void EverySeedIsProfitableAtBaseDemandInItsSeasons()
    {
        foreach (PriceTable.PriceRow row in db.prices.rows)
        {
            foreach (SeasonData season in db.calendar.seasons)
            {
                if (!row.StockedIn(season)) continue;
                int cost = EconomyModel.SeedPrice(row, season, db.economy);
                int revenue = row.crop.harvestYield * EconomyModel.SellPrice(row, season, 1f);
                Assert.Less(cost, revenue, $"{row.seed.Id} in {season.displayName}: cost {cost} vs revenue {revenue}");
            }
        }
    }

    [Test]
    public void ShopOffersOnlyInSeasonSeedsAndUnownedModifiers()
    {
        RunState run = new RunState();
        run.modifierIds.Add("scarecrow");
        foreach (SeasonData season in db.calendar.seasons)
        {
            CalendarDate date = new CalendarDate { season = season };
            List<ShopOffer> offers = ShopGenerator.Generate(db, run, date, new SeededRng(9));
            Assert.LessOrEqual(offers.FindAll(o => o.kind == OfferKind.Seed).Count, db.economy.shopSeedSlots);
            foreach (ShopOffer o in offers)
            {
                if (o.kind == OfferKind.Seed) Assert.IsTrue(db.prices.RowForSeed(o.id).StockedIn(season), $"{o.id} not stocked in {season.displayName}");
                else Assert.AreNotEqual("scarecrow", o.id, "owned modifiers are not offered again");
            }
        }
    }

    [Test]
    public void PriceTableCoversEveryCropWithMatchingSeed()
    {
        Assert.IsTrue(db.prices.Validate(out List<string> errors), string.Join("; ", errors));
    }

    // ------------------------------------------------------------------ bank

    private static BankConfig Bank(float fee = 10f, int perSeason = 0, int vaultCap = 200)
    {
        BankConfig b = TestUtil.Make<BankConfig>();
        b.depositFeePercent = fee; b.maxDepositPerSeason = perSeason; b.vaultCap = vaultCap;
        return b;
    }

    [Test]
    public void DepositAppliesTheFee()
    {
        BankConfig cfg = Bank(); created.Add(cfg);
        RunState run = new RunState { coins = 50 };
        MetaProgress meta = new MetaProgress();

        DepositResult r = BankLogic.Deposit(run, meta, 20, cfg);

        Assert.AreEqual(20, r.spent);
        Assert.AreEqual(18, r.credited);
        Assert.AreEqual(2, r.fee);
        Assert.AreEqual(30, run.coins);
        Assert.AreEqual(18, meta.vaultCoins);
        Assert.AreEqual(18, run.depositsThisRun);
    }

    [Test]
    public void DepositIsLimitedByWalletSeasonCapAndVaultCap()
    {
        BankConfig cfg = Bank(0f, perSeason: 15, vaultCap: 100); created.Add(cfg);

        RunState a = new RunState { coins = 5 };
        Assert.AreEqual(5, BankLogic.Deposit(a, new MetaProgress(), 100, cfg).spent, "wallet limit");

        RunState b = new RunState { coins = 100 };
        MetaProgress mb = new MetaProgress();
        Assert.AreEqual(15, BankLogic.Deposit(b, mb, 100, cfg).spent, "season cap");
        Assert.AreEqual(0, BankLogic.Deposit(b, mb, 100, cfg).spent, "season cap is used up");

        BankConfig tight = Bank(10f, 0, 10); created.Add(tight);
        RunState c = new RunState { coins = 100 };
        MetaProgress mc = new MetaProgress();
        DepositResult r = BankLogic.Deposit(c, mc, 100, tight);
        Assert.AreEqual(10, mc.vaultCoins, "vault cap");
        Assert.AreEqual(12, r.spent, "only what is needed for 10 net coins is taken");
        Assert.AreEqual(88, c.coins);
    }

    [Test]
    public void ABankFeeOfOneHundredPercentDepositsNothing()
    {
        BankConfig cfg = Bank(100f); created.Add(cfg);
        RunState run = new RunState { coins = 10 };
        Assert.AreEqual(0, BankLogic.Deposit(run, new MetaProgress(), 10, cfg).spent);
        Assert.AreEqual(10, run.coins);
    }

    // ------------------------------------------------------------------ withdrawals

    private static BankConfig WithdrawBank()
    {
        BankConfig b = TestUtil.Make<BankConfig>(); // default curve: 7.5% + 0.25% per coin, capped at 90%
        return b;
    }

    [Test]
    public void WithdrawFeeMatchesTheDesignExamples()
    {
        BankConfig cfg = WithdrawBank(); created.Add(cfg);
        Assert.AreEqual(1, BankLogic.WithdrawFee(10, cfg), "10 coins costs 1");
        Assert.AreEqual(10, BankLogic.WithdrawFee(50, cfg), "50 coins costs 10");
        Assert.AreEqual(115, BankLogic.WithdrawFee(200, cfg), "57.5% of 200");
        Assert.AreEqual(0, BankLogic.WithdrawFee(0, cfg));
    }

    [Test]
    public void WithdrawFeeShareGrowsWithTheAmountAndIsCapped()
    {
        BankConfig cfg = WithdrawBank(); created.Add(cfg);
        float last = 0f;
        foreach (int amount in new[] { 10, 25, 50, 100, 200, 300 })
        {
            float share = BankLogic.WithdrawFee(amount, cfg) / (float)amount;
            Assert.Greater(share, last, $"{amount} coins should cost a bigger share than the amount before");
            last = share;
        }
        Assert.LessOrEqual(BankLogic.WithdrawFee(100000, cfg), 90000, "never more than the max percent");
        Assert.GreaterOrEqual(BankLogic.WithdrawFee(2, cfg), 1, "there is always at least a 1 coin fee");
    }

    [Test]
    public void WithdrawMovesVaultCoinsToThePurseMinusTheFee()
    {
        BankConfig cfg = WithdrawBank(); created.Add(cfg);
        RunState run = new RunState { coins = 5 };
        MetaProgress meta = new MetaProgress { vaultCoins = 100 };

        WithdrawResult r = BankLogic.Withdraw(run, meta, 50, cfg);

        Assert.AreEqual(50, r.taken);
        Assert.AreEqual(40, r.received);
        Assert.AreEqual(10, r.fee);
        Assert.AreEqual(50, meta.vaultCoins);
        Assert.AreEqual(45, run.coins);
    }

    [Test]
    public void WithdrawIsLimitedByTheVaultAndRefusesTinyOrEmptyRequests()
    {
        BankConfig cfg = WithdrawBank(); created.Add(cfg);
        RunState run = new RunState { coins = 0 };
        MetaProgress meta = new MetaProgress { vaultCoins = 20 };

        WithdrawResult r = BankLogic.Withdraw(run, meta, 500, cfg);
        Assert.AreEqual(20, r.taken, "can't take more than the vault holds");
        Assert.AreEqual(0, meta.vaultCoins);
        Assert.AreEqual(r.received, run.coins);

        Assert.AreEqual(0, BankLogic.Withdraw(run, meta, 10, cfg).taken, "empty vault");
        MetaProgress one = new MetaProgress { vaultCoins = 1 };
        Assert.AreEqual(0, BankLogic.Withdraw(new RunState(), one, 1, cfg).taken, "1 coin would leave nothing after the fee");
        Assert.AreEqual(1, one.vaultCoins);
        Assert.AreEqual(0, BankLogic.Withdraw(new RunState(), new MetaProgress { vaultCoins = 50 }, 0, cfg).taken);
    }

    [Test]
    public void DepositingThenWithdrawingAlwaysLosesCoins()
    {
        BankConfig cfg = Bank(10f, perSeason: 0, vaultCap: 1000); created.Add(cfg);
        RunState run = new RunState { coins = 100 };
        MetaProgress meta = new MetaProgress();

        DepositResult d = BankLogic.Deposit(run, meta, 100, cfg);
        BankLogic.Withdraw(run, meta, d.credited, cfg);

        Assert.Less(run.coins, 100, "the two fees mean no money is created by going round the bank");
    }

    [Test]
    public void TheRealBankConfigKeepsTheDesignExamples()
    {
        Assert.AreEqual(1, BankLogic.WithdrawFee(10, db.bank));
        Assert.AreEqual(10, BankLogic.WithdrawFee(50, db.bank));
    }

    [Test]
    public void VaultCoinsNeverCarryIntoTheNextYearsPurse()
    {
        MetaProgress meta = new MetaProgress { vaultCoins = 30 };
        RunState run = RunLogic.NewRun(5, db, meta);

        Assert.AreEqual(db.run.startingCoins, run.coins, "every year starts with the same small purse");
        Assert.AreEqual(30, meta.vaultCoins, "unspent vault coins stay banked for upgrades");
        Assert.AreEqual(1, meta.runsStarted);
    }

    [Test]
    public void LeftoverPurseCoinsDoNotCarryEither()
    {
        MetaProgress meta = new MetaProgress();
        RunState first = RunLogic.NewRun(1, db, meta);
        first.coins = 77;
        RunLogic.FailRun(first, meta);

        RunState second = RunLogic.NewRun(2, db, meta);
        Assert.AreEqual(db.run.startingCoins, second.coins);
        Assert.AreEqual(0, meta.vaultCoins, "coins that weren't banked are simply lost");
    }

    [Test]
    public void YearsCountUpFromOneAndSurviveOldSaves()
    {
        MetaProgress meta = new MetaProgress();
        Assert.AreEqual(1, RunLogic.NewRun(1, db, meta).year);
        Assert.AreEqual(2, RunLogic.NewRun(2, db, meta).year);
        RunState third = RunLogic.NewRun(3, db, meta);
        Assert.AreEqual(3, third.YearNumber(meta));

        // A save from before years existed has year 0: fall back to the years played.
        RunState old = new RunState { year = 0 };
        Assert.AreEqual(3, old.YearNumber(meta));
        Assert.AreEqual(1, old.YearNumber(new MetaProgress()), "never shows Year 0");
    }

    [Test]
    public void TheRealBankConfigHasADepositLimitSoYouCannotBankEverything()
    {
        Assert.Greater(db.bank.maxDepositPerSeason, 0, "a per-season limit must be set");

        RunState run = new RunState { coins = 500 };
        MetaProgress meta = new MetaProgress();
        DepositResult r = BankLogic.Deposit(run, meta, 500, db.bank);

        Assert.AreEqual(db.bank.maxDepositPerSeason, r.spent, "only the limit goes through, not the whole purse");
        Assert.Greater(run.coins, 0);
        Assert.AreEqual(0, BankLogic.Deposit(run, meta, 500, db.bank).spent, "the season's allowance is used up");

        // A new season (RunLogic resets this when the bank opens) allows another deposit.
        run.depositedThisSeason = 0;
        Assert.Greater(BankLogic.Deposit(run, meta, 500, db.bank).spent, 0);
    }

    [Test]
    public void TheYearsMaximumBankedIsBelowTheVaultCapSoUpgradesStayAGrind()
    {
        int perYear = db.bank.maxDepositPerSeason * db.calendar.seasons.Count;
        Assert.LessOrEqual(perYear, db.bank.vaultCap, "one year can't fill the vault on its own");
    }

    [Test]
    public void BuyingAnUnlockSpendsTheVaultOnce()
    {
        UnlockData u = db.unlocks.All[0];
        MetaProgress meta = new MetaProgress { vaultCoins = u.cost + 5 };
        Assert.IsTrue(BankLogic.TryBuyUnlock(meta, u));
        Assert.AreEqual(5, meta.vaultCoins);
        Assert.IsFalse(BankLogic.TryBuyUnlock(meta, u), "already owned");

        MetaProgress poor = new MetaProgress { vaultCoins = u.cost - 1 };
        Assert.IsFalse(BankLogic.TryBuyUnlock(poor, u));
    }

    [Test]
    public void UnlockEffectsApplyAtTheStartOfEveryRun()
    {
        MetaProgress meta = new MetaProgress();
        meta.unlockedIds.Add("hardy_paws");
        meta.unlockedIds.Add("nest_egg");

        RunState run = RunLogic.NewRun(11, db, meta);

        Assert.AreEqual(db.run.startingMaxHp + 1, run.maxHp);
        Assert.AreEqual(db.run.startingCoins + 25, run.coins);
    }
}
