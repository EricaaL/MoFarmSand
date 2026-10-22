using UnityEngine;

/// <summary>The Controls page of the Esc menu: every key and mouse button, grouped by what it does.</summary>
public class ControlsScreen : IRunScreen
{
    private readonly RunDirector director;

    private static readonly (string group, (string key, string action)[] rows)[] Groups =
    {
        ("Moving", new[]
        {
            ("W A S D", "Walk around"),
            ("Left Shift", "Sprint"),
        }),
        ("Farming", new[]
        {
            ("E", "Plant, harvest, talk to the merchant, ring the bell"),
            ("Left Click", "Harvest a ripe plot near you"),
        }),
        ("Items", new[]
        {
            ("1 - 8", "Pick a hotbar slot"),
            ("Mouse Wheel", "Cycle hotbar slots"),
            ("Drag a slot off the bar", "Use it where you drop it"),
            ("Tab", "Items info"),
        }),
        ("Crops", new[]
        {
            ("Hold Left Click", "Aim a thrown crop, release to throw"),
            ("Hold Left Click", "Auto-fire peas and corn toward the cursor"),
            ("Right Click", "Cancel an aim, or eat a support crop for a shield"),
        }),
        ("Other", new[]
        {
            ("Q (hold)", "Dance wheel"),
            ("Esc", "Menu / go back"),
        }),
    };

    public ControlsScreen(RunDirector director) { this.director = director; }

    public void OnOpen() { }

    /// <summary>Draws every key group starting at (x, y) in a column <paramref name="w"/> wide; returns the y below the last row. Shared with How to Play.</summary>
    public static float DrawGroups(float x, float y, float w)
    {
        float s = ImGuiKit.Scale;
        float keyW = Mathf.Min(280 * s, w * 0.4f);
        float lineH = 30 * s, headerH = 34 * s;

        foreach (var (group, rows) in Groups)
        {
            ImGuiKit.Label(new Rect(x, y, w, headerH), group, 20f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);
            y += headerH;
            foreach (var (key, action) in rows)
            {
                ImGuiKit.Label(new Rect(x + 16 * s, y, keyW, lineH), key, 18f, Color.white, TextAnchor.MiddleLeft, true);
                ImGuiKit.Label(new Rect(x + 16 * s + keyW, y, w - keyW - 16 * s, lineH), action, 18f, ImGuiKit.Muted, TextAnchor.MiddleLeft);
                y += lineH;
            }
            y += 8 * s;
        }
        return y;
    }

    public void Draw()
    {
        float s = ImGuiKit.Scale;
        Rect panel = ImGuiKit.Modal(900f, 720f, "Controls");
        ImGuiKit.Label(new Rect(panel.xMax - 420 * s, panel.y + 26 * s, 396 * s, 30 * s), "Esc to go back", 18f, ImGuiKit.Muted, TextAnchor.MiddleRight);

        DrawGroups(panel.x + 40 * s, panel.y + 90 * s, panel.width - 80 * s);

        if (ImGuiKit.Button(new Rect(panel.xMax - 190 * s, panel.yMax - 66 * s, 166 * s, 44 * s), "Back", true, 20f)) director.CloseControls();
    }
}
