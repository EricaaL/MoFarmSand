using UnityEngine;

/// <summary>Always-on run info during a day: purse, date card with the day/night dial, key hints, disaster banner.</summary>
public class RunHud
{
    private readonly RunDirector director;
    private float bannerUntil;
    private DisasterData bannerDisaster;
    private float bannerSeverity;
    private bool subscribed;
    private bool bonusSubscribed;
    private string toast;
    private float toastUntil;

    public RunHud(RunDirector director) { this.director = director; }

    private void OnBonusSeed(SeedData seed)
    {
        toast = $"Lucky find!  +1 {seed.DisplayName}";
        toastUntil = Time.unscaledTime + 3f;
    }

    /// <summary>Shows a short fading note in the middle of the screen.</summary>
    public void Say(string text)
    {
        toast = text;
        toastUntil = Time.unscaledTime + 3f;
    }

    public void Draw()
    {
        DayPlan plan = director.Plan;
        if (plan == null) return;
        if (!subscribed && director.Disaster != null)
        {
            director.Disaster.OnDisasterStarted += OnDisaster;
            subscribed = true;
            // The disaster may already have begun before the HUD subscribed.
            if (director.Disaster.Current != null) OnDisaster(director.Disaster.Current, director.Disaster.Severity);
        }

        if (!bonusSubscribed && director.Conditions != null)
        {
            director.Conditions.OnBonusSeed += OnBonusSeed;
            bonusSubscribed = true;
        }

        float s = ImGuiKit.Scale;
        float w = Screen.width;
        UiTheme theme = ImGuiKit.Theme;

        // Bonus seed toast (from the Lucky Sprout unlock), fades out.
        if (!string.IsNullOrEmpty(toast) && Time.unscaledTime < toastUntil)
        {
            float a = Mathf.Clamp01((toastUntil - Time.unscaledTime) / 0.8f);
            Rect t = new Rect(w * 0.5f - 280 * s, Screen.height * 0.62f, 560 * s, 54 * s);
            ImGuiKit.Rounded(t, new Color(0.1f, 0.25f, 0.12f, 0.85f * a));
            ImGuiKit.HudLabel(t, toast, 28f, TextAnchor.MiddleCenter, true, a);
        }

        // Purse (below the hearts, top left).
        Rect coinRow = new Rect(26 * s, 96 * s, 260 * s, 52 * s);
        ImGuiKit.IconValue(coinRow, theme != null ? theme.coin : null, director.Wallet.Coins.ToString(), 38f, ImGuiKit.HudText, TextAnchor.MiddleLeft, true);

        float cardBottom = DrawDateCard(plan, s, w, theme);

        // Beta scene only: pick any seed. Registered with the hotbar's pointer check so clicking it doesn't also throw a crop.
        if (director.IsBeta)
        {
            Rect seedsButton = new Rect(16 * s, Screen.height - 96 * s, 200 * s, 40 * s);
            HotbarView.BlockRect = seedsButton;
            if (ImGuiKit.Button(seedsButton, "Get Seeds", true, 20f)) director.ShowBetaSeeds();
        }

        // Lost a boss fight last season: say why the crops are slow.
        RunState run = director.Run;
        float notesY = cardBottom + 14 * s;
        if (run != null && run.growthDebuffSeason >= 0 && plan.date.seasonIndex == run.growthDebuffSeason)
        {
            int slower = Mathf.RoundToInt((1f - run.growthDebuffMult) * 100f);
            ImGuiKit.HudLabel(new Rect(w - 420 * s, notesY, 404 * s, 26 * s), $"Sluggish soil: crops grow {slower}% slower", 18f, TextAnchor.MiddleRight, true);
            notesY += 28 * s;
        }

        // Disaster banner.
        if (bannerDisaster != null && Time.unscaledTime < bannerUntil)
        {
            float a = Mathf.Clamp01((bannerUntil - Time.unscaledTime) / 0.6f);
            Rect b = new Rect(w * 0.5f - 360 * s, 130 * s, 720 * s, 80 * s);
            Color c = bannerDisaster.uiColor; c.a = 0.85f * a;
            ImGuiKit.Rounded(b, c);
            ImGuiKit.HudLabel(b, $"{bannerDisaster.displayName.ToUpper()}  —  {bannerDisaster.bannerText}", 28f, TextAnchor.MiddleCenter, true, a);
            ImGuiKit.HudLabel(new Rect(b.x, b.yMax - 26 * s, b.width, 22 * s), $"Severity {Mathf.RoundToInt(bannerSeverity * 100f)}%", 16f, TextAnchor.MiddleCenter, false, a);
        }
        else if (director.Disaster != null && director.Disaster.Current != null)
        {
            ImGuiKit.HudLabel(new Rect(w - 360 * s, notesY, 344 * s, 26 * s), $"! {director.Disaster.Current.displayName}", 20f, TextAnchor.MiddleRight, true);
        }
    }

