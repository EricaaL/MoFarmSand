using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// In-play tip cards for first-time players: a small non-blocking card in the top-right that appears once per tip, at the
/// moment it matters. Which tip is next is the pure <see cref="TipBook"/>; this raises them from existing events and draws.
/// </summary>
public class TipsView : MonoBehaviour
{
    [SerializeField] private TipRegistry tips;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private DisasterDirector disasters;
    [SerializeField] private float showSeconds = 10f;
    [SerializeField] private float gapSeconds = 1.2f;

    /// <summary>Raised when a tip card starts showing. Drives the tip sound.</summary>
    public static event System.Action Shown;

    private const float FadeIn = 0.3f, FadeOut = 0.5f;

    private TipBook book;
    private float shownFor;
    private float gapLeft;
    private int resetSeen;
    private string announced;

    private void Awake()
    {
        book = new TipBook(GameSettings.SeenTips) { Enabled = GameSettings.TipsEnabled };
        resetSeen = GameSettings.TipsResetCount;
    }

    private void OnEnable()
    {
        FarmTile.Planted += HandlePlanted;
        FarmTile.Ripened += HandleRipened;
        FarmTile.Harvested += HandleHarvested;
        if (inventory != null) inventory.OnChanged += HandleInventory;
        if (disasters != null) disasters.OnDisasterStarted += HandleDisaster;
    }

    private void Start()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnPhaseChanged += HandlePhase;
        GameManager.Instance.OnVoidFall += HandleVoidFall;
    }

    private void OnDisable()
    {
        FarmTile.Planted -= HandlePlanted;
        FarmTile.Ripened -= HandleRipened;
        FarmTile.Harvested -= HandleHarvested;
        if (inventory != null) inventory.OnChanged -= HandleInventory;
        if (disasters != null) disasters.OnDisasterStarted -= HandleDisaster;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnPhaseChanged -= HandlePhase;
        GameManager.Instance.OnVoidFall -= HandleVoidFall;
    }

    private void HandlePlanted(FarmTile tile, GameObject who) { book.Raise(TipIds.FirstPlant); }
    private void HandleRipened(FarmTile tile) { book.Raise(TipIds.FirstRipe); }
    private void HandleHarvested(FarmTile tile, CropData crop) { book.Raise(TipIds.FirstHarvest); }
    private void HandleDisaster(DisasterData disaster, float seconds) { book.Raise(TipIds.FirstDisaster); }
    private void HandleVoidFall(bool dayEnds) { book.Raise(TipIds.FirstVoidFall); }
    private void HandlePhase(GamePhase phase) { if (phase == GamePhase.Night) book.Raise(TipIds.FirstNight); }

    private void HandleInventory()
    {
        ItemData item = inventory.ActiveItem;
        if (item == null || inventory.ActiveCount <= 0) return;
        if (item is SeedData) book.Raise(TipIds.HoldingSeed);
        else if (item is CropData crop)
        {
            if (crop.type == CropType.Shooter) book.Raise(TipIds.ShooterHeld);
            else if (crop.type == CropType.Support) book.Raise(TipIds.SupportHeld);
        }
    }

    private void Update()
    {
        // The Options screen drives both of these through GameSettings.
        if (GameSettings.TipsResetCount != resetSeen) { resetSeen = GameSettings.TipsResetCount; book.Reset(); shownFor = 0f; gapLeft = 0f; }
        book.Enabled = GameSettings.TipsEnabled;
        if (!book.Enabled || RunDirector.ModalOpen) return;

        GamePhase phase = GameManager.Instance != null ? GameManager.Instance.Phase : GamePhase.Day;
        if (phase == GamePhase.Day)
        {
            book.Raise(TipIds.FirstDay);
            if (interactor != null && interactor.Current is MerchantStall) book.Raise(TipIds.Merchant);
        }

        if (book.Current == null) return;
        if (gapLeft > 0f) { gapLeft -= Time.unscaledDeltaTime; return; }
        if (announced != book.Current && TipsView.Visible(phase)) { announced = book.Current; Shown?.Invoke(); }
        shownFor += Time.unscaledDeltaTime;
        if (shownFor >= showSeconds) DismissCurrent();
    }

    private void OnGUI()
    {
        if (Event.current.type == EventType.Layout) HotbarView.BlockRect2 = Rect.zero; // set again below while a card is up
        if (!book.Enabled || book.Current == null || gapLeft > 0f || RunDirector.ModalOpen) return;
        GamePhase phase = GameManager.Instance != null ? GameManager.Instance.Phase : GamePhase.Day;
        if (phase != GamePhase.Day && phase != GamePhase.Night) return;

        TipData tip = tips != null ? tips.Get(book.Current) : null;
        if (tip == null) { book.Dismiss(); return; } // no asset for this id: don't get stuck on it

        float alpha = Mathf.Min(Mathf.Clamp01(shownFor / FadeIn), Mathf.Clamp01((showSeconds - shownFor) / FadeOut));
        float s = ImGuiKit.Scale;
        float w = 360f * s, pad = 22f * s, frame = 10f * s;
        float bodyH = ImGuiKit.TextHeight(tip.body, 20f, w - pad * 2f);
        float h = pad * 0.5f + 56f * s + bodyH + pad;
        // Top left, under the hearts and the purse.
        Rect card = new Rect(26f * s, 206f * s, w, h);

        Color orange = ImGuiKit.Orange; orange.a = alpha;
        Color cream = ImGuiKit.Cream; cream.a = alpha;
        Color text = ImGuiKit.Text; text.a = alpha;
        ImGuiKit.Rounded(new Rect(card.x - frame, card.y - frame, card.width + frame * 2f, card.height + frame * 2f), orange);
        ImGuiKit.Rounded(card, cream);
        // Shrink the title until it fits on one line beside the X button (never below 22).
        float titleW = w - pad * 2f - 56f * s;
        float titleSize = 36f;
        while (titleSize > 22f && ImGuiKit.TextWidth(tip.title, titleSize, ImGuiKit.Face.Display) > titleW) titleSize -= 1f;
        ImGuiKit.Heading(new Rect(card.x + pad, card.y + pad * 0.3f, titleW + 8f * s, 46f * s), tip.title, titleSize, text);
        ImGuiKit.DashedRule(card.x + pad, card.y + pad * 0.3f + 48f * s, w - pad * 2f, text);
        ImGuiKit.Label(new Rect(card.x + pad, card.y + pad * 0.3f + 58f * s, w - pad * 2f, bodyH + 4f * s), tip.body, 20f, text, TextAnchor.UpperLeft);

        // The X: dismiss this tip now (it counts as seen).
        Rect close = new Rect(card.xMax - 56f * s, card.y + 6f * s, 44f * s, 44f * s);
        HotbarView.BlockRect2 = close;
        if (ImGuiKit.CloseButton(close, false, alpha)) DismissCurrent();
    }

    private static bool Visible(GamePhase phase) { return phase == GamePhase.Day || phase == GamePhase.Night; }

    private void DismissCurrent()
    {
        book.Dismiss();
        GameSettings.SaveSeenTips(new List<string>(book.Seen));
        shownFor = 0f;
        gapLeft = gapSeconds;
        announced = null;
        HotbarView.BlockRect2 = Rect.zero;
    }
}
