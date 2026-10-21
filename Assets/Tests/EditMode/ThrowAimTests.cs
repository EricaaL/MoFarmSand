using NUnit.Framework;

public class ThrowAimTests
{
    private static ThrowAimInput Input(bool canAim = true, bool pressed = false, bool held = false, bool released = false, bool cancel = false, bool cooldown = true)
        => new ThrowAimInput { canAim = canAim, pressedThisFrame = pressed, held = held, releasedThisFrame = released, cancelPressed = cancel, cooldownReady = cooldown };

    [Test]
    public void PressingStartsAimingAndHoldingKeepsTheArcUp()
    {
        ThrowAim aim = new ThrowAim();

        Assert.AreEqual(ThrowAimResult.None, aim.Step(Input()), "idle");
        Assert.AreEqual(ThrowAimResult.Aiming, aim.Step(Input(pressed: true, held: true)));
        Assert.IsTrue(aim.IsAiming);
        Assert.AreEqual(ThrowAimResult.Aiming, aim.Step(Input(held: true)), "still held: keep showing the arc");
        Assert.AreEqual(ThrowAimResult.Aiming, aim.Step(Input(held: true)));
    }

    [Test]
    public void ReleasingThrowsExactlyOnce()
    {
        ThrowAim aim = new ThrowAim();
        aim.Step(Input(pressed: true, held: true));

        Assert.AreEqual(ThrowAimResult.Throw, aim.Step(Input(released: true)));
        Assert.IsFalse(aim.IsAiming);
        Assert.AreEqual(ThrowAimResult.None, aim.Step(Input()), "no second throw");
        Assert.AreEqual(ThrowAimResult.None, aim.Step(Input(released: true)), "a stray release does nothing");
    }

    [Test]
    public void PressingDoesNotThrowOnTheSameFrame()
    {
        // The old behavior threw on click; now a click only starts the aim.
        ThrowAim aim = new ThrowAim();
        Assert.AreNotEqual(ThrowAimResult.Throw, aim.Step(Input(pressed: true, held: true)));
    }

    [Test]
    public void ARealClickAndReleaseWithinOneFrameStillCountsAsAnAimThenThrow()
    {
        // A very quick tap: pressed this frame and released next frame.
        ThrowAim aim = new ThrowAim();
        Assert.AreEqual(ThrowAimResult.Aiming, aim.Step(Input(pressed: true, held: true)));
        Assert.AreEqual(ThrowAimResult.Throw, aim.Step(Input(released: true)));
    }

    [Test]
    public void RightClickCancelsWithoutThrowingAndTheLaterReleaseIsIgnored()
    {
        ThrowAim aim = new ThrowAim();
        aim.Step(Input(pressed: true, held: true));

        Assert.AreEqual(ThrowAimResult.Cancelled, aim.Step(Input(held: true, cancel: true)));
        Assert.IsFalse(aim.IsAiming);
        Assert.AreEqual(ThrowAimResult.None, aim.Step(Input(held: true)), "still holding after cancelling: nothing happens");
        Assert.AreEqual(ThrowAimResult.None, aim.Step(Input(released: true)), "letting go after a cancel doesn't throw");
    }

    [Test]
    public void LosingTheAbilityToAimCancelsInsteadOfThrowing()
    {
        // Switching to another hotbar slot, running out of crops, or a menu opening all make canAim false.
        ThrowAim aim = new ThrowAim();
        aim.Step(Input(pressed: true, held: true));

        Assert.AreEqual(ThrowAimResult.Cancelled, aim.Step(Input(canAim: false, held: true)));
        Assert.IsFalse(aim.IsAiming);
        Assert.AreEqual(ThrowAimResult.None, aim.Step(Input(canAim: true, released: true)), "the release afterwards is ignored");
    }

    [Test]
    public void CannotStartAimingWhenYouCannotAim()
    {
        ThrowAim aim = new ThrowAim();
        Assert.AreEqual(ThrowAimResult.None, aim.Step(Input(canAim: false, pressed: true, held: true)), "no ammo, menu open, or a non-throwable crop");
        Assert.IsFalse(aim.IsAiming);
    }

    [Test]
    public void ReleasingDuringTheCooldownCancelsInsteadOfThrowing()
    {
        ThrowAim aim = new ThrowAim();
        aim.Step(Input(pressed: true, held: true));
        Assert.AreEqual(ThrowAimResult.Cancelled, aim.Step(Input(released: true, cooldown: false)));
        Assert.IsFalse(aim.IsAiming);
    }

    [Test]
    public void AMissedReleaseEventStillThrowsOnceTheButtonIsNoLongerHeld()
    {
        // E.g. the release happened during a frame where input was disabled.
        ThrowAim aim = new ThrowAim();
        aim.Step(Input(pressed: true, held: true));
        Assert.AreEqual(ThrowAimResult.Throw, aim.Step(Input(held: false)));
    }

    [Test]
    public void ResetDropsAnAimInProgress()
    {
        ThrowAim aim = new ThrowAim();
        aim.Step(Input(pressed: true, held: true));
        aim.Reset();
        Assert.IsFalse(aim.IsAiming);
        Assert.AreEqual(ThrowAimResult.None, aim.Step(Input(released: true)));
    }

    [Test]
    public void YouCanAimAgainStraightAfterThrowing()
    {
        ThrowAim aim = new ThrowAim();
        aim.Step(Input(pressed: true, held: true));
        Assert.AreEqual(ThrowAimResult.Throw, aim.Step(Input(released: true)));

        Assert.AreEqual(ThrowAimResult.Aiming, aim.Step(Input(pressed: true, held: true)));
        Assert.AreEqual(ThrowAimResult.Throw, aim.Step(Input(released: true)));
    }
}
