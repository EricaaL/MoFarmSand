using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>The 8-crop roster (two per season), the shooter / scare crops, and the seed-lock rules.</summary>
public class CropRosterTests
{
    private GameDatabase db;

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    private CropData Crop(string id) => (CropData)db.items.Get(id);

    private static RunState FreshRun(GameDatabase db, int seed, params string[] unlocked)
    {
        MetaProgress meta = new MetaProgress();
        meta.unlockedIds.AddRange(unlocked);
        return RunLogic.NewRun(seed, db, meta);
    }

    // ------------------------------------------------------------------ the roster

    [Test]
    public void EachSeasonHasItsTwoCropsAsItsBestToGrow()
    {
        var expected = new Dictionary<string, string[]>
        {
            { "spring", new[] { "peas", "lettuce" } },
            { "summer", new[] { "tomato", "corn" } },
            { "autumn", new[] { "turnip", "pumpkin" } },
            { "winter", new[] { "garlic", "spinach" } },
        };

        ItemInfoContext ctx = new ItemInfoContext { db = db };
        Dictionary<string, List<string>> actual = new Dictionary<string, List<string>>();
        foreach (ItemData item in db.items.All)
        {
            if (!(item is CropData crop)) continue;
            string best = ItemInfoBuilder.BestSeason(crop, ctx, out _).seasonId;
            if (!actual.ContainsKey(best)) actual[best] = new List<string>();
            actual[best].Add(crop.Id);
        }

        foreach (var kv in expected) CollectionAssert.AreEquivalent(kv.Value, actual[kv.Key], $"best crops of {kv.Key}");
    }

    [Test]
    public void RemovedCropsAreGone()
    {
        foreach (string id in new[] { "frostberry", "honey_turnip", "sunfire_tomato", "golden_pumpkin", "aurora_frostberry" })
        {
            Assert.IsFalse(db.items.TryGet(id, out _), $"{id} should be removed");
            Assert.IsFalse(db.items.TryGet("seed_" + id, out _), $"seed_{id} should be removed");
        }
        foreach (UnlockData u in db.unlocks.All) Assert.IsFalse(u.UnlocksCrops, $"{u.Id} still unlocks a crop variant");
    }

    [Test]
    public void EveryCropHasASeedAndAPriceRowAndAStockingSeason()
    {
        foreach (ItemData item in db.items.All)
        {
            if (!(item is CropData crop)) continue;
            Assert.IsNotNull(CropAvailability.SeedFor(crop, db), $"{crop.Id} has no seed");
            PriceTable.PriceRow row = db.prices.RowForCrop(crop.Id);
            Assert.IsNotNull(row, $"{crop.Id} has no price row");
            Assert.IsNotNull(row.seed, $"{crop.Id}'s price row has no seed");
        }
    }

    // ------------------------------------------------------------------ shooters and garlic

    [Test]
    public void PeasAndCornAreTenShotShooters()
    {
        foreach (string id in new[] { "peas", "corn" })
        {
            CropData c = Crop(id);
            Assert.AreEqual(CropType.Shooter, c.type, id);
            Assert.AreEqual(10, c.ammoPerCrop, $"{id} holds 10 shots");
            Assert.Greater(c.fullnessValue, 0f, id);
            Assert.IsNotNull(c.projectilePrefab, $"{id} needs a bullet prefab");
            Assert.IsNotNull(c.projectilePrefab.GetComponent<CropProjectile>(), $"{id} bullet needs CropProjectile");
        }
    }

    [Test]
    public void ShooterBulletsAreMuchWeakerThanAThrownCropButAddUpOverAMagazine()
    {
        foreach (string id in new[] { "peas", "corn" })
        {
            CropData c = Crop(id);
            Assert.Less(c.fullnessValue, Crop("tomato").fullnessValue, $"{id}: one bullet is weaker than a tomato");
            Assert.Greater(c.fullnessValue * c.ammoPerCrop, Crop("tomato").fullnessValue, $"{id}: a whole magazine beats one tomato");
        }
    }

    [Test]
    public void GarlicIsAThrownCropThatScaresTheBossForThreeSeconds()
    {
        CropData garlic = Crop("garlic");
        Assert.AreEqual(CropType.Projectile, garlic.type);
        Assert.AreEqual(3f, garlic.scareSeconds);
        Assert.IsNotNull(garlic.projectilePrefab);
        Assert.AreEqual(0f, Crop("tomato").scareSeconds, "only garlic scares");
    }

