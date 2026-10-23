using UnityEngine;

/// <summary>The in-run menu (Esc): resume, route map, options (with How to Play and the item list), save and exit, or leave the year at any time.</summary>
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

        ImGuiKit.Backdrop(0f);
        DrawOrangeFade(s);

        ImGuiKit.Heading(new Rect(100f * s, 190f * s, 900f * s, 150f * s), "DOGSEND", 130f, ImGuiKit.Text);
        string where = inDay && director.Plan != null && director.Plan.node != null
            ? $"Year {year}  ·  {director.Plan.node.displayName}  ·  {(director.Plan.date.season != null ? director.Plan.date.season.displayName : "")} day {director.Plan.date.dayInSeason + 1}"
            : $"Year {year}  ·  Choosing the next day";
        ImGuiKit.Italic(new Rect(110f * s, 332f * s, 800f * s, 40f * s), where, 26f, ImGuiKit.Text);

        float x = 152f * s, w = 760f * s, h = 76f * s, step = 83f * s;
        float y = 408f * s;
        Color text = Color.white;

        if (ImGuiKit.TextItem(new Rect(x, y, w, h), "Resume", 54f, text)) { director.ClosePause(); return; }
        y += step;
        if (inDay)
        {
            if (ImGuiKit.TextItem(new Rect(x, y, w, h), "Route Map", 54f, text)) { director.ShowRoute(); return; }
            y += step;
        }
        if (ImGuiKit.TextItem(new Rect(x, y, w, h), "Options", 54f, text)) { director.ShowOptions(); return; }
        y += step;

        // Abandon Year sits right above Save and Exit, same size and column. The beta scene's "Reset Beta Slot" takes its place there.
        string danger = director.IsBeta
            ? (confirmAbandon ? "Click again to wipe the beta slot" : "Reset Beta Slot")
            : (confirmAbandon ? "Really abandon? Click again" : "Abandon Year");
        if (ImGuiKit.TextItem(new Rect(x, y, w, h), danger, 54f, confirmAbandon ? new Color(1f, 0.93f, 0.8f) : text))
        {
            if (confirmAbandon)
            {
                if (director.IsBeta) director.ResetBetaSlot(); else director.AbandonRun();
                return;
            }
            confirmAbandon = true;
        }
        y += step;

        if (ImGuiKit.TextItem(new Rect(x, y, w, h), "Save and Exit", 54f, text)) { director.QuitToTitle(); return; }
        y += step;

        // Footer: the warning for a pending abandon, else the save note.
        string note = confirmAbandon
            ? (director.IsBeta ? "The beta slot is wiped and a fresh free-roam year starts." : "Coins in your purse are lost. You'll go to the upgrade shop.")
            : inDay ? "Today's progress is not saved. The year resumes from the start of today." : "The year resumes from here.";
        ImGuiKit.Label(new Rect(x, y - 12f * s, w + 300f * s, 34f * s), note, 22f, confirmAbandon ? new Color(0.45f, 0.08f, 0.04f) : new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleLeft);
        y += 52f * s;

        if (director.IsBeta && inDay && ImGuiKit.TextItem(new Rect(x, y, w, 56f * s), "Back to Beta Map", 36f, text)) { director.LeaveBetaDay(); return; }
    }

    /// <summary>Orange wash on the left that fades to nothing towards the right, like the design.</summary>
    private static void DrawOrangeFade(float s)
    {
        Color c = ImGuiKit.Orange;
        Rect win = ImGuiKit.ScreenLocal;
        // Whole-pixel strips that touch but never overlap, so their alpha doesn't stack into visible lines.
        float x0 = Mathf.Floor(win.x), width = Mathf.Round(1300f * s + Mathf.Max(0f, -win.x)), step = Mathf.Max(2f, Mathf.Round(6f * s));
        for (float x = 0f; x < width; x += step)
        {
            c.a = 0.95f * Mathf.Pow(1f - x / width, 1.1f);
            ImGuiKit.Fill(new Rect(x0 + x, win.y, Mathf.Min(step, width - x), win.height), c);
        }
    }
}
