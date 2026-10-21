using UnityEngine;

/// <summary>
/// A change to the saved run state (events, rest stops, unlocks). Pure: it edits <see cref="RunState"/>,
/// which is restored into the scene afterwards, so effects stay testable without a scene.
/// </summary>
public abstract class RunEffect : ScriptableObject
{
    public abstract void Apply(RunState run, GameDatabase db);

    /// <summary>Short text for event buttons and tooltips.</summary>
    public abstract string Describe();
}
