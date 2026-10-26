using UnityEngine;

/// <summary>
/// A change to the saved run state (events, rest stops, unlocks). Pure: it edits <see cref="RunState"/>,
/// which is restored into the scene afterwards, so effects stay testable without a scene.
/// </summary>
public abstract class RunEffect : ScriptableObject
{
    public abstract void Apply(RunState run, GameDatabase db);

    /// <summary>Run-start variant that also sees the year just finished (null on the very first year). Most effects ignore it.</summary>
    public virtual void ApplyAtYearStart(RunState run, GameDatabase db, RunState previous) { Apply(run, db); }

    /// <summary>Short text for event buttons and tooltips.</summary>
    public abstract string Describe();
}
