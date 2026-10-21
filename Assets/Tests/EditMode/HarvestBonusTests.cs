using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class HarvestBonusTests
{
    private GameDatabase db;
    private readonly List<Object> created = new List<Object>();

    private T Track<T>(T o) where T : Object { created.Add(o); return o; }

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    private RunState RunWith(params string[] modifierIds)
    {
        RunState run = RunLogic.NewRun(3, db, new MetaProgress());
        run.modifierIds.AddRange(modifierIds);
        return run;
    }

    // ------------------------------------------------------------------ the unlock

    [Test]
    public void LuckySproutUnlockIsOfferedAndGivesTheHiddenModifierAtRunStart()
    {
        UnlockData sprout = db.unlocks.Get("lucky_sprout");
        Assert.IsNotNull(sprout, "run 'MoFarmSand > Add Run Systems' to create it");
        Assert.AreEqual(45, sprout.cost);

        MetaProgress meta = new MetaProgress { vaultCoins = 100 };
        Assert.IsTrue(BankLogic.TryBuyUnlock(meta, sprout), "bought with vault coins, outside a run");
        Assert.AreEqual(55, meta.vaultCoins);
        Assert.AreEqual(db.run.startingCoins, RunLogic.NewRun(1, db, meta).coins, "and the leftover vault never enters the purse");

        RunState run = RunLogic.NewRun(8, db, meta);
        CollectionAssert.Contains(run.modifierIds, "seed_finder");
        Assert.AreEqual(0.12f, HarvestBonus.ChanceFor(db, run), 1e-5f, "low chance, as designed");
    }

    [Test]
    public void WithoutTheUnlockThereIsNoBonus()
    {
        RunState run = RunLogic.NewRun(8, db, new MetaProgress());
        Assert.AreEqual(0f, HarvestBonus.ChanceFor(db, run));

        HarvestBonus bonus = new HarvestBonus(db, run, new SeededRng(1));
        for (int i = 0; i < 500; i++) Assert.IsNull(bonus.Roll());
    }

    [Test]
    public void TheMerchantNeverSellsTheUnlockOnlyModifier()
    {
        RunModifierData finder = db.modifiers.Get("seed_finder");
        Assert.IsFalse(finder.shopItem);

        RunState run = RunLogic.NewRun(2, db, new MetaProgress());
        foreach (SeasonData season in db.calendar.seasons)
            for (int seed = 0; seed < 60; seed++)
            {
                List<ShopOffer> offers = ShopGenerator.Generate(db, run, new CalendarDate { season = season }, new SeededRng(seed));
                Assert.IsFalse(offers.Exists(o => o.id == "seed_finder"), "seed_finder must stay unlock-only");
            }
    }

    [Test]
    public void EveryUnlockModifierEffectPointsAtARegisteredModifier()
    {
        foreach (UnlockData u in db.unlocks.All)
            foreach (RunEffect e in u.onRunStart)
                if (e is AddModifierEffect add)
                    Assert.AreSame(add.modifier, db.modifiers.Get(add.modifier.Id), $"{u.Id} grants an unregistered modifier");
    }

    // ------------------------------------------------------------------ rolling

    [Test]
    public void ChancesAddUpAndClampToOne()
    {
        RunState run = RunWith("seed_finder", "seed_finder");
        Assert.AreEqual(0.24f, HarvestBonus.ChanceFor(db, run), 1e-5f, "duplicates in a save still add (tiers can stack)");

        RunModifierData big = Track(TestUtil.Make<RunModifierData>());
        big.modifierId = "test_big"; big.harvestSeedChance = 1f;
        db.modifiers.Ensure(big);
        try
        {
            Assert.AreEqual(1f, HarvestBonus.ChanceFor(db, RunWith("seed_finder", "test_big")), 1e-5f);
        }
        finally { RemoveFromRegistry(db.modifiers, big); }
    }

    private static void RemoveFromRegistry(RunModifierRegistry reg, RunModifierData item)
    {
        FieldInfo f = typeof(IdRegistry<RunModifierData>).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic);
        ((List<RunModifierData>)f.GetValue(reg)).Remove(item);
        typeof(IdRegistry<RunModifierData>).GetField("lookup", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reg, null);
    }

    [Test]
    public void RollFollowsTheChanceAndPicksUniformlyFromEverySeed()
    {
        RunState run = RunWith("seed_finder");
        HarvestBonus bonus = new HarvestBonus(db, run, new SeededRng(77));

        Dictionary<string, int> counts = new Dictionary<string, int>();
        const int harvests = 20000;
        int hits = 0;
        for (int i = 0; i < harvests; i++)
        {
            SeedData seed = bonus.Roll();
            if (seed == null) continue;
            hits++;
            counts[seed.Id] = counts.TryGetValue(seed.Id, out int c) ? c + 1 : 1;
        }

        Assert.That(hits / (float)harvests, Is.EqualTo(0.12f).Within(0.015f), "about 12% of harvests");

        // Every seed you have access to can drop; locked crop variants can't.
        int seedKinds = 0;
        foreach (ItemData item in db.items.All)
            if (item is SeedData seed && CropAvailability.IsSeedAvailable(seed, run.unlockedIds, db)) seedKinds++;
        Assert.AreEqual(seedKinds, counts.Count, "every available kind of seed can drop");
        foreach (KeyValuePair<string, int> kv in counts)
            Assert.That(kv.Value / (float)hits, Is.EqualTo(1f / seedKinds).Within(0.05f), $"{kv.Key} is as likely as any other seed");
    }

    [Test]
    public void SameSeedAndOrderGivesTheSameLuck()
    {
        RunState run = RunWith("seed_finder");
        HarvestBonus a = new HarvestBonus(db, run, SeededRng.Derive(5, 9, RngStreams.HarvestBonus));
        HarvestBonus b = new HarvestBonus(db, run, SeededRng.Derive(5, 9, RngStreams.HarvestBonus));
        for (int i = 0; i < 200; i++) Assert.AreSame(a.Roll(), b.Roll());
    }

    // ------------------------------------------------------------------ in the scene

    private PlayerInventory NewInventory(out GameObject player)
    {
        player = Track(new GameObject("Player"));
        return player.AddComponent<PlayerInventory>();
    }

    private FarmConditions Conditions(HarvestBonus bonus)
    {
        FarmConditions cond = Track(new GameObject("Conditions")).AddComponent<FarmConditions>();
        cond.Bonus = bonus;
        return cond;
    }

    private HarvestBonus Guaranteed()
    {
        RunModifierData sure = Track(TestUtil.Make<RunModifierData>());
        sure.modifierId = "test_sure"; sure.harvestSeedChance = 1f;
        db.modifiers.Ensure(sure);
        try { return new HarvestBonus(db, RunWith("test_sure"), new SeededRng(2)); }
        finally { RemoveFromRegistry(db.modifiers, sure); }
    }

    [Test]
    public void GrantedSeedLandsInTheInventoryAndFiresTheHudEvent()
    {
        PlayerInventory inv = NewInventory(out _);
        FarmConditions cond = Conditions(Guaranteed());
        SeedData announced = null;
        cond.OnBonusSeed += s => announced = s;

        Assert.IsTrue(cond.TryGrantBonusSeed(inv));

        Assert.IsNotNull(announced);
        Assert.AreEqual(1, inv.CountOf(announced));
    }

    [Test]
    public void NoRoomMeansNoSeedAndNoToast()
    {
        PlayerInventory inv = NewInventory(out _);
        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            CropData filler = Track(TestUtil.Make<CropData>());
            filler.cropId = "filler" + i;
            inv.AddItem(filler, 1);
        }
        FarmConditions cond = Conditions(Guaranteed());
        bool fired = false;
        cond.OnBonusSeed += _ => fired = true;

        Assert.IsFalse(cond.TryGrantBonusSeed(inv));
        Assert.IsFalse(fired);
    }

    [Test]
    public void TilesWithNoBonusConfiguredDropNothingExtra()
    {
        // The original trailer scene never sets a bonus: harvests give exactly the crop.
        PlayerInventory inv = NewInventory(out _);
        FarmConditions cond = Conditions(null);
        Assert.IsFalse(cond.TryGrantBonusSeed(inv));
    }

    [Test]
    public void HarvestingGivesTheCropPlusAFreeSeedWhenTheBonusHits()
    {
        CropData tomato = (CropData)db.items.Get("tomato");
        SeedData tomatoSeed = (SeedData)db.items.Get("seed_tomato");
        PlayerInventory inv = NewInventory(out GameObject player);
        inv.AddItem(tomatoSeed, 1);

        FarmTile tile = Track(new GameObject("Tile")).AddComponent<FarmTile>();
        tile.SetFreePlanting(false);
        tile.SetConditions(Conditions(Guaranteed()));

        tile.Interact(player);
        typeof(FarmTile).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(tile, 0f);
        typeof(FarmTile).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tile, null);
        int seedsBefore = 0;
        foreach (ItemData item in db.items.All) if (item is SeedData s) seedsBefore += inv.CountOf(s);
        Assert.AreEqual(0, seedsBefore, "the only seed was planted");

        tile.Interact(player);

        Assert.AreEqual(tomato.harvestYield, inv.CountOf(tomato));
        int seedsAfter = 0;
        foreach (ItemData item in db.items.All) if (item is SeedData s) seedsAfter += inv.CountOf(s);
        Assert.AreEqual(1, seedsAfter, "one bonus seed on top of the crop");
    }
}
