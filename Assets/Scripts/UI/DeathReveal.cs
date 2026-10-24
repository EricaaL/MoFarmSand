using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The reveal after "YOU DIED". Whatever screen comes up next (map, summary...) is stilled and shown as a heavily blurred
/// picture of itself that sharpens back to the live screen over <see cref="DeblurSeconds"/>. A black rectangle sits on top
/// of everything from the moment the death screen ends and fades out over <see cref="BlackFadeSeconds"/>, so the reload never
/// flashes. Presentation only.
/// The new screen is read back at end of frame (menu included) with black painted over the display that frame, then shrunk
/// through a chain of half-size copies; drawing two neighbouring sizes and cross-fading gives a smooth blur amount.
/// </summary>
public static class DeathReveal
{
    private const float SettleSeconds = 0.35f;   // new screen gets this long to draw while hidden under the black
    public const float BlackFadeSeconds = 2f;    // the black rectangle fades out over this long
    public const float DeblurSeconds = 3f;       // the blur fades back to normal over this long
    private const int Levels = 6;                // 0 = screen/2 ... 5 = screen/64

    private static Runner runner;

    /// <summary>The player just died. The blur only starts after the death screen, so this only readies the runner.</summary>
    public static void Begin(float deathSeconds)
    {
        if (runner != null) Object.Destroy(runner.gameObject);
        GameObject go = new GameObject("DeathRevealRunner") { hideFlags = HideFlags.HideInHierarchy };
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
    }

    /// <summary>Call right before the scene reloads after a loss: black covers the screen, then the blur sharpens on the next one.</summary>
    public static void Arm() { if (runner != null) runner.Arm(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { runner = null; }

    private class Runner : MonoBehaviour
    {
        private enum State { Waiting, Armed, Revealing }

        private readonly RenderTexture[] chain = new RenderTexture[Levels];
        private State state = State.Waiting;
        private float revealAt;
        private bool grabbing, haveNewScreen;

        public void Arm()
        {
            if (state != State.Waiting) return;
            state = State.Armed;
        }

        private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // A reload nobody armed (the legacy loop) just drops the effect.
            if (state != State.Armed) { Destroy(gameObject); return; }
            state = State.Revealing;
            StartCoroutine(GrabNewScreen());
        }

        /// <summary>
        /// Stills the new screen (menu included, so it is read back at end of frame). The overlay hides for that one frame,
        /// then black is painted straight over the back buffer so the un-blurred screen never reaches the display.
        /// </summary>
        private IEnumerator GrabNewScreen()
        {
            yield return new WaitForSecondsRealtime(SettleSeconds);   // the new screen and its backdrop need a moment to draw
            grabbing = true;
            yield return new WaitForEndOfFrame();

            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            if (shot == null) { Destroy(gameObject); yield break; }
            // The back buffer can read back with alpha 0, which would make the blurred copy see-through.
            Color32[] pixels = shot.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i].a = 255;
            shot.SetPixels32(pixels);
            shot.Apply(false);
            EnsureTargets(shot.width / 2, shot.height / 2);
            Graphics.Blit(shot, chain[0]);
            for (int i = 1; i < Levels; i++) Graphics.Blit(chain[i - 1], chain[i]);
            Destroy(shot);

            Graphics.Blit(Texture2D.blackTexture, (RenderTexture)null);
            grabbing = false;
            haveNewScreen = true;
            revealAt = Time.unscaledTime;
        }

        private void EnsureTargets(int w, int h)
        {
            w = Mathf.Max(64, w); h = Mathf.Max(36, h);
            if (chain[0] != null && chain[0].width == w && chain[0].height == h) return;
            for (int i = 0; i < Levels; i++)
            {
                if (chain[i] != null) chain[i].Release();
                int d = 1 << i;
                chain[i] = new RenderTexture(Mathf.Max(8, w / d), Mathf.Max(8, h / d), 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                chain[i].Create();
            }
        }

        private void DrawBlur(Rect full, float blur01, float alpha)
        {
            float blur = blur01 * (Levels - 1);
            int lo = Mathf.FloorToInt(blur), hi = Mathf.Min(Levels - 1, lo + 1);
            float frac = blur - lo;
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(full, chain[hi]);
            GUI.color = new Color(1f, 1f, 1f, alpha * (1f - frac));
            GUI.DrawTexture(full, chain[lo]);
            GUI.color = old;
        }

        private static void DrawBlack(Rect full, float alpha)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void OnGUI()
        {
            if (state == State.Waiting) return;
            GUI.depth = -100;   // in front of every other screen; set on every event so Unity sorts this OnGUI last
            if (Event.current.type != EventType.Repaint) return;
            if (grabbing) return;   // one frame without the overlay so the new screen can be read back

            Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
            if (!haveNewScreen) { DrawBlack(full, 1f); return; }   // death screen over, new screen not up yet: pure black

            float t = Time.unscaledTime - revealAt;
            if (t >= Mathf.Max(DeblurSeconds, BlackFadeSeconds)) { Destroy(gameObject); return; }

            // The blurred picture starts at full blur and sharpens into the live screen; the black on top fades on its own clock.
            float r = Mathf.Clamp01(t / DeblurSeconds);
            float sharpen = Mathf.SmoothStep(0f, 1f, r);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.5f) / 0.5f));   // opaque for the first half, then melts into the live screen
            DrawBlur(full, 1f - sharpen, alpha);
            DrawBlack(full, 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / BlackFadeSeconds)));
        }

        private void OnDestroy()
        {
            foreach (RenderTexture rt in chain) if (rt != null) rt.Release();
            if (runner == this) runner = null;
        }
    }
}
