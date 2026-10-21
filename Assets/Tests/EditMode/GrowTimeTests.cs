using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GrowTimeTests
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

    [Test]
    public void CapRateNeverLetsACropFinishFasterThanTheMinimum()
    {
        ClimateConfig cfg = TestUtil.Make<ClimateConfig>();
        created.Add(cfg);
        cfg.minGrowSeconds = 10f;

        Assert.AreEqual(1.1f, ClimateModel.CapRate(11f, 1.6f, cfg), 1e-4f, "11s at x1.6 would be 6.9s: capped to 10s");
        Assert.AreEqual(0.5f, ClimateModel.CapRate(11f, 0.5f, cfg), 1e-4f, "slow growth is left alone");
        Assert.AreEqual(3f, ClimateModel.CapRate(11f, 3f, null) , 1e-4f, "no config means no cap");
    }

    [Test]
    public void EveryRealCropTakesAtLeastTenSecondsEvenWithEveryBoost()
    {
        Assert.GreaterOrEqual(db.climate.minGrowSeconds, 10f);
        float bestPossibleRate = db.climate.maxGrowth * 1.4f * 1.5f; // best season, rest-stop bonus, fertilizer-style boost
        foreach (ItemData item in db.items.All)
        {
            if (!(item is CropData crop)) continue;
            float seconds = crop.growTime / ClimateModel.CapRate(crop.growTime, bestPossibleRate, db.climate);
            Assert.GreaterOrEqual(seconds, 10f - 1e-3f, $"{crop.displayName} could be harvested in {seconds:0.0}s");
            Assert.GreaterOrEqual(crop.growTime, 10f, $"{crop.displayName}'s base time should already be 10s or more");
        }
    }
}
