using NUnit.Framework;
using UnityEngine;

public class GrowthStageTests
{
    private CropData crop;

    [SetUp]
    public void SetUp()
    {
        crop = TestUtil.Make<CropData>();
        crop.growthStages = new GameObject[3];
    }

    [TearDown]
    public void TearDown() { if (crop != null) Object.DestroyImmediate(crop); }

    [Test]
    public void ThreeStagesSplitGrowingInHalfAndReserveTheLastForHarvest()
    {
        Assert.AreEqual(0, crop.StageIndexFor(0f, false));
        Assert.AreEqual(0, crop.StageIndexFor(0.49f, false));
        Assert.AreEqual(1, crop.StageIndexFor(0.5f, false));
        Assert.AreEqual(1, crop.StageIndexFor(0.99f, false));
        Assert.AreEqual(2, crop.StageIndexFor(1f, true));
    }

    [Test]
    public void NoStagesAlwaysReturnsZero()
    {
        crop.growthStages = new GameObject[0];
        Assert.AreEqual(0, crop.StageIndexFor(0.7f, false));
        Assert.AreEqual(0, crop.StageIndexFor(1f, true));
    }
}
