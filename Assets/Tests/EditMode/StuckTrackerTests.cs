using NUnit.Framework;

public class StuckTrackerTests
{
    private const float Dt = 0.1f;

    [Test]
    public void FreeMovementIsNeverStuck()
    {
        StuckTracker t = new StuckTracker();
        for (int i = 0; i < 100; i++) Assert.IsFalse(t.Tick(Dt, 0.1f, 0.1f));
        Assert.IsFalse(t.Escaping);
    }

    [Test]
    public void PushingWithoutMovingStartsAnEscapeAfterTheStuckTime()
    {
        StuckTracker t = new StuckTracker(stuckSeconds: 0.4f);
        bool started = false;
        int frames = 0;
        while (!started && frames < 20) { started = t.Tick(Dt, 0.1f, 0f); frames++; }

        Assert.IsTrue(started);
        Assert.AreEqual(4, frames, "0.4 s of 0.1 s frames");
        Assert.IsTrue(t.Escaping);
    }

    [Test]
    public void SlowProgressBelowTheRatioCountsAsStuck()
    {
        StuckTracker t = new StuckTracker(progressRatio: 0.25f, stuckSeconds: 0.2f);
        t.Tick(Dt, 0.1f, 0.01f);
        Assert.IsTrue(t.Tick(Dt, 0.1f, 0.01f));
    }

    [Test]
    public void MovingAgainResetsTheStuckTimer()
    {
        StuckTracker t = new StuckTracker(stuckSeconds: 0.4f);
        t.Tick(Dt, 0.1f, 0f); t.Tick(Dt, 0.1f, 0f); t.Tick(Dt, 0.1f, 0f);
        t.Tick(Dt, 0.1f, 0.1f);
        Assert.IsFalse(t.Tick(Dt, 0.1f, 0f), "the count starts over");
        Assert.IsFalse(t.Escaping);
    }

    [Test]
    public void TheEscapeEndsAndTheNextOneSidestepsTheOtherWay()
    {
        StuckTracker t = new StuckTracker(stuckSeconds: 0.2f, escapeSeconds: 0.5f);
        t.Tick(Dt, 0.1f, 0f);
        Assert.IsTrue(t.Tick(Dt, 0.1f, 0f));
        int first = t.Side;

        for (int i = 0; i < 10 && t.Escaping; i++) t.Tick(Dt, 0.1f, 0f);
        Assert.IsFalse(t.Escaping, "it gives the chase another go");

        t.Tick(Dt, 0.1f, 0f);
        Assert.IsTrue(t.Tick(Dt, 0.1f, 0f));
        Assert.AreEqual(-first, t.Side);
    }

    [Test]
    public void NotIntendingToMoveIsNotStuckAndResetClearsAnEscape()
    {
        StuckTracker t = new StuckTracker(stuckSeconds: 0.2f);
        for (int i = 0; i < 20; i++) Assert.IsFalse(t.Tick(Dt, 0f, 0f));

        t.Tick(Dt, 0.1f, 0f);
        t.Tick(Dt, 0.1f, 0f);
        Assert.IsTrue(t.Escaping);
        t.Reset();
        Assert.IsFalse(t.Escaping);
    }
}
