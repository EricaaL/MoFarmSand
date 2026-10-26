using UnityEngine;

/// <summary>
/// Keeps the game running when its window loses focus (the Player Settings flag does the same for builds;
/// this is the safeguard in case that flag is reset).
/// </summary>
public static class KeepRunningInBackground
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply() { Application.runInBackground = true; }
}
