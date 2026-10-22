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

    private void Awake()
    {
        white = Texture2D.whiteTexture;
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnVoidFall += HandleVoidFall;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnVoidFall -= HandleVoidFall;
        HotbarView.Reset();
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
        bool hasEncounter = GameManager.Instance == null || GameManager.Instance.HasEncounter;

        // Fullness bar (top center), only on days with a night fight.
        float f = hunger != null ? hunger.Fullness01 : 0f;
        string bossName = hunger != null && hunger.Data != null ? hunger.Data.displayName : "Boss";
        if (hasEncounter)
        {
            Rect bar = new Rect(w / 2f - 200f, 16f, 400f, 28f);
            Fill(bar, new Color(0f, 0f, 0f, 0.6f));
            Fill(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * f, bar.height - 4f), new Color(0.3f, 0.85f, 0.35f));
            string stronger = hunger != null && !hunger.Scale.IsIdentity ? " (stronger)" : string.Empty;
            ImGuiKit.HudLabel(bar, $"{bossName}{stronger} Fullness {Mathf.RoundToInt(f * 100f)}%", centered);
        }
        ImGuiKit.HudLabel(new Rect(w / 2f - 200f, 46f, 400f, 24f), phase.ToString().ToUpper(), centered);

        // HP + shield (top left)
        if (health != null)
        {
            string hearts = new string('♥', health.Hp) + new string('♡', Mathf.Max(0, health.MaxHp - health.Hp));
            float hs = ImGuiKit.Scale;
            ImGuiKit.HudLabel(new Rect(16f * hs, 10f * hs, 520f * hs, 60f * hs), "HP " + hearts, 44f, TextAnchor.MiddleLeft, true);
            if (health.HasShield)
                ImGuiKit.HudLabel(new Rect(16f * hs, 68f * hs, 420f * hs, 36f * hs), $"[BARK SHIELD x{health.ShieldCharges} - {Mathf.CeilToInt(health.ShieldTimeLeft)}s]", 24f, TextAnchor.MiddleLeft, true);
        }

        // Hotbar (bottom center): slots, hover tooltip, drag-to-use.
        if (hotbar == null && inventory != null) hotbar = new HotbarView(inventory);
        if (hotbar != null) hotbar.Draw(w, h);

        // Held item (bottom right): icon, name and ammo / count of the active slot.
        if (heldItem == null && inventory != null) heldItem = new HeldItemView(inventory);
        if (heldItem != null) heldItem.Draw(w, h);

        // Interact prompt: wraps to the screen width instead of running past its box.
        if (interactor != null && interactor.Current != null && (phase == GamePhase.Day || phase == GamePhase.Night))
        {
            float promptW = Mathf.Min(w - 40f, 760f);
            string prompt = interactor.Current.Prompt;
            float promptH = Mathf.Max(28f, ImGuiKit.TextHeight(prompt, 18f, promptW, true) + 6f);
            ImGuiKit.HudLabel(new Rect(w / 2f - promptW / 2f, h - 150f - promptH + 28f, promptW, promptH), prompt, 18f, TextAnchor.LowerCenter, true);
        }

        if (Time.unscaledTime < voidMessageUntil)
            ImGuiKit.HudLabel(new Rect(0f, h * 0.3f, w, 60f), voidMessage, new GUIStyle(big) { fontSize = 36 });

        // Banner
        if (phase == GamePhase.Won) ImGuiKit.HudLabel(new Rect(0f, h / 2f - 40f, w, 80f), $"{bossName.ToUpper()} SATISFIED!", big);
        else if (phase == GamePhase.Lost)
        {
            bool soft = GameManager.Instance != null && GameManager.Instance.SurvivableLoss;
            ImGuiKit.HudLabel(new Rect(0f, h / 2f - 40f, w, 80f), soft ? "THE CATERPILLAR WINS THE NIGHT..." : "THE NIGHT CONSUMES YOU...", big);
            if (soft) ImGuiKit.HudLabel(new Rect(0f, h / 2f + 30f, w, 40f), "The soil will sulk next season: crops grow slower.", centered);
        }
        else if (phase == GamePhase.DayEnd) ImGuiKit.HudLabel(new Rect(0f, h / 2f - 40f, w, 80f), "DAY COMPLETE!", big);
    }
}
