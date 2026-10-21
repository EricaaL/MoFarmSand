using UnityEngine;

/// <summary>The in-run menu (Esc): resume, look at the route, open the items tab, or leave the year at any time.</summary>
public class PauseScreen : IRunScreen
{
    private readonly RunDirector director;
    private bool confirmAbandon;

    public PauseScreen(RunDirector director) { this.director = director; }

    // The slider edits this while dragging and it is applied on release, so the panel doesn't rescale under the cursor.
    private float pendingScale = 1f;

    public void OnOpen() { confirmAbandon = false; pendingScale = ImGuiKit.UserScale; }

    public void Draw()
    {
        RunState run = director.Run;
        float s = ImGuiKit.Scale;
        bool inDay = director.IsDayRunning;
        int year = run.YearNumber(director.Meta);

        float height = inDay ? 640f : 550f;
        Rect panel = ImGuiKit.Modal(620f, height, $"Year {year} - Paused");
        float x = panel.x + 40 * s, w = panel.width - 80 * s;
        float y = panel.y + 92 * s;
        float bh = 54 * s, gap = 12 * s;

        string where = inDay && director.Plan != null && director.Plan.node != null
            ? $"{director.Plan.node.displayName}  ·  {(director.Plan.date.season != null ? director.Plan.date.season.displayName : "")} day {director.Plan.date.dayInSeason + 1}"
            : "Choosing the next day";
        ImGuiKit.Label(new Rect(x, y - 10 * s, w, 28 * s), where, 18f, ImGuiKit.Muted, TextAnchor.MiddleLeft);
        y += 26 * s;

        DrawHudSizeSlider(x, y, w, s);
        y += 52 * s;

        if (ImGuiKit.Button(new Rect(x, y, w, bh), "Resume", true, 24f)) { director.ClosePause(); return; }
        y += bh + gap;

        if (inDay)
        {
            if (ImGuiKit.Button(new Rect(x, y, w, bh), "Route Map", true, 22f)) { director.ShowRoute(); return; }
            y += bh + gap;
            if (ImGuiKit.Button(new Rect(x, y, w, bh), "Items (Tab)", true, 22f)) { director.ShowItemsFromPause(); return; }
            y += bh + gap;
        }

        if (ImGuiKit.Button(new Rect(x, y, w, bh), "Save and Exit to Title", true, 22f)) { director.QuitToTitle(); return; }
        y += bh + gap;
        ImGuiKit.Label(new Rect(x, y - 8 * s, w, 22 * s),
            inDay ? "Today's progress is not saved. The year resumes from the start of today." : "The year resumes from here.",
            15f, ImGuiKit.Muted, TextAnchor.MiddleLeft);
        y += 22 * s;

        string abandon = confirmAbandon ? "Really abandon the year? Click again" : "Abandon Year";
        if (ImGuiKit.Button(new Rect(x, y, w, bh), abandon, true, 22f))
        {
            if (confirmAbandon) { director.AbandonRun(); return; }
            confirmAbandon = true;
        }
        if (confirmAbandon)
            ImGuiKit.Label(new Rect(x, y + bh + 4 * s, w, 24 * s), "Coins in your purse are lost. You'll go to the upgrade shop.", 16f, new Color(1f, 0.6f, 0.55f), TextAnchor.MiddleLeft);
    }

    private void DrawHudSizeSlider(float x, float y, float w, float s)
    {
        ImGuiKit.Label(new Rect(x, y, 150 * s, 36 * s), $"HUD size {Mathf.RoundToInt(pendingScale * 100f)}%", 18f, Color.white, TextAnchor.MiddleLeft, true);
        pendingScale = GUI.HorizontalSlider(new Rect(x + 170 * s, y + 12 * s, w - 170 * s - 110 * s, 20 * s), pendingScale, ImGuiKit.MinUserScale, ImGuiKit.MaxUserScale);
        if (Event.current.rawType == EventType.MouseUp && !Mathf.Approximately(pendingScale, ImGuiKit.UserScale))
            ImGuiKit.UserScale = pendingScale;
        if (ImGuiKit.Button(new Rect(x + w - 96 * s, y, 96 * s, 36 * s), "Reset", true, 18f))
        {
            ImGuiKit.UserScale = 1f;
            pendingScale = 1f;
        }
    }
}
