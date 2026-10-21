using System;

[Serializable]
public class SaveFile
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public string savedAtUtc;
    public MetaProgress meta = new MetaProgress();
    /// <summary>JsonUtility can't store null for nested classes, so check this instead of run == null.</summary>
    public bool hasRun;
    public RunState run = new RunState();
}
