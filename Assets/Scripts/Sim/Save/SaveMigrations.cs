using System.Collections.Generic;

public interface ISaveMigration
{
    /// <summary>The version this step upgrades from. It leaves the file at From + 1.</summary>
    int From { get; }
    void Apply(SaveFile file);
}

/// <summary>Ordered upgrade steps. JsonUtility fills missing fields with defaults, so most steps only derive values.</summary>
public static class SaveMigrations
{
    public static readonly List<ISaveMigration> Steps = new List<ISaveMigration> { new SixDaySeasonsStep() };

    public static void Apply(SaveFile file) => Apply(file, Steps);

    public static void Apply(SaveFile file, IReadOnlyList<ISaveMigration> steps)
    {
        bool progressed = true;
        while (progressed && file.version < SaveFile.CurrentVersion)
        {
            progressed = false;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].From != file.version) continue;
                steps[i].Apply(file);
                file.version++;
                progressed = true;
                break;
            }
        }
        // Without a registered step the file is still brought forward: missing fields already hold defaults.
        if (file.version < SaveFile.CurrentVersion) file.version = SaveFile.CurrentVersion;
    }
}

/// <summary>v1 years had 3-day seasons and one boss. Their saved maps don't fit the 6-day calendar, so the unfinished year is dropped (meta is kept).</summary>
public sealed class SixDaySeasonsStep : ISaveMigration
{
    public int From => 1;

    public void Apply(SaveFile file)
    {
        if (!file.hasRun) return;
        file.hasRun = false;
        file.run = new RunState();
    }
}
