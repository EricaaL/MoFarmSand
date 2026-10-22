using UnityEngine;

/// <summary>The first screen: Play (opens the save slots), Options, Quit Game.</summary>
public class MainMenuScreen : IRunScreen
{
    private readonly RunDirector director;
    private bool confirmQuit;

    public MainMenuScreen(RunDirector director) { this.director = director; }

    public void OnOpen() { confirmQuit = false; }

    public void Draw()
    {
        Rect panel = ImGuiKit.Modal(560f, 646f, "DOGSEND");
        float s = ImGuiKit.Scale;
        float x = panel.x + 40 * s, w = panel.width - 80 * s;
        float y = panel.y + 90 * s;
        float bh = 68 * s, gap = 18 * s;

        ImGuiKit.Label(new Rect(x, y, w, 30 * s), "A creepy-cute farming roguelike", 18f, ImGuiKit.Muted, TextAnchor.MiddleLeft);
        y += 56 * s;

        if (ImGuiKit.Button(new Rect(x, y, w, bh), "Play", true, 30f)) { director.ShowTitle(); return; }
        y += bh + gap;

        if (ImGuiKit.Button(new Rect(x, y, w, bh), "How to Play", true, 26f)) { director.ShowHowToPlay(); return; }
        y += bh + gap;

        if (ImGuiKit.Button(new Rect(x, y, w, bh), "Options", true, 26f)) { director.ShowOptions(); return; }
        y += bh + gap;

        if (ImGuiKit.Button(new Rect(x, y, w, bh), confirmQuit ? "Click again to quit" : "Quit Game", true, 26f))
        {
            if (confirmQuit) { director.QuitGame(); return; }
            confirmQuit = true;
        }
    }
}