    private const float CardScale = 0.78f;   // the date card is drawn at this fraction of its mockup size

    /// <summary>
    /// Top-right: the cream date card with the day/night wheel behind its left edge. The wheel stays still; the orange arrow turns
    /// around the hub (left = dawn, bottom = day, right = dusk, top = night). The countdown sits under the wheel.
    /// Returns the y just below the card's key hints, for the notes that follow.
    /// </summary>
    private float DrawDateCard(DayPlan plan, float s, float w, UiTheme theme)
    {
        float c = s * CardScale;
        Rect card = new Rect(w - 26 * s - 332 * c, 14 * s, 332 * c, 428 * c);   // top lines up with the hearts
        Vector2 hub = new Vector2(card.x + 2 * c, card.y + 236 * c);   // wheel sits low enough for the timer to fit above it inside the card's height
        float radius = 142 * c;

        // Still wheel with an orange rim.
        if (theme != null && theme.dayNightDial != null)
        {
            ImGuiKit.Circle(hub, radius + 14 * c, ImGuiKit.Orange);
            GUI.DrawTexture(new Rect(hub.x - radius, hub.y - radius, radius * 2f, radius * 2f), theme.dayNightDial, ScaleMode.StretchToFill, true);
        }

        // The arrow (drawn pointing left): at the start of a phase it points down, then turns clockwise through the visible left half to point up.
        Color orange = ImGuiKit.Orange;
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(ArrowAngle(), hub);
        Vector2 tip = new Vector2(hub.x - 112 * c, hub.y);
        ImGuiKit.Line(tip, hub, 12 * c, orange);
        ImGuiKit.Line(tip, tip + new Vector2(42 * c, -34 * c), 12 * c, orange);
        ImGuiKit.Line(tip, tip + new Vector2(42 * c, 34 * c), 12 * c, orange);
        ImGuiKit.Circle(tip, 6 * c, orange);
        GUI.matrix = saved;
        ImGuiKit.Circle(hub, 36 * c, orange);   // the pivot knob stays put

        ImGuiKit.Rounded(card, ImGuiKit.Orange);
        Rect inner = new Rect(card.x + 13 * c, card.y + 13 * c, card.width - 26 * c, card.height - 26 * c);
        ImGuiKit.Rounded(inner, ImGuiKit.Cream);

        SeasonData season = plan.date.season;
        int year = director.Run != null ? director.Run.YearNumber(director.Meta) : 1;
        float x = inner.x + 18 * c, cw = inner.width - 30 * c;
        ImGuiKit.Heading(new Rect(x, inner.y + 6 * c, cw, 72 * c), $"Year {year}", 56f * CardScale, ImGuiKit.Text);
        ImGuiKit.DashedRule(x, inner.y + 84 * c, cw);
        // Boxes are taller than the text and don't wrap, so descenders (the g in "Spring") are never clipped.
        ImGuiKit.Italic(new Rect(x, inner.y + 88 * c, cw, 64 * c), season != null ? season.displayName : "", 40f * CardScale, ImGuiKit.Text, TextAnchor.MiddleLeft, false);
        ImGuiKit.Italic(new Rect(x, inner.y + 134 * c, cw, 56 * c), $"Day {plan.date.dayInSeason + 1}/{plan.date.daysInSeason}", 34f * CardScale, ImGuiKit.Text, TextAnchor.MiddleLeft, false);

        Rect iconBox = new Rect(x - 4 * c, inner.y + 196 * c, 96 * c, 96 * c);
        ImGuiKit.Icon(iconBox, season != null ? season.icon : null);
        ImGuiKit.Italic(new Rect(iconBox.xMax + 10 * c, inner.y + 196 * c, cw - 96 * c, 54 * c), plan.weather.kind.ToString(), 34f * CardScale, ImGuiKit.Muted, TextAnchor.MiddleLeft, false);
        ImGuiKit.Italic(new Rect(iconBox.xMax + 10 * c, inner.y + 238 * c, cw - 96 * c, 54 * c), $"{plan.weather.temperature:0}°C", 34f * CardScale, ImGuiKit.Muted, TextAnchor.MiddleLeft, false);

        string nodeName = plan.node != null ? plan.node.displayName : "";
        ImGuiKit.Label(new Rect(inner.x, inner.yMax - 74 * c, inner.width, 56 * c), nodeName, 34f * CardScale, ImGuiKit.Text, TextAnchor.MiddleCenter, false, false);

        // Key hints under the card: both caps share one width and one x, so the caps and their words line up.
        float capW = ImGuiKit.KeyCapWidth("ESC", "TAB");
        float rowH = 46 * c;
        Rect esc = new Rect(card.x + 40 * c, card.yMax + 16 * c, capW + 190 * c, rowH);
        ImGuiKit.KeyCap(esc, "ESC", "Menu", true, capW);
        ImGuiKit.KeyCap(new Rect(esc.x, esc.y + rowH + 12 * c, esc.width, rowH), "TAB", "Items", true, capW);

        DrawTimer(s, hub, radius, c);
        return esc.y + rowH * 2f + 12 * c;
    }

