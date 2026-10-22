/// <summary>What the throw layer should do this frame.</summary>
public enum ThrowPoseAction
{
    /// <summary>Nothing changed.</summary>
    None,
    /// <summary>The player started aiming: raise the arm to the aim pose and hold it.</summary>
    Hold,
    /// <summary>The aim ended in a throw: let the held pose swing through.</summary>
    Release,
    /// <summary>The aim ended with no throw (cancelled, switched crop, paused): drop the arm.</summary>
    Cancel,
    /// <summary>A throw with no aim before it (e.g. dragged off the hotbar): play the swing from its start.</summary>
    Swing
}

/// <summary>
/// Turns "is the player aiming" and "did a crop just leave the hand" into throw-layer actions. Pure logic: PlayerAnimator
/// feeds it each frame and plays the matching animation.
/// </summary>
public sealed class ThrowPoseTracker
{
    /// <summary>True while the aim pose is being held.</summary>
    public bool Holding { get; private set; }

    public ThrowPoseAction Step(bool aiming, bool fired)
    {
        if (!Holding)
        {
            if (aiming) { Holding = true; return ThrowPoseAction.Hold; }
            return fired ? ThrowPoseAction.Swing : ThrowPoseAction.None;
        }

        if (aiming) return ThrowPoseAction.None;
        Holding = false;
        return fired ? ThrowPoseAction.Release : ThrowPoseAction.Cancel;
    }

    public void Reset() { Holding = false; }
}
