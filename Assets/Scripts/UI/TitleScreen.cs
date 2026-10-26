using UnityEngine;

/// <summary>Three save-slot cards (continue / start new). Upgrades are bought at the end of a year, or on the map at the start of the next one.</summary>
public class TitleScreen : IRunScreen
{
    private readonly RunDirector director;
    private int pendingDelete = -1;

    // Peeking reads the save files, so keep the summaries instead of re-reading them every IMGUI event.
    private readonly SlotSummary[] summaries = new SlotSummary[SaveService.SlotCount];
    private float peekedAt = -10f;

    // Read-only look at a slot's upgrades (buying happens at the end of a year).
    private int previewSlot = -1;
    private MetaProgress previewMeta;

    public TitleScreen(RunDirector director) { this.director = director; }

    public void Draw()
    {
        if (RunSession.HasActiveSlot) director.CloseSlot();   // the slot cards are the only title page now
        if (previewMeta != null) { DrawPreview(); return; }
        DrawSlotList();
    }

    /// <summary>Closes the upgrade preview if it is open (Esc / back). Returns true when it was.</summary>
    public bool ClosePreview()
    {
        if (previewMeta == null) return false;
        previewMeta = null;
        previewSlot = -1;
        return true;
    }

    private void OpenPreview(int slot)
    {
        LoadResult r = director.Service.Load(slot);
        if (r.status != LoadStatus.Ok) return;
        SaveService.Sanitize(r.file, director.Db);
        previewSlot = slot;
        previewMeta = r.file.meta;
    }

    private void DrawPreview()
    {
        float s = ImGuiKit.Scale;
        ImGuiKit.Backdrop(UiBackdrop.LiveInterval);
        if (ImGuiKit.BackButton(new Rect(42f * s, 36f * s, 130f * s, 130f * s))) { ClosePreview(); return; }
        UpgradeList.Draw(director, new Rect(209f * s, 119f * s, 1502f * s, 842f * s), previewMeta);    }

    /// <summary>From a slot's upgrade page back to the three slot cards (the back button and Esc).</summary>
    public void BackToSlots()
    {
        director.CloseSlot();
        Refresh(true);
    }

    private void Refresh(bool force)
    {
        if (!force && Time.unscaledTime - peekedAt < 1f) return;
        for (int i = 0; i < summaries.Length; i++) summaries[i] = director.Service.Peek(i);
        peekedAt = Time.unscaledTime;
    }

    // ------------------------------------------------------------------ slot cards

    private void DrawSlotList()
    {
        float s = ImGuiKit.Scale;
        Refresh(false);
        ImGuiKit.Backdrop(UiBackdrop.LiveInterval);

        if (ImGuiKit.BackButton(new Rect(42f * s, 36f * s, 130f * s, 130f * s)))
        {
            pendingDelete = -1;
            director.ShowMainMenu();
            return;
        }

        float cardW = 410f * s, cardH = 615f * s, step = 515f * s;
        float x0 = 210f * s, y0 = 153f * s;
        int hovered = -1;
        for (int i = 0; i < SaveService.SlotCount; i++)
            if (new Rect(x0 + i * step, y0, cardW + 25f * s, cardH + 27f * s).Contains(Event.current.mousePosition)) hovered = i;

        for (int i = 0; i < SaveService.SlotCount; i++)
        {
            bool lit = hovered == i || (hovered < 0 && i == 0);
            if (DrawCard(i, new Rect(x0 + i * step, y0, cardW, cardH), summaries[i], lit, s)) return;
        }
    }

    /// <summary>One slot. Returns true when it changed the screen (caller should stop drawing).</summary>
    private bool DrawCard(int i, Rect card, SlotSummary sum, bool lit, float s)
    {
        Color shadow = lit ? ImGuiKit.Orange : ImGuiKit.CardGray;

        ImGuiKit.Rounded(new Rect(card.x + 25f * s, card.y + 27f * s, card.width, card.height), shadow);

        // "Upgrades" tab peeking out above the card's top right, in the shadow colour (read-only preview of this slot's upgrades).
        if (sum.status == LoadStatus.Ok)
        {
            Rect tab = new Rect(card.x + 228f * s, card.y - 53f * s, card.width - 203f * s, 120f * s);
            ImGuiKit.Rounded(tab, shadow);
            ImGuiKit.Italic(new Rect(tab.x, tab.y, tab.width, 53f * s), "Upgrades", 30f, ImGuiKit.Muted, TextAnchor.MiddleCenter);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && new Rect(tab.x, tab.y, tab.width, 53f * s).Contains(e.mousePosition))
            {
                ImGuiKit.ConsumeSmallClick();
                pendingDelete = -1;
                OpenPreview(i);
                return true;
            }
        }
        ImGuiKit.Rounded(card, lit ? ImGuiKit.Cream : ImGuiKit.PanelLight);

