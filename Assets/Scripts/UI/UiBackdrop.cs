using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The blurred live game view drawn behind full-screen menus (the Canva mockups put a blurred picture of the farm there).
/// The main camera is rendered into a tiny render texture (that is the blur: bilinear-stretched, then softened by a second
/// downsample) from a hidden runner's LateUpdate, never from inside OnGUI. Presentation only.
/// </summary>
public static class UiBackdrop
{
    private const int Divisor = 4;       // first render: screen / 4
    private const int BlurDivisor = 14;  // soft copy the menus actually show

    private static RenderTexture sharp, soft;
    private static int lastWantedFrame = -10;
    private static float lastCapture = -100f;
    private static float interval;
    private static bool hasImage;
    private static Runner runner;

    /// <summary>Re-capture interval for menus over the turning title panorama.</summary>
    public const float LiveInterval = 0.1f;

    /// <summary>Forget the picture so the next screen captures the world as it is now (call when a screen opens over live gameplay).</summary>
    public static void Invalidate() { hasImage = false; }

    /// <summary>Draws the backdrop full-screen. <paramref name="refreshSeconds"/> &gt; 0 keeps re-capturing (a live menu); 0 keeps one picture. False if there is none yet.</summary>
    public static bool Draw(float refreshSeconds)
    {
        EnsureRunner();
        lastWantedFrame = Time.frameCount;
        interval = refreshSeconds;
        if (!hasImage || soft == null || !soft.IsCreated()) return false;
        if (Event.current.type == EventType.Repaint)
            GUI.DrawTexture(ImGuiKit.ScreenLocal, soft, ScaleMode.StretchToFill, false);
        return true;
    }

    private static void EnsureRunner()
    {
        if (runner != null) return;
        GameObject go = new GameObject("UiBackdropRunner") { hideFlags = HideFlags.HideInHierarchy };
        Object.DontDestroyOnLoad(go);   // survives the day's scene reload, and is cleaned up when Play mode ends
        runner = go.AddComponent<Runner>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sharp = null; soft = null; runner = null; hasImage = false; lastWantedFrame = -10; lastCapture = -100f;
    }

    private static void Capture()
    {
        Camera cam = Camera.main;
        if (cam == null || GraphicsSettings.currentRenderPipeline == null) return;

        int w = Mathf.Max(64, Screen.width / Divisor), h = Mathf.Max(36, Screen.height / Divisor);
        Ensure(ref sharp, w, h, true);
        Ensure(ref soft, Mathf.Max(32, Screen.width / BlurDivisor), Mathf.Max(18, Screen.height / BlurDivisor), false);

        var request = new RenderPipeline.StandardRequest { destination = sharp };
        if (!RenderPipeline.SupportsRenderRequest(cam, request)) return;
        RenderPipeline.SubmitRenderRequest(cam, request);
        Graphics.Blit(sharp, soft);
        hasImage = true;
        lastCapture = Time.unscaledTime;
    }

    private static void Ensure(ref RenderTexture rt, int w, int h, bool depth)
    {
        if (rt != null && rt.width == w && rt.height == h && rt.IsCreated()) return;
        if (rt != null) rt.Release();
        rt = new RenderTexture(w, h, depth ? 24 : 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        rt.Create();
    }

    private class Runner : MonoBehaviour
    {
        private void LateUpdate()
        {
            // Only while a screen asked for it in the last couple of frames.
            if (Time.frameCount - lastWantedFrame > 2) return;
            bool stale = !hasImage || (interval > 0f && Time.unscaledTime - lastCapture >= interval);
            if (stale) Capture();
        }
    }
}
