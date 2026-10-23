using UnityEngine;

/// <summary>The first screen: Start Game (opens the save slots), Options, How to Play, Quit. Drawn over the live, blurred farm.</summary>
public class MainMenuScreen : IRunScreen
{
    private readonly RunDirector director;
    private bool confirmQuit;

    public MainMenuScreen(RunDirector director) { this.director = director; }

    public void OnOpen() { confirmQuit = false; }

    public void Draw()
    {
        float s = ImGuiKit.Scale;
        ImGuiKit.Backdrop(1.5f);

        ImGuiKit.Heading(new Rect(110f * s, 110f * s, 1100f * s, 190f * s), "DOGSEND", 175f, ImGuiKit.Text);

        // The first pill is lit until the pointer picks another, like the highlighted "Start Game" in the design.
        string[] labels = { "Start Game", "Options", "How to Play", confirmQuit ? "Click again to quit" : "Quit" };
        float x = 135f * s, w = 445f * s, h = 100f * s, step = 135f * s;
        float y = 458f * s;
        int hovered = -1;
        for (int i = 0; i < labels.Length; i++)
            if (new Rect(x, y + i * step, w, h).Contains(Event.current.mousePosition)) hovered = i;

        for (int i = 0; i < labels.Length; i++)
        {
            bool lit = hovered == i || (hovered < 0 && i == 0);
            if (!ImGuiKit.Pill(new Rect(x, y + i * step, w, h), labels[i], ImGuiKit.PillKind.Light, true, 46f, lit, TextAnchor.MiddleLeft)) continue;
            switch (i)
            {
                case 0: director.ShowTitle(); return;
                case 1: director.ShowOptions(); return;
                case 2: director.ShowHowToPlay(); return;
                default:
                    if (confirmQuit) { director.QuitGame(); return; }
                    confirmQuit = true;
                    break;
            }
        }

        Vector2 canvas = ImGuiKit.CanvasSize;
        ImGuiKit.Label(new Rect(canvas.x - 420f * s, canvas.y - 110f * s, 390f * s, 90f * s),
            $"MoFarmSand\nv{Application.version}", 24f, new Color(0.25f, 0.25f, 0.28f, 0.9f), TextAnchor.LowerRight);
    }
}
