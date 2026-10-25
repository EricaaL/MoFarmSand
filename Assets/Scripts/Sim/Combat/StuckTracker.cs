/// <summary>
/// Spots a mover that keeps pushing but barely moves (wedged between scenery) and asks it to sidestep for a moment.
/// Pure: the caller reports how far it meant to move and how far it did.
/// </summary>
public class StuckTracker
{
    private readonly float progressRatio;
    private readonly float stuckSeconds;
    private readonly float escapeSeconds;
    private float stuckFor;
    private float escapeLeft;

    public StuckTracker(float progressRatio = 0.25f, float stuckSeconds = 0.4f, float escapeSeconds = 0.8f)
    {
        this.progressRatio = progressRatio;
        this.stuckSeconds = stuckSeconds;
        this.escapeSeconds = escapeSeconds;
        Side = 1;
    }

    /// <summary>True while the mover should steer along the obstacle instead of at its goal.</summary>
    public bool Escaping => escapeLeft > 0f;

    /// <summary>+1 or -1: which way round the obstacle to sidestep. Flips every time an escape starts.</summary>
    public int Side { get; private set; }

    /// <summary>Call once per moving frame. Returns true on the frame an escape begins.</summary>
    public bool Tick(float dt, float intended, float moved)
    {
        if (escapeLeft > 0f)
        {
            escapeLeft -= dt;
            return false;
        }
        if (intended <= 0.0001f || moved >= intended * progressRatio)
        {
            stuckFor = 0f;
            return false;
        }
        stuckFor += dt;
        if (stuckFor < stuckSeconds) return false;
        stuckFor = 0f;
        escapeLeft = escapeSeconds;
        Side = -Side;
        return true;
    }

    /// <summary>Forget any progress, e.g. while the mover is deliberately standing still.</summary>
    public void Reset()
    {
        stuckFor = 0f;
        escapeLeft = 0f;
    }
}
