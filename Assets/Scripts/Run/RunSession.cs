using System.IO;
using UnityEngine;

/// <summary>
/// Survives scene reloads. Holds the active slot and its save file in memory; every day boundary
/// commits it to disk, so the in-memory copy and the file never drift far apart.
/// </summary>
public static class RunSession
{
    public static int ActiveSlot = -1;
    public static SaveFile Current;
    private static SaveService service;

    public static SaveService Service =>
        service ??= new SaveService(new FileSaveStore(Path.Combine(Application.persistentDataPath, "saves")));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        ActiveSlot = -1;
        Current = null;
        service = null;
    }

    public static bool HasActiveSlot => ActiveSlot >= 0 && Current != null;

    /// <summary>Writes the current file to its slot. Returns false if there is nothing to write or the slot is from a newer game.</summary>
    public static bool Commit()
    {
        if (!HasActiveSlot) return false;
        return Service.Save(ActiveSlot, Current);
    }

    public static void Unload()
    {
        ActiveSlot = -1;
        Current = null;
    }
}
