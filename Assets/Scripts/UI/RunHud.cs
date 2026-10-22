using UnityEngine;

/// <summary>Always-on run info during a day: purse, date and weather chip, disaster banner.</summary>
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

        // Bonus seed toast (from the Lucky Sprout unlock), fades out.
        if (!string.IsNullOrEmpty(toast) && Time.unscaledTime < toastUntil)
        {
            float a = Mathf.Clamp01((toastUntil - Time.unscaledTime) / 0.8f);
            Rect t = new Rect(w * 0.5f - 280 * s, Screen.height * 0.62f, 560 * s, 54 * s);
            ImGuiKit.Fill(t, new Color(0.1f, 0.25f, 0.12f, 0.85f * a));
            ImGuiKit.HudLabel(t, toast, 28f, TextAnchor.MiddleCenter, true, a);
        }

        // Date / weather chip (top right).
        string season = plan.date.season != null ? plan.date.season.displayName : "";
        Color seasonColor = plan.date.season != null ? plan.date.season.uiColor : Color.white;
        Rect chip = new Rect(w - 360 * s, 12 * s, 344 * s, 86 * s);
        ImGuiKit.Fill(chip, new Color(0f, 0f, 0f, 0.55f));
        ImGuiKit.Fill(new Rect(chip.x, chip.y, 6 * s, chip.height), seasonColor);
        int year = director.Run != null ? director.Run.YearNumber(director.Meta) : 1;
        ImGuiKit.HudLabel(new Rect(chip.x + 16 * s, chip.y + 4 * s, chip.width - 24 * s, 28 * s), $"Year {year} · {season} · Day {plan.date.dayInSeason + 1}/{plan.date.daysInSeason}", 20f, TextAnchor.MiddleLeft, true);
        ImGuiKit.HudLabel(new Rect(chip.x + 16 * s, chip.y + 32 * s, chip.width - 24 * s, 24 * s), $"{plan.weather.kind}  ·  {plan.weather.temperature:0}°C", 18f);
        string nodeName = plan.node != null ? plan.node.displayName : "";
        ImGuiKit.HudLabel(new Rect(chip.x + 16 * s, chip.y + 56 * s, chip.width - 24 * s, 24 * s), nodeName, 18f, TextAnchor.MiddleLeft, true);

        DrawClock(s);

        // Key hint, bottom left (the hotbar is bottom center).
        ImGuiKit.HudLabel(new Rect(16 * s, Screen.height - 48 * s, 360 * s, 32 * s), "[Tab] Items   [Esc] Menu", 20f, TextAnchor.MiddleLeft, true);

        // Purse (below the chip).
        ImGuiKit.HudLabel(new Rect(w - 360 * s, 104 * s, 344 * s, 34 * s), $"Coins: {director.Wallet.Coins}", 26f, TextAnchor.MiddleRight, true);

        // Lost a boss fight last season: say why the crops are slow.
        RunState run = director.Run;
        if (run != null && run.growthDebuffSeason >= 0 && plan.date.seasonIndex == run.growthDebuffSeason)
        {
            int slower = Mathf.RoundToInt((1f - run.growthDebuffMult) * 100f);
            ImGuiKit.HudLabel(new Rect(w - 420 * s, 164 * s, 404 * s, 26 * s), $"Sluggish soil: crops grow {slower}% slower", 18f, TextAnchor.MiddleRight, true);
        }

        // Disaster banner.
        if (bannerDisaster != null && Time.unscaledTime < bannerUntil)
        {
            float a = Mathf.Clamp01((bannerUntil - Time.unscaledTime) / 0.6f);
            Rect b = new Rect(w * 0.5f - 360 * s, 130 * s, 720 * s, 80 * s);
            Color c = bannerDisaster.uiColor; c.a = 0.85f * a;
            ImGuiKit.Fill(b, c);
            ImGuiKit.HudLabel(b, $"{bannerDisaster.displayName.ToUpper()}  —  {bannerDisaster.bannerText}", 28f, TextAnchor.MiddleCenter, true, a);
            ImGuiKit.HudLabel(new Rect(b.x, b.yMax - 26 * s, b.width, 22 * s), $"Severity {Mathf.RoundToInt(bannerSeverity * 100f)}%", 16f, TextAnchor.MiddleCenter, false, a);
        }
        else if (director.Disaster != null && director.Disaster.Current != null)
        {
            ImGuiKit.HudLabel(new Rect(w - 360 * s, 138 * s, 344 * s, 26 * s), $"! {director.Disaster.Current.displayName}", 20f, TextAnchor.MiddleRight, true);
        }
    }

    /// <summary>Daylight / night countdown under the hearts. Flashes red when time is nearly up.</summary>
    private void DrawClock(float s)
    {
        DayClock clock = director.Clock;
        if (clock == null || !clock.Active || clock.Unlimited) return;

        bool night = clock.IsNight;
        float remaining = clock.Remaining;
        float frac = clock.Duration > 0f ? Mathf.Clamp01(remaining / clock.Duration) : 0f;
        bool urgent = remaining <= 15f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);

        Color fill = night ? new Color(0.6f, 0.5f, 0.95f)
            : urgent ? Color.Lerp(new Color(1f, 0.35f, 0.25f), new Color(1f, 0.7f, 0.3f), pulse)
            : Color.Lerp(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.6f, 0.25f), 1f - frac);

        Rect box = new Rect(16 * s, 110 * s, 280 * s, 50 * s);
        ImGuiKit.Fill(box, new Color(0f, 0f, 0f, 0.55f));
        ImGuiKit.Fill(new Rect(box.x + 6 * s, box.yMax - 14 * s, (box.width - 12 * s) * frac, 8 * s), fill);

        int secs = Mathf.CeilToInt(remaining);
        string label = night ? "Night" : "Daylight";
        ImGuiKit.HudLabel(new Rect(box.x + 10 * s, box.y + 2 * s, 120 * s, 26 * s), label, 20f, TextAnchor.MiddleLeft, true);
        ImGuiKit.HudLabel(new Rect(box.xMax - 130 * s, box.y + 2 * s, 120 * s, 26 * s), $"{secs / 60}:{secs % 60:00}", 24f, TextAnchor.MiddleRight, true);

        if (urgent)
        {
            string warn = night ? "Dawn is coming!" : (director.Plan != null && director.Plan.encounter != null ? "Dusk is near! Gather crops!" : "Dusk is near!");
            ImGuiKit.HudLabel(new Rect(box.x, box.yMax + 2 * s, 360 * s, 26 * s), warn, 20f, TextAnchor.MiddleLeft, true, 0.6f + 0.4f * pulse);
        }
    }

    private void OnDisaster(DisasterData d, float severity)
    {
        bannerDisaster = d;
        bannerSeverity = severity;
        bannerUntil = Time.unscaledTime + 4f;
    }
}
