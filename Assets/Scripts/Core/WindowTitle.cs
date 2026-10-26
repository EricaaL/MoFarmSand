using System.Collections;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Windows builds show "Dogsend" in the title bar. The product name stays MoFarmSand because the save folder and
/// PlayerPrefs are keyed on it, so the title is set directly on the game window instead.
/// </summary>
public class WindowTitle : MonoBehaviour
{
    public const string Title = "Dogsend";

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("user32.dll")] private static extern System.IntPtr GetActiveWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowText(System.IntPtr hWnd, string text);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        GameObject go = new GameObject("WindowTitle");
        DontDestroyOnLoad(go);
        go.AddComponent<WindowTitle>();
    }

    private IEnumerator Start()
    {
        // The window can take a frame or two to become the active one. It is set again now and then, in case a display mode change rebuilds it.
        while (true)
        {
            System.IntPtr window = GetActiveWindow();
            if (window != System.IntPtr.Zero) { SetWindowText(window, Title); yield return new WaitForSecondsRealtime(5f); }
            else yield return null;
        }
    }
#else
    private IEnumerator Start() { yield break; }
#endif
}
