using UnityEngine;

/// <summary>The in-run menu (Esc): resume, read the controls, look at the route, open the items tab, or leave the year at any time.</summary>
public class PauseScreen : IRunScreen
{
    private readonly RunDirector director;
    private bool confirmAbandon;

    public PauseScreen(RunDirector director) { this.director = director; }

    public void OnOpen() { confirmAbandon = false; }

    public void Draw()
    {
        RunState run = director.Run;
        float s = ImGuiKit.Scale;
        bool inDay = director.IsDayRunning;
        int year = run.YearNumber(director.Meta);

        float height = inDay ? 720f : 630f;
        Rect panel = ImGuiKit.Modal(620f, height, $"Year {year} - Paused");
        float x = panel.x + 40 * s, w = panel.width - 80 * s;
        float y = panel.y + 92 * s;
        float bh = 54 * s, gap = 12 * s;

        string where = inDay && director.Plan != null && director.Plan.node != null
            ? $"{director.Plan.node.displayName}  ·  {(director.Plan.date.season != null ? director.Plan.date.season.displayName : "")} day {director.Plan.date.dayInSeason + 1}"
            : "Choosing the next day";
        ImGuiKit.Label(new Rect(x, y - 10 * s, w, 28 * s), where, 18f, ImGuiKit.Muted, TextAnchor.MiddleLeft);
        y += 26 * s;

        if (ImGuiKit.Button(new Rect(x, y, w, bh), "Resume", true, 24f)) { director.ClosePause(); return; }
        y += bh + gap;

        if (ImGuiKit.Button(new Rect(x, y, w, bh), "Options", true, 22f)) { director.ShowOptions(); return; }
        y += bh + gap;

        if (ImGuiKit.Button(new Rect(x, y, w, bh), "Controls", true, 22f)) { director.ShowControls(); return; }
        y += bh + gap;

        if (inDay)
        {
            if (ImGuiKit.Button(new Rect(x, y, w, bh), "Route Map", true, 22f)) { director.ShowRoute(); return; }
            y += bh + gap;
            if (ImGuiKit.Button(new Rect(x, y, w, bh), "Items (Tab)", true, 22f)) { director.ShowItemsFromPause(); return; }
            y += bh + gap;
        }

        if (ImGuiKit.Button(new Rect(x, y, w, bh), "Save and Exit to Main Menu", true, 22f)) { director.QuitToTitle(); return; }
        y += bh + gap;
        ImGuiKit.Label(new Rect(x, y - 8 * s, w, 22 * s),
            inDay ? "Today's progress is not saved. The year resumes from the start of today." : "The year resumes from here.",
            15f, ImGuiKit.Muted, TextAnchor.MiddleLeft);
        y += 22 * s;

        if (director.IsBeta)
        {
            if (inDay && ImGuiKit.Button(new Rect(x, y, w, bh), "Back to Beta Map", true, 22f)) { director.LeaveBetaDay(); return; }
            if (inDay) y += bh + gap;
            if (ImGuiKit.Button(new Rect(x, y, w, bh), confirmAbandon ? "Click again to wipe the beta slot" : "Reset Beta Slot", true, 22f))
            {
                if (confirmAbandon) { director.ResetBetaSlot(); return; }
                confirmAbandon = true;
            }
            return;
        }

        string abandon = confirmAbandon ? "Really abandon the year? Click again" : "Abandon Year";
        if (ImGuiKit.Button(new Rect(x, y, w, bh), abandon, true, 22f))
        {
            if (confirmAbandon) { director.AbandonRun(); return; }
            confirmAbandon = true;
        }
        if (confirmAbandon)
            ImGuiKit.Label(new Rect(x, y + bh + 4 * s, w, 24 * s), "Coins in your purse are lost. You'll go to the upgrade shop.", 16f, new Color(1f, 0.6f, 0.55f), TextAnchor.MiddleLeft);
    }
}
