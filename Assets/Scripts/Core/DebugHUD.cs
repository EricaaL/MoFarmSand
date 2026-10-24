using UnityEngine;

public class DebugHUD : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private BossHunger hunger;

    private GUIStyle centered;
    private GUIStyle big;
    private Texture2D white;
    private HotbarView hotbar;
    private HeldItemView heldItem;
    private string voidMessage = string.Empty;
    private float voidMessageUntil;

    // Death screen: red title plus black creeping in from the frame.
    private const float DefaultDeathSeconds = 4.5f;
    private static readonly Color DeathRed = new Color(0.75f, 0.05f, 0.05f);
    private Texture2D edgeFade;
    private float lostAt = -1f;
    private float deathSeconds = DefaultDeathSeconds;
    private GameManager subscribedTo;

    private void Awake()
    {
        white = Texture2D.whiteTexture;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            subscribedTo = GameManager.Instance;
            subscribedTo.OnVoidFall += HandleVoidFall;
            subscribedTo.OnPhaseChanged += HandlePhaseChanged;
        }
    }

    private void OnDestroy()
    {
        if (subscribedTo != null)
        {
            subscribedTo.OnVoidFall -= HandleVoidFall;
            subscribedTo.OnPhaseChanged -= HandlePhaseChanged;
        }
        if (edgeFade != null) Destroy(edgeFade);
        HotbarView.Reset();
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        if (phase != GamePhase.Lost) return;
        lostAt = Time.unscaledTime;
        RunDirector director = FindFirstObjectByType<RunDirector>();
        deathSeconds = director != null && director.Db != null && director.Db.run != null ? director.Db.run.deathScreenSeconds : DefaultDeathSeconds;
        // Only the roguelike loop reloads into a screen that can carry the blur on.
        if (director != null && director.IsRoguelike) DeathReveal.Begin(deathSeconds);
    }

    /// <summary>Opaque at the frame, clear in the middle; the alpha is stretched over the whole screen.</summary>
    private Texture2D EdgeFade()
    {
        if (edgeFade != null) return edgeFade;
        const int size = 128;
        edgeFade = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 0 at the frame, 1 at the centre (square-ish falloff so the black follows the screen edge).
                float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)) / (size * 0.5f);
                float a = 1f - Mathf.SmoothStep(0f, 1f, edge);
                edgeFade.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        edgeFade.Apply();
        return edgeFade;
    }

    private void DrawDeathScreen(float w, float h)
    {
        float t = lostAt < 0f ? 0f : Mathf.Clamp01((Time.unscaledTime - lostAt) / Mathf.Max(0.1f, deathSeconds));
        Rect full = new Rect(0f, 0f, w, h);

        // Black from the frame first, then the middle closes.
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.6f)));
        GUI.DrawTexture(full, EdgeFade());
        GUI.color = old;
        float solid = Mathf.InverseLerp(0.5f, 1f, t);
        if (solid > 0f) Fill(full, new Color(0f, 0f, 0f, solid));

        // Title pops in over the first moments, settling from slightly oversized.
        float seconds = lostAt < 0f ? 0f : Time.unscaledTime - lostAt;
        float pop = Mathf.Clamp01(seconds / 0.6f);
        float size = 160f * Mathf.Lerp(1.15f, 1f, Mathf.SmoothStep(0f, 1f, pop));
        float hs = ImGuiKit.Scale;
        Rect title = new Rect(0f, h * 0.5f - 130f * hs, w, 260f * hs);
        Color shadow = new Color(0f, 0f, 0f, 0.6f * pop);
        ImGuiKit.Heading(new Rect(title.x + 6f * hs, title.y + 8f * hs, title.width, title.height), "YOU DIED", size, shadow, TextAnchor.MiddleCenter);
        ImGuiKit.Heading(title, "YOU DIED", size, new Color(DeathRed.r, DeathRed.g, DeathRed.b, pop), TextAnchor.MiddleCenter);

        bool soft = GameManager.Instance != null && GameManager.Instance.SurvivableLoss;
        if (soft)
            ImGuiKit.HudLabel(new Rect(0f, h * 0.5f + 110f * hs, w, 40f * hs), "The soil will sulk next season: crops grow slower.", 26f, TextAnchor.MiddleCenter, false, pop);
    }

    private void HandleVoidFall(bool dayEnds)
    {
        voidMessage = dayEnds ? "YOU FELL INTO THE VOID  -1 ♥  (the day is over)" : "YOU FELL INTO THE VOID  -1 ♥";
        voidMessageUntil = Time.unscaledTime + 2.5f;
    }

    private void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = old;
    }

    private void OnGUI()
    {
        if (centered == null)
        {
            centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
            centered.normal.textColor = Color.white;
            big = new GUIStyle(centered) { fontSize = 48 };
        }

        // Run screens (map, shop, bank...) take over the whole display.
        if (RunDirector.ModalOpen) { HotbarView.Reset(); return; }

        float w = Screen.width, h = Screen.height;
        GamePhase phase = GameManager.Instance != null ? GameManager.Instance.Phase : GamePhase.Day;
        GUI.depth = phase == GamePhase.Lost ? -20 : 0;   // in front of the run HUD (depth -10) while dying
        bool hasEncounter = GameManager.Instance == null || GameManager.Instance.HasEncounter;

        // Fullness bar (top center), only on days with a night fight.
        float f = hunger != null ? hunger.Fullness01 : 0f;
        string bossName = hunger != null && hunger.Data != null ? hunger.Data.displayName : "Boss";
        if (hasEncounter)
        {
            Rect bar = new Rect(w / 2f - 200f, 16f, 400f, 28f);
            ImGuiKit.Rounded(bar, new Color(0.3f, 0.2f, 0.12f, 0.75f));
            if (f > 0.001f) ImGuiKit.Rounded(new Rect(bar.x + 3f, bar.y + 3f, Mathf.Max(bar.height - 6f, (bar.width - 6f) * f), bar.height - 6f), new Color(0.3f, 0.85f, 0.35f));
            string stronger = hunger != null && !hunger.Scale.IsIdentity ? " (stronger)" : string.Empty;
            ImGuiKit.HudLabel(bar, $"{bossName}{stronger} Fullness {Mathf.RoundToInt(f * 100f)}%", centered);
        }

        // HP + shield (top left)
        if (health != null)
        {
            float hs = ImGuiKit.Scale;
            UiTheme theme = ImGuiKit.Theme;
            if (theme != null && theme.heartFull != null)
            {
                // One heart icon per max HP, full or bitten-out; the purse (RunHud) sits under them.
                float size = 72f * hs, step = 80f * hs;
                for (int i = 0; i < health.MaxHp; i++)
                    ImGuiKit.Icon(new Rect(18f * hs + i * step, 14f * hs, size, size), i < health.Hp ? theme.heartFull : theme.heartGone);
            }
            else
            {
                string hearts = new string('♥', health.Hp) + new string('♡', Mathf.Max(0, health.MaxHp - health.Hp));
                ImGuiKit.HudLabel(new Rect(16f * hs, 10f * hs, 520f * hs, 60f * hs), "HP " + hearts, 44f, TextAnchor.MiddleLeft, true);
            }
            if (health.HasShield)
                ImGuiKit.HudLabel(new Rect(18f * hs, 150f * hs, 420f * hs, 36f * hs), $"[BARK SHIELD x{health.ShieldCharges} - {Mathf.CeilToInt(health.ShieldTimeLeft)}s]", 24f, TextAnchor.MiddleLeft, true);
        }

        // Hotbar (bottom center): slots, hover tooltip, drag-to-use.
        if (hotbar == null && inventory != null) hotbar = new HotbarView(inventory);
        if (hotbar != null) hotbar.Draw(w, h);

        // Held item (bottom right): icon, name and ammo / count of the active slot.
        if (heldItem == null && inventory != null) heldItem = new HeldItemView(inventory);
        if (heldItem != null) heldItem.Draw(w, h);

        // Interact prompt: wraps to the screen width instead of running past its box.
        if (interactor != null && interactor.Current != null && !HotbarView.TooltipOpen && (phase == GamePhase.Day || phase == GamePhase.Night))
        {
            // Sits just above the hotbar (which is scaled with the HUD), not on top of it.
            float ps = ImGuiKit.Scale;
            float promptW = Mathf.Min(w - 40f, 760f * ps);
            string prompt = interactor.Current.Prompt;
            float promptH = Mathf.Max(28f * ps, ImGuiKit.TextHeight(prompt, 18f, promptW, true) + 6f * ps);
            ImGuiKit.HudLabel(new Rect(w / 2f - promptW / 2f, h - 140f * ps - promptH, promptW, promptH), prompt, 18f, TextAnchor.LowerCenter, true);
        }

        if (Time.unscaledTime < voidMessageUntil)
            ImGuiKit.HudLabel(new Rect(0f, h * 0.3f, w, 60f), voidMessage, new GUIStyle(big) { fontSize = 36 });

        // Banner
        if (phase == GamePhase.Won) ImGuiKit.HudLabel(new Rect(0f, h / 2f - 40f, w, 80f), "You Survived Another Year...", big);
        else if (phase == GamePhase.DayEnd) ImGuiKit.HudLabel(new Rect(0f, h / 2f - 40f, w, 80f), "DAY COMPLETE!", big);

        if (phase == GamePhase.Lost) DrawDeathScreen(w, h);
    }
}
