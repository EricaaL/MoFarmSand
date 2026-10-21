/// <summary>What happened this frame while the player aims a thrown crop.</summary>
public enum ThrowAimResult
{
    /// <summary>Not aiming and nothing to do.</summary>
    None,
    /// <summary>Button held: show the arc.</summary>
    Aiming,
    /// <summary>Button released while aiming: throw now.</summary>
    Throw,
    /// <summary>The aim was abandoned (cancelled, switched crop, ran out, paused...). Hide the arc, throw nothing.</summary>
    Cancelled
}

/// <summary>Inputs for one frame of aiming. Plain data so the state machine stays testable.</summary>
public struct ThrowAimInput
{
    /// <summary>The player can act and holds a throwable crop with ammo.</summary>
    public bool canAim;
    public bool pressedThisFrame;
    public bool held;
    public bool releasedThisFrame;
    public bool cancelPressed;
    public bool cooldownReady;
}

/// <summary>Hold to aim, release to throw. Pure logic: PlayerCombat feeds it input and acts on the result.</summary>
public sealed class ThrowAim
{
    public bool IsAiming { get; private set; }

    public ThrowAimResult Step(ThrowAimInput i)
    {
        if (!IsAiming)
        {
            if (i.canAim && i.pressedThisFrame && i.held)
            {
                IsAiming = true;
                return ThrowAimResult.Aiming;
            }
            return ThrowAimResult.None;
        }

        // Anything that makes aiming impossible ends it without throwing: paused, switched slot, no ammo.
        if (!i.canAim || i.cancelPressed)
        {
            IsAiming = false;
            return ThrowAimResult.Cancelled;
        }

        // Released (or the release was missed and the button is simply no longer held): throw.
        if (i.releasedThisFrame || !i.held)
        {
            IsAiming = false;
            return i.cooldownReady ? ThrowAimResult.Throw : ThrowAimResult.Cancelled;
        }

        return ThrowAimResult.Aiming;
    }

    public void Reset() { IsAiming = false; }
}