    /// <summary>
    /// Clockwise rotation (degrees) to apply to the left-pointing arrow: -90 (points down) at the start of the day or night,
    /// 0 (points left) halfway, +90 (points up) at the end. Day and night each sweep the visible half once.
    /// </summary>
    private float ArrowAngle()
    {
        DayClock clock = director.Clock;
        float p = 0f;
        if (clock != null && clock.Active && !clock.Unlimited && clock.Duration > 0f)
            p = 1f - Mathf.Clamp01(clock.Remaining / clock.Duration);
        return -90f + 180f * p;
    }

    /// <summary>Daylight / night countdown under the wheel. Flashes red when time is nearly up. Hidden once the day has resolved (the clock is stopped).</summary>
    private void DrawTimer(float s, Vector2 hub, float radius, float c)
    {
        DayClock clock = director.Clock;
        if (clock == null || !clock.Active || clock.Unlimited) return;
        GamePhase phase = GameManager.Instance != null ? GameManager.Instance.Phase : GamePhase.Day;
        if (phase != GamePhase.Day && phase != GamePhase.Night) return;

        bool night = clock.IsNight;
        float remaining = clock.Remaining;
        float frac = clock.Duration > 0f ? Mathf.Clamp01(remaining / clock.Duration) : 0f;
        bool urgent = remaining <= 15f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);

        Color fill = night ? new Color(0.6f, 0.5f, 0.95f)
            : urgent ? Color.Lerp(new Color(1f, 0.35f, 0.25f), new Color(1f, 0.7f, 0.3f), pulse)
            : Color.Lerp(new Color(1f, 0.85f, 0.35f), ImGuiKit.Orange, 1f - frac);

        // A small pill above the visible (left) half of the wheel, ending just left of the card.
        Rect box = new Rect(hub.x - radius - 12 * c, hub.y - radius - 14 * c - 8 * c - 58 * c, radius + 2 * c, 58 * c);
        ImGuiKit.Rounded(box, new Color(0f, 0f, 0f, 0.55f));
        ImGuiKit.Rounded(new Rect(box.x + 8 * c, box.yMax - 14 * c, Mathf.Max(8 * c, (box.width - 16 * c) * frac), 7 * c), fill);
        int secs = Mathf.CeilToInt(remaining);
        ImGuiKit.HudLabel(new Rect(box.x, box.y + 2 * c, box.width, 38 * c), $"{secs / 60}:{secs % 60:00}", 26f, TextAnchor.MiddleCenter, true);

        if (urgent)
        {
            string warn = night ? "Dawn is coming!" : (director.Plan != null && director.Plan.encounter != null ? "Dusk is near! Gather crops!" : "Dusk is near!");
            ImGuiKit.HudLabel(new Rect(box.x - 60 * c, hub.y + radius + 22 * c, box.width + 120 * c, 30 * c), warn, 18f, TextAnchor.MiddleCenter, true, 0.6f + 0.4f * pulse);
        }
    }

    private void OnDisaster(DisasterData d, float severity)
    {
        bannerDisaster = d;
        bannerSeverity = severity;
        bannerUntil = Time.unscaledTime + 4f;
    }
}
