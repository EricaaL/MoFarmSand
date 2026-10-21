using NUnit.Framework;
using UnityEngine;

public class PieMenuMathTests
{
    [Test]
    public void InsideDeadZoneSelectsNothing()
    {
        Assert.AreEqual(-1, PieMenuMath.SliceAt(new Vector2(5f, 5f), 3, 40f));
        Assert.AreEqual(-1, PieMenuMath.SliceAt(Vector2.zero, 3, 0.5f));
    }

    [Test]
    public void ThreeSlicesStartAtTopAndGoClockwise()
    {
        Assert.AreEqual(0, PieMenuMath.SliceAt(new Vector2(0f, -100f), 3, 10f), "up");
        Assert.AreEqual(1, PieMenuMath.SliceAt(new Vector2(87f, 50f), 3, 10f), "down-right (120 degrees)");
        Assert.AreEqual(2, PieMenuMath.SliceAt(new Vector2(-87f, 50f), 3, 10f), "down-left (240 degrees)");
    }

    [Test]
    public void FourSlicesCoverEveryDirection()
    {
        Assert.AreEqual(0, PieMenuMath.SliceAt(new Vector2(0f, -100f), 4, 10f));
        Assert.AreEqual(1, PieMenuMath.SliceAt(new Vector2(100f, 0f), 4, 10f));
        Assert.AreEqual(2, PieMenuMath.SliceAt(new Vector2(0f, 100f), 4, 10f));
        Assert.AreEqual(3, PieMenuMath.SliceAt(new Vector2(-100f, 0f), 4, 10f));
    }

    [Test]
    public void SliceZeroWrapsAcrossTheTopSeam()
    {
        Assert.AreEqual(0, PieMenuMath.SliceAt(new Vector2(-20f, -100f), 3, 10f), "slightly left of up");
        Assert.AreEqual(0, PieMenuMath.SliceAt(new Vector2(20f, -100f), 3, 10f), "slightly right of up");
    }

    [Test]
    public void SliceCentersAreEvenlySpaced()
    {
        Assert.AreEqual(0f, PieMenuMath.SliceCenterAngle(0, 3), 0.001f);
        Assert.AreEqual(120f, PieMenuMath.SliceCenterAngle(1, 3), 0.001f);
        Assert.AreEqual(240f, PieMenuMath.SliceCenterAngle(2, 3), 0.001f);
        Vector2 up = PieMenuMath.SliceDirection(0, 3);
        Assert.AreEqual(-1f, up.y, 0.001f, "screen y is down, so up is negative");
    }
}
