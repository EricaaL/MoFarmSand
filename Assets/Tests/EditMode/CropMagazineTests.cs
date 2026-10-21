using NUnit.Framework;

public class CropMagazineTests
{
    private CropData peas;
    private CropData corn;
    private CropMagazine mag;
    private int stack;

    [SetUp]
    public void SetUp()
    {
        peas = TestUtil.Make<CropData>(); peas.cropId = "peas"; peas.type = CropType.Shooter; peas.ammoPerCrop = 10;
        corn = TestUtil.Make<CropData>(); corn.cropId = "corn"; corn.type = CropType.Shooter; corn.ammoPerCrop = 4;
        mag = new CropMagazine();
        stack = 0;
    }

    private bool Load()
    {
        if (stack <= 0) return false;
        stack--;
        return true;
    }

    [Test]
    public void OneCropGivesTenShotsThenNeedsAnotherCrop()
    {
        stack = 2;
        for (int i = 0; i < 10; i++) Assert.IsTrue(mag.TryFire(peas, Load, 1f, out _), $"shot {i + 1}");
        Assert.AreEqual(1, stack, "only one crop was used for the first 10 shots");
        Assert.AreEqual(0, mag.Loaded(peas));

        Assert.IsTrue(mag.TryFire(peas, Load, 1f, out _), "the 11th shot loads the second crop");
        Assert.AreEqual(0, stack);
        Assert.AreEqual(9, mag.Loaded(peas));
    }

    [Test]
    public void CannotFireWithNothingLoadedAndNothingToLoad()
    {
        stack = 0;
        Assert.IsFalse(mag.CanFire(peas, stack));
        Assert.IsFalse(mag.TryFire(peas, Load, 1f, out _));
        Assert.AreEqual(0, mag.Loaded(peas));
    }

    [Test]
    public void LoadedRoundsKeepFiringEvenWhenTheStackIsEmpty()
    {
        stack = 1;
        Assert.IsTrue(mag.TryFire(peas, Load, 1f, out _));
        Assert.AreEqual(0, stack);
        Assert.IsTrue(mag.CanFire(peas, stack), "9 rounds are still in the magazine");
        for (int i = 0; i < 9; i++) Assert.IsTrue(mag.TryFire(peas, Load, 1f, out _));
        Assert.IsFalse(mag.CanFire(peas, stack));
    }

    [Test]
    public void BulletsCarryTheQualityOfTheCropThatWasLoaded()
    {
        stack = 2;
        mag.TryFire(peas, Load, 0.5f, out float first);
        Assert.AreEqual(0.5f, first);
        mag.TryFire(peas, Load, 1.2f, out float second); // same magazine: still the first crop's strength
        Assert.AreEqual(0.5f, second);
    }

    [Test]
    public void SwitchingCropsLoadsTheOtherCropFresh()
    {
        stack = 3;
        mag.TryFire(peas, Load, 1f, out _);
        Assert.AreEqual(9, mag.Loaded(peas));
        Assert.AreEqual(0, mag.Loaded(corn), "the peas rounds are not corn rounds");

        Assert.IsTrue(mag.TryFire(corn, Load, 1f, out _));
        Assert.AreEqual(3, mag.Loaded(corn), "corn holds 4 shots, one just fired");
        Assert.AreEqual(1, stack);
    }

    [Test]
    public void ClearEmptiesTheMagazine()
    {
        stack = 1;
        mag.TryFire(peas, Load, 1f, out _);
        mag.Clear();
        Assert.AreEqual(0, mag.Loaded(peas));
    }
}