        UiTheme theme = ImGuiKit.Theme;
        float x = card.x + 26f * s, w = card.width - 52f * s;
        ImGuiKit.Heading(new Rect(x, card.y + 22f * s, w, 80f * s), $"Slot {i + 1}", 64f, ImGuiKit.Text);
        ImGuiKit.DashedRule(x, card.y + 108f * s, w);

        string action = "Start New";
        bool actionEnabled = true;
        switch (sum.status)
        {
            case LoadStatus.Empty:
                ImGuiKit.Label(new Rect(x, card.y + 128f * s, w, 70f * s), "Empty", 52f, ImGuiKit.Text);
                break;
            case LoadStatus.Corrupt:
                ImGuiKit.Label(new Rect(x, card.y + 128f * s, w, 130f * s), "This save could not be read.", 30f, ImGuiKit.Text, TextAnchor.UpperLeft);
                action = "Reset slot";
                break;
            case LoadStatus.TooNew:
                ImGuiKit.Label(new Rect(x, card.y + 128f * s, w, 130f * s), "Saved by a newer version of the game.", 30f, ImGuiKit.Text, TextAnchor.UpperLeft);
                action = "Can't open";
                actionEnabled = false;
                break;
            default:
                DrawSlotDetails(card, sum, theme, x, w, s);
                action = sum.hasRun ? "Continue" : "Start New";
                break;
        }

        Rect button = new Rect(card.x + 45f * s, card.yMax - 108f * s, card.width - 90f * s, 76f * s);
        if (ImGuiKit.Pill(button, action, lit ? ImGuiKit.PillKind.Primary : ImGuiKit.PillKind.Gray, actionEnabled, 36f))
        {
            if (sum.status == LoadStatus.Corrupt) { director.DeleteSlot(i); Refresh(true); return true; }
            director.OpenSlot(i);
            if (!RunSession.HasActiveSlot) return true;
            if (director.File.hasRun) director.ContinueRun(); else director.StartNewRun();
            return true;
        }

        // Round X under the card deletes the slot: the first click arms it (red), the second one deletes.
        if (sum.status != LoadStatus.Empty)
        {
            bool armed = pendingDelete == i;
            Rect del = new Rect(card.center.x - 30f * s, card.yMax + 40f * s, 60f * s, 60f * s);   // centred under the card
            if (ImGuiKit.CloseButton(del, armed))
            {
                if (armed) { director.DeleteSlot(i); pendingDelete = -1; Refresh(true); return true; }
                pendingDelete = i;
            }
            if (armed)
                ImGuiKit.HudLabel(new Rect(card.x - 20f * s, del.yMax + 6f * s, card.width + 40f * s, 36f * s), "Click again to delete this slot", 22f, TextAnchor.MiddleCenter, true);
        }
        return false;
    }

    /// <summary>"12/200": what is in the vault out of what it can hold.</summary>
    private string VaultText(SlotSummary sum)
    {
        BankConfig bank = director.Db.bank;
        return bank != null ? $"{sum.vaultCoins}/{VaultLogic.CapFor(sum.vaultLevel, bank)}" : sum.vaultCoins.ToString();
    }

    private void DrawSlotDetails(Rect card, SlotSummary sum, UiTheme theme, float x, float w, float s)
    {
        float y = card.y + 126f * s;
        if (sum.hasRun)
        {
            ImGuiKit.Label(new Rect(x, y, w, 70f * s), $"Year {Mathf.Max(1, sum.year)}", 52f, ImGuiKit.Text);
            CalendarDate d = director.Db.Calendar.DateOf(sum.dayIndex);
            SeasonData season = d.season;
            ImGuiKit.Icon(new Rect(x - 6f * s, y + 72f * s, 96f * s, 96f * s), season != null ? season.icon : null);
            ImGuiKit.Italic(new Rect(x + 100f * s, y + 74f * s, w - 100f * s, 58f * s), season != null ? season.displayName : "", 38f, ImGuiKit.Text, TextAnchor.MiddleLeft, false);
            ImGuiKit.Italic(new Rect(x + 100f * s, y + 118f * s, w - 100f * s, 54f * s), $"Day {d.dayInSeason + 1}/{d.daysInSeason}", 34f, ImGuiKit.Text, TextAnchor.MiddleLeft, false);
            ImGuiKit.IconValue(new Rect(x, y + 196f * s, w, 70f * s), theme != null ? theme.coin : null, sum.coins.ToString(), 40f, ImGuiKit.Text);
            ImGuiKit.IconValue(new Rect(x, y + 276f * s, w, 70f * s), theme != null ? theme.piggyBank : null, VaultText(sum), 40f, ImGuiKit.Text);
        }
        else
        {
            ImGuiKit.Label(new Rect(x, y, w, 70f * s), "No year in progress", 34f, ImGuiKit.Text);
            ImGuiKit.IconValue(new Rect(x, y + 276f * s, w, 70f * s), theme != null ? theme.piggyBank : null, VaultText(sum), 40f, ImGuiKit.Text);
        }
    }
}
