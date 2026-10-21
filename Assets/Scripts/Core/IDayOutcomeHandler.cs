/// <summary>
/// Receives the end of a day (Won, Lost or DayEnd) from <see cref="GameManager"/>. When none is set,
/// GameManager keeps its legacy behavior and simply reloads the scene.
/// </summary>
public interface IDayOutcomeHandler
{
    void OnDayResolved(GamePhase outcome);
}

/// <summary>Per-day rules set by the run director. Defaults reproduce the original single-boss day.</summary>
public struct DayRules
{
    public bool hasEncounter;
    /// <summary>Losing tonight's fight doesn't end the year (a season boss): the HUD says so.</summary>
    public bool survivableLoss;

    public static DayRules Default => new DayRules { hasEncounter = true };
}