    [Test]
    public void ShieldCropsAreLettuceTurnipSpinach()
    {
        List<string> support = new List<string>();
        foreach (ItemData item in db.items.All)
            if (item is CropData c && c.type == CropType.Support) support.Add(c.Id);
        CollectionAssert.AreEquivalent(new[] { "lettuce", "turnip", "spinach" }, support);
    }

    // ------------------------------------------------------------------ seed locks (no shipped crop is locked any more)

    [Test]
    public void NoBaseCropIsLockedAtStart()
    {
        List<string> none = new List<string>();
        foreach (ItemData item in db.items.All)
            Assert.IsTrue(CropAvailability.IsItemAvailable(item, none, db), $"{item.Id} should be available from the start");
    }

    [Test]
    public void EachSeedIsGatedByAtMostOneUnlock()
    {
        Dictionary<SeedData, string> owner = new Dictionary<SeedData, string>();
        foreach (UnlockData u in db.unlocks.All)
            foreach (SeedData s in u.unlocksSeeds)
            {
                Assert.IsFalse(owner.ContainsKey(s), $"{s.Id} is gated by both {(owner.ContainsKey(s) ? owner[s] : "?")} and {u.Id}");
                owner[s] = u.Id;
            }
    }

    [Test]
    public void UnlockCostsAreAffordableWithinTheVaultCap()
    {
        foreach (UnlockData u in db.unlocks.All)
            Assert.LessOrEqual(u.cost, db.bank.vaultCap, $"{u.Id} could never be bought: it costs more than the vault holds");
    }

    [Test]
    public void ALockedSeedAndItsCropShareOneLock()
    {
        // The gate rules still exist even though no shipped crop uses them, so test them on a throwaway database.
        CropData crop = TestUtil.Make<CropData>(); crop.cropId = "test_crop";
        SeedData seed = TestUtil.Make<SeedData>(); seed.seedId = "seed_test_crop"; seed.grows = crop;
        UnlockData gate = TestUtil.Make<UnlockData>(); gate.unlockId = "crop_test"; gate.unlocksSeeds = new List<SeedData> { seed };
        GameDatabase fake = TestUtil.Make<GameDatabase>();
        fake.items = TestUtil.Make<ItemRegistry>(); fake.items.Ensure(crop); fake.items.Ensure(seed);
        fake.unlocks = TestUtil.Make<UnlockRegistry>(); fake.unlocks.Ensure(gate);

        List<string> none = new List<string>();
        List<string> owned = new List<string> { gate.Id };

        Assert.IsFalse(CropAvailability.IsItemAvailable(seed, none, fake));
        Assert.IsFalse(CropAvailability.IsItemAvailable(crop, none, fake));
        Assert.AreSame(gate, CropAvailability.LockedBehind(crop, none, fake));
        Assert.IsTrue(CropAvailability.IsItemAvailable(seed, owned, fake));
        Assert.IsTrue(CropAvailability.IsItemAvailable(crop, owned, fake));
        Assert.IsNull(CropAvailability.LockedBehind(seed, owned, fake));
    }

    // ------------------------------------------------------------------ saves

    [Test]
    public void UnlockSnapshotSurvivesSavesAndUnknownIdsAreDropped()
    {
        RunState run = FreshRun(db, 5, "hardy_paws");
        CollectionAssert.AreEquivalent(new[] { "hardy_paws" }, run.unlockedIds);

        SaveFile file = new SaveFile { hasRun = true, run = run };
        file.meta.unlockedIds.AddRange(run.unlockedIds);
        SaveFile back = JsonUtility.FromJson<SaveFile>(JsonUtility.ToJson(file));
        CollectionAssert.AreEquivalent(run.unlockedIds, back.run.unlockedIds);

        // A save from before the roster change may still list a deleted crop unlock.
        back.run.unlockedIds.Add("crop_honey_turnip");
        back.run.unlockedIds.Add("ghost_unlock");
        SaveService.Sanitize(back, db);
        CollectionAssert.DoesNotContain(back.run.unlockedIds, "crop_honey_turnip");
        CollectionAssert.DoesNotContain(back.run.unlockedIds, "ghost_unlock");
        CollectionAssert.Contains(back.run.unlockedIds, "hardy_paws");
    }

    [Test]
    public void OldSavesWithRemovedCropsStillLoad()
    {
        RunState run = FreshRun(db, 6);
        run.inventory.Add(new SlotSave("frostberry", 3, 1f));
        SaveFile file = new SaveFile { hasRun = true, run = run };
        SaveFile back = JsonUtility.FromJson<SaveFile>(JsonUtility.ToJson(file));
        Assert.DoesNotThrow(() => SaveService.Sanitize(back, db));
    }
}
