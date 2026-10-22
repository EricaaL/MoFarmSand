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

    private const float FadeIn = 0.3f, FadeOut = 0.5f;

    private TipBook book;
    private float shownFor;
    private float gapLeft;
    private int resetSeen;

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
        shownFor += Time.unscaledDeltaTime;
        if (shownFor >= showSeconds)
        {
            book.Dismiss();
            GameSettings.SaveSeenTips(new List<string>(book.Seen));
            shownFor = 0f;
            gapLeft = gapSeconds;
        }
    }

    private void OnGUI()
    {
        if (!book.Enabled || book.Current == null || gapLeft > 0f || RunDirector.ModalOpen) return;
        GamePhase phase = GameManager.Instance != null ? GameManager.Instance.Phase : GamePhase.Day;
        if (phase != GamePhase.Day && phase != GamePhase.Night) return;

        TipData tip = tips != null ? tips.Get(book.Current) : null;
        if (tip == null) { book.Dismiss(); return; } // no asset for this id: don't get stuck on it

        float alpha = Mathf.Min(Mathf.Clamp01(shownFor / FadeIn), Mathf.Clamp01((showSeconds - shownFor) / FadeOut));
        float s = ImGuiKit.Scale;
        float w = 400f * s, pad = 16f * s;
        float bodyH = ImGuiKit.TextHeight(tip.body, 18f, w - pad * 2f);
        float h = pad + 30f * s + bodyH + pad;
        // Below RunHud's top-right stack (season chip, coins, disaster and soil warnings end around y = 190).
        Rect card = new Rect(Screen.width - w - 20f * s, 204f * s, w, h);

        Color ink = ImGuiKit.Ink; ink.a *= alpha;
        Color accent = ImGuiKit.Accent; accent.a = alpha;
        ImGuiKit.Fill(card, ink);
        ImGuiKit.Fill(new Rect(card.x, card.y, 6f * s, card.height), accent);
        ImGuiKit.Label(new Rect(card.x + pad + 6f * s, card.y + pad * 0.6f, w - pad * 2f, 30f * s), tip.title, 22f, accent, TextAnchor.MiddleLeft, true);
        Color text = Color.white; text.a = alpha;
        ImGuiKit.Label(new Rect(card.x + pad + 6f * s, card.y + pad * 0.6f + 32f * s, w - pad * 2f - 6f * s, bodyH + 4f * s), tip.body, 18f, text, TextAnchor.UpperLeft);
    }
}
