using UnityEngine;

/// <summary>Countdown with an "unlimited" mode (duration &lt;= 0). Pure: the caller supplies the delta time.</summary>
public sealed class DayTimer
{
    public float Duration { get; private set; }
    public float Remaining { get; private set; }
    public bool Unlimited => Duration <= 0f;

    /// <summary>0 at the start, 1 when time is up. Always 0 when unlimited.</summary>
    public float Progress01 => Unlimited ? 0f : 1f - Mathf.Clamp01(Remaining / Duration);

    public void Start(float duration)
    {
        Duration = duration;
        Remaining = Mathf.Max(0f, duration);
    }

    /// <summary>Advances the clock. Returns true exactly once, on the tick that runs it out.</summary>
    public bool Tick(float dt)
    {
        if (Unlimited || Remaining <= 0f) return false;
        Remaining = Mathf.Max(0f, Remaining - Mathf.Max(0f, dt));
        return Remaining <= 0f;
    }
}
