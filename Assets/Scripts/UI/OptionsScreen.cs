using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The tabbed settings page, reachable from the main menu and the Esc menu: Options (sound, tips, HUD size), How to Play,
/// and the Item List of every seed and crop.
/// </summary>
public class OptionsScreen : IRunScreen
{
    public const int TabOptions = 0, TabHowToPlay = 1, TabItems = 2;

    private readonly RunDirector director;
    private readonly HowToPlayScreen howToPlay = new HowToPlayScreen();
    private int tab;

    public OptionsScreen(RunDirector director) { this.director = director; }

    public void OnOpen(int startTab = TabOptions)
    {
        tab = startTab;
        howToPlay.OnOpen();
        director.ItemList.OnOpen();
    }

    public void Draw()
    {
        float s = ImGuiKit.Scale;
        ImGuiKit.Backdrop(director.IsTitleView ? UiBackdrop.LiveInterval : 0f);   // keeps turning behind the title's options
        if (ImGuiKit.BackButton(new Rect(42f * s, 36f * s, 130f * s, 130f * s))) { director.CloseOptions(); return; }

        // On the title screen there is no day, so the Item List describes the first day of spring.
        var names = new List<string> { "Options", "How to Play", "Item List" };
        if (tab >= names.Count) tab = 0;

        Rect outer = new Rect(209f * s, 119f * s, 1502f * s, 842f * s);
        tab = ImGuiKit.Tabs(outer.x, outer.y, names.ToArray(), tab);
        ImGuiKit.Rounded(outer, ImGuiKit.Orange);
        Rect inner = new Rect(outer.x + 26f * s, outer.y + 22f * s, outer.width - 52f * s, outer.height - 44f * s);
        ImGuiKit.Rounded(inner, ImGuiKit.Cream);

        switch (tab)
        {
            case TabHowToPlay: howToPlay.DrawBody(inner); break;
            case TabItems: director.ItemList.DrawBody(outer, false); break;
            default: DrawSettings(inner, s); break;
        }
    }

    private void DrawSettings(Rect inner, float s)
    {
        float x = inner.x + 55f * s, w = inner.width - 110f * s;
        float y = inner.y + 50f * s, row = 100f * s;

        DrawVolume(x, y, w, s);
        DrawHudSize(x, y + row, w, s);
        DrawTips(x, y + row * 2f, w, s);
    }

    private static void DrawVolume(float x, float y, float w, float s)
    {
        float volume = GameSettings.MasterVolume;
        ImGuiKit.Label(new Rect(x, y, 300f * s, 64f * s), "Sound", 40f, ImGuiKit.Text, TextAnchor.MiddleLeft);
        float next = ImGuiKit.Slider(new Rect(x + 330f * s, y + 8f * s, w - 330f * s - 330f * s, 48f * s), volume, 0f, 1f);
        if (!Mathf.Approximately(next, volume)) GameSettings.MasterVolume = next;
        ImGuiKit.Label(new Rect(x + w - 310f * s, y, 120f * s, 64f * s), $"{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%", 32f, ImGuiKit.Muted, TextAnchor.MiddleRight);
        if (ImGuiKit.Pill(new Rect(x + w - 170f * s, y, 170f * s, 64f * s), "Reset", ImGuiKit.PillKind.Light, true, 26f)) GameSettings.MasterVolume = 1f;
    }

    private static void DrawHudSize(float x, float y, float w, float s)
    {
        float scale = ImGuiKit.UserScale;
        ImGuiKit.Label(new Rect(x, y, 300f * s, 64f * s), "HUD size", 40f, ImGuiKit.Text, TextAnchor.MiddleLeft);
        float next = ImGuiKit.Slider(new Rect(x + 330f * s, y + 8f * s, w - 330f * s - 330f * s, 48f * s), scale, ImGuiKit.MinUserScale, ImGuiKit.MaxUserScale);
        if (!Mathf.Approximately(next, scale)) ImGuiKit.UserScale = next;
        ImGuiKit.Label(new Rect(x + w - 310f * s, y, 120f * s, 64f * s), $"{Mathf.RoundToInt(ImGuiKit.UserScale * 100f)}%", 32f, ImGuiKit.Muted, TextAnchor.MiddleRight);
        if (ImGuiKit.Pill(new Rect(x + w - 170f * s, y, 170f * s, 64f * s), "Reset", ImGuiKit.PillKind.Light, true, 26f)) ImGuiKit.UserScale = 1f;
    }

    private static void DrawTips(float x, float y, float w, float s)
    {
        bool on = GameSettings.TipsEnabled;
        ImGuiKit.Label(new Rect(x, y, 300f * s, 64f * s), "Tips", 40f, ImGuiKit.Text, TextAnchor.MiddleLeft);
        if (ImGuiKit.Pill(new Rect(x + 330f * s, y, 200f * s, 64f * s), on ? "On" : "Off", on ? ImGuiKit.PillKind.Primary : ImGuiKit.PillKind.Gray, true, 28f)) GameSettings.TipsEnabled = !on;
        if (ImGuiKit.Pill(new Rect(x + w - 330f * s, y, 330f * s, 64f * s), "Show tips again", ImGuiKit.PillKind.Light, true, 26f)) GameSettings.ResetTips();
    }
}
