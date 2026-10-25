using NUnit.Framework;

public class HeldItemTextTests
{
    private static CropData Shooter(int ammo = 10)
    {
        CropData c = TestUtil.Make<CropData>();
        c.cropId = "peas"; c.type = CropType.Shooter; c.ammoPerCrop = ammo;
        return c;
    }

    private static CropData Crop(CropType type)
    {
        CropData c = TestUtil.Make<CropData>();
        c.cropId = "x"; c.type = type;
        return c;
    }

    [Test]
    public void EmptySlotIsHidden()
    {
        Assert.IsFalse(HeldItemText.Format(null, 5, 0).visible);
        Assert.IsFalse(HeldItemText.Format(Crop(CropType.Projectile), 0, 0).visible, "a spent tomato stack shows nothing");
    }

    [Test]
    public void ShooterShowsLoadedOverMagazineAndSpareCrops()
    {
        HeldItemLabel l = HeldItemText.Format(Shooter(), 3, 7);
        Assert.IsTrue(l.visible);
        Assert.AreEqual("7 / 10", l.main);
        Assert.AreEqual("+3", l.sub);
        Assert.IsFalse(l.low);
        Assert.IsFalse(l.outOfAmmo);
        Assert.AreEqual("+1", HeldItemText.Format(Shooter(), 1, 7).sub);
    }

    [Test]
    public void ShooterWithAnEmptyMagazineShowsTheNextCropAsFullyLoaded()
    {
        // Fresh peas: nothing fired yet, so nothing is "loaded", but the first shot loads a crop. Show 10 / 10.
        HeldItemLabel l = HeldItemText.Format(Shooter(), 3, 0);
        Assert.AreEqual("10 / 10", l.main);
        Assert.AreEqual("+2", l.sub, "one of the three crops is the one that gets loaded");
        Assert.IsFalse(l.low);
        Assert.IsFalse(l.outOfAmmo);

        HeldItemLabel single = HeldItemText.Format(Shooter(), 1, 0);
        Assert.AreEqual("10 / 10", single.main);
        Assert.AreEqual(string.Empty, single.sub);
        Assert.IsFalse(single.low, "10 rounds is not low");
    }

    [Test]
    public void ShooterIsLowOnlyWhenFewRoundsAndNoSpareCrops()
    {
        Assert.IsTrue(HeldItemText.Format(Shooter(), 0, HeldItemText.LowRounds).low);
        Assert.IsFalse(HeldItemText.Format(Shooter(), 0, HeldItemText.LowRounds + 1).low);
        Assert.IsFalse(HeldItemText.Format(Shooter(), 1, 1).low, "a spare crop means more ammo is coming");
        Assert.AreEqual(string.Empty, HeldItemText.Format(Shooter(), 0, 2).sub);
    }

    [Test]
    public void ShooterWithNothingLeftIsOutOfAmmoButStillVisible()
    {
        HeldItemLabel l = HeldItemText.Format(Shooter(), 0, 0);
        Assert.IsTrue(l.visible);
        Assert.IsTrue(l.outOfAmmo);
        Assert.IsTrue(l.low);
        Assert.AreEqual("Out of ammo", l.main);
    }

    [Test]
    public void MagazineSizeComesFromTheCrop()
    {
        Assert.AreEqual("3 / 4", HeldItemText.Format(Shooter(4), 0, 3).main);
    }

    [Test]
    public void ThrownAndShieldCropsShowACount()
    {
        Assert.AreEqual("x4", HeldItemText.Format(Crop(CropType.Projectile), 4, 0).main);
        Assert.AreEqual("x4", HeldItemText.Format(Crop(CropType.Support), 4, 0).main);
        Assert.AreEqual(string.Empty, HeldItemText.Format(Crop(CropType.Projectile), 4, 0).sub);
        Assert.IsFalse(HeldItemText.Format(Crop(CropType.Projectile), 4, 0).low);
    }

    [Test]
    public void SeedsShowJustTheCount()
    {
        SeedData seed = TestUtil.Make<SeedData>(); seed.seedId = "seed_x";
        HeldItemLabel l = HeldItemText.Format(seed, 4, 0);
        Assert.AreEqual("x4", l.main);
        Assert.AreEqual(string.Empty, l.sub);
        Assert.AreEqual(string.Empty, HeldItemText.Format(seed, 1, 0).sub);
    }
}
