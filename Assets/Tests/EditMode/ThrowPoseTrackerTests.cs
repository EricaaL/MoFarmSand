using NUnit.Framework;

public class ThrowPoseTrackerTests
{
    [Test]
    public void NothingHappensWhileIdle()
    {
        ThrowPoseTracker t = new ThrowPoseTracker();
        Assert.AreEqual(ThrowPoseAction.None, t.Step(false, false));
        Assert.IsFalse(t.Holding);
    }

    [Test]
    public void StartingToAimHoldsThePoseOnceAndThenStaysQuiet()
    {
        ThrowPoseTracker t = new ThrowPoseTracker();
        Assert.AreEqual(ThrowPoseAction.Hold, t.Step(true, false));
        Assert.IsTrue(t.Holding);
        for (int i = 0; i < 5; i++) Assert.AreEqual(ThrowPoseAction.None, t.Step(true, false), "no restarts while the button stays down");
    }

    [Test]
    public void ReleasingWithAThrowSwingsThroughAndStopsHolding()
    {
        ThrowPoseTracker t = new ThrowPoseTracker();
        t.Step(true, false);
        Assert.AreEqual(ThrowPoseAction.Release, t.Step(false, true));
        Assert.IsFalse(t.Holding);
        Assert.AreEqual(ThrowPoseAction.None, t.Step(false, false));
    }

    [Test]
    public void AbandoningTheAimDropsTheArmWithoutAThrow()
    {
        ThrowPoseTracker t = new ThrowPoseTracker();
        t.Step(true, false);
        Assert.AreEqual(ThrowPoseAction.Cancel, t.Step(false, false), "right-click, switched slot, out of ammo or paused");
        Assert.IsFalse(t.Holding);
    }

    [Test]
    public void AThrowWithNoAimBeforeItPlaysTheSwingFromItsStart()
    {
        ThrowPoseTracker t = new ThrowPoseTracker();
        Assert.AreEqual(ThrowPoseAction.Swing, t.Step(false, true), "e.g. dragged off the hotbar");
        Assert.IsFalse(t.Holding);
    }

    [Test]
    public void AimingAgainRightAfterAThrowHoldsAgain()
    {
        ThrowPoseTracker t = new ThrowPoseTracker();
        t.Step(true, false);
        t.Step(false, true);
        Assert.AreEqual(ThrowPoseAction.Hold, t.Step(true, false));
    }

    [Test]
    public void ResetForgetsAHeldPose()
    {
        ThrowPoseTracker t = new ThrowPoseTracker();
        t.Step(true, false);
        t.Reset();
        Assert.IsFalse(t.Holding);
        Assert.AreEqual(ThrowPoseAction.None, t.Step(false, false));
    }
}
