using UnityEngine;

/// <summary>The first screen: Start Game (opens the save slots), Options, How to Play, Credits, Quit. Drawn over the live, blurred farm.</summary>
public class MainMenuScreen : IRunScreen
{
    private readonly RunDirector director;
    private bool confirmQuit;

    public MainMenuScreen(RunDirector director) { this.director = director; }

    public void OnOpen() { confirmQuit = false; }

    /// <summary>The farm panorama stays sharp behind the menu; a soft dark fade on the left keeps the logo and buttons readable.</summary>
    private static void DrawScrim()
    {
        Rect screen = ImGuiKit.ScreenLocal;
        // Whole-pixel strips that touch but never overlap, so no seams show where the alpha would double up.
        const int strips = 24;
        int width = Mathf.RoundToInt(screen.width * 0.5f);
        for (int i = 0; i < strips; i++)
        {
            int x0 = width * i / strips, x1 = width * (i + 1) / strips;
            float a = 0.38f * (1f - i / (float)strips);
            ImGuiKit.Fill(new Rect(screen.x + x0, screen.y, x1 - x0, screen.height), new Color(0.08f, 0.05f, 0.02f, a));
        }
    }

    public void Draw()
    {
        float s = ImGuiKit.Scale;
        DrawScrim();

        ImGuiKit.Heading(new Rect(110f * s, 110f * s, 1100f * s, 190f * s), "DOGSEND", 175f, ImGuiKit.Text);

        // The first pill is lit until the pointer picks another, like the highlighted "Start Game" in the design.
        string[] labels = { "Start Game", "Options", "How to Play", "Credits", confirmQuit ? "Click again to quit" : "Quit" };
        float x = 135f * s, w = 445f * s, h = 92f * s, step = 115f * s;
        float y = 430f * s;
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
                case 3: director.ShowCredits(); return;
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
