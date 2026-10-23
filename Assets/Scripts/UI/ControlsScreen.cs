using UnityEngine;

/// <summary>Every key and mouse button, grouped by what it does. Drawn by the Controls page of How to Play.</summary>
public static class ControlsScreen
{
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
            ("Tab", "Inventory"),
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

    /// <summary>Draws every key group starting at (x, y) in a column <paramref name="w"/> wide; returns the y below the last row.</summary>
    public static float DrawGroups(float x, float y, float w)
    {
        float s = ImGuiKit.Scale;
        float keyW = Mathf.Min(330f * s, w * 0.4f);
        float lineH = 34f * s, headerH = 38f * s;

        foreach (var (group, rows) in Groups)
        {
            ImGuiKit.Label(new Rect(x, y, w, headerH), group, 26f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);
            y += headerH;
            foreach (var (key, action) in rows)
            {
                ImGuiKit.Label(new Rect(x + 16 * s, y, keyW, lineH), key, 22f, ImGuiKit.Text, TextAnchor.MiddleLeft, true);
                ImGuiKit.Label(new Rect(x + 16 * s + keyW, y, w - keyW - 16 * s, lineH), action, 22f, ImGuiKit.Muted, TextAnchor.MiddleLeft);
                y += lineH;
            }
            y += 6 * s;
        }
        return y;
    }
}
