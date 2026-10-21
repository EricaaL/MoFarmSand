using NUnit.Framework;

public class SnowAccumulationTests
{
    [Test]
    public void NoSnowWhenStartAndEndAreZero()
    {
        Assert.AreEqual(0f, SnowAccumulation.Amount(0f, 0f, 0.5f, 0.5f, 0.2f));
    }

    [Test]
    public void ReachesEndAtSeasonEnd()
    {
        Assert.AreEqual(1f, SnowAccumulation.Amount(0.35f, 1f, 1f, 0f, 0.2f), 0.0001f);
    }

    [Test]
    public void GrowsOverSeasonAndDay()
    {
        float earlier = SnowAccumulation.Amount(0.35f, 1f, 0.2f, 0f, 0.2f);
        float later = SnowAccumulation.Amount(0.35f, 1f, 0.6f, 0f, 0.2f);
        float laterInDay = SnowAccumulation.Amount(0.35f, 1f, 0.6f, 1f, 0.2f);
        Assert.Less(earlier, later);
        Assert.Less(later, laterInDay);
    }

    [Test]
    public void StaysClamped()
    {
        Assert.AreEqual(1f, SnowAccumulation.Amount(0.9f, 2f, 1f, 1f, 1f));
        Assert.AreEqual(0f, SnowAccumulation.Amount(-1f, 0f, 0f, 0f, 0f));
    }
}
