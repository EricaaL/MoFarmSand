using UnityEngine;

/// <summary>Options page, reachable from the main menu and the Esc menu: master volume and UI size.</summary>
public class OptionsScreen : IRunScreen
{
    private readonly RunDirector director;

    // The UI size slider edits this while dragging and it is applied on release, so the panel doesn't rescale under the cursor.
    private float pendingScale = 1f;

    public OptionsScreen(RunDirector director) { this.director = director; }

    public void OnOpen() { pendingScale = ImGuiKit.UserScale; }

    public void Draw()
    {
        float s = ImGuiKit.Scale;
        Rect panel = ImGuiKit.Modal(720f, 420f, "Options");
        ImGuiKit.Label(new Rect(panel.xMax - 420 * s, panel.y + 26 * s, 396 * s, 30 * s), "Esc to go back", 18f, ImGuiKit.Muted, TextAnchor.MiddleRight);

        float x = panel.x + 40 * s, w = panel.width - 80 * s;
        float y = panel.y + 110 * s;

        DrawVolume(x, y, w, s);
        y += 64 * s;
        DrawUiSize(x, y, w, s);
        y += 64 * s;
        DrawTips(x, y, w, s);

        if (ImGuiKit.Button(new Rect(panel.xMax - 190 * s, panel.yMax - 66 * s, 166 * s, 44 * s), "Back", true, 20f)) director.CloseOptions();
    }

    private void DrawVolume(float x, float y, float w, float s)
    {
        float volume = GameSettings.MasterVolume;
        ImGuiKit.Label(new Rect(x, y, 200 * s, 36 * s), $"Sound {Mathf.RoundToInt(volume * 100f)}%", 20f, Color.white, TextAnchor.MiddleLeft, true);
        float next = GUI.HorizontalSlider(new Rect(x + 220 * s, y + 12 * s, w - 220 * s - 110 * s, 20 * s), volume, 0f, 1f);
        if (!Mathf.Approximately(next, volume)) GameSettings.MasterVolume = next;
        if (ImGuiKit.Button(new Rect(x + w - 96 * s, y, 96 * s, 36 * s), "Reset", true, 18f)) GameSettings.MasterVolume = 1f;
    }

    private void DrawTips(float x, float y, float w, float s)
    {
        bool on = GameSettings.TipsEnabled;
        ImGuiKit.Label(new Rect(x, y, 200 * s, 36 * s), "Tips", 20f, Color.white, TextAnchor.MiddleLeft, true);
        if (ImGuiKit.Button(new Rect(x + 220 * s, y, 150 * s, 36 * s), on ? "On" : "Off", true, 18f)) GameSettings.TipsEnabled = !on;
        if (ImGuiKit.Button(new Rect(x + w - 190 * s, y, 190 * s, 36 * s), "Show tips again", true, 18f)) GameSettings.ResetTips();
    }

    private void DrawUiSize(float x, float y, float w, float s)
    {
        ImGuiKit.Label(new Rect(x, y, 200 * s, 36 * s), $"UI size {Mathf.RoundToInt(pendingScale * 100f)}%", 20f, Color.white, TextAnchor.MiddleLeft, true);
        pendingScale = GUI.HorizontalSlider(new Rect(x + 220 * s, y + 12 * s, w - 220 * s - 110 * s, 20 * s), pendingScale, ImGuiKit.MinUserScale, ImGuiKit.MaxUserScale);
        if (Event.current.rawType == EventType.MouseUp && !Mathf.Approximately(pendingScale, ImGuiKit.UserScale))
            ImGuiKit.UserScale = pendingScale;
        if (ImGuiKit.Button(new Rect(x + w - 96 * s, y, 96 * s, 36 * s), "Reset", true, 18f))
        {
            ImGuiKit.UserScale = 1f;
            pendingScale = 1f;
        }
    }
}
