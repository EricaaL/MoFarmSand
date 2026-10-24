using UnityEngine;

/// <summary>
/// The item description block: display-font title, "owned N", a dashed rule, price, description, grow time and fullness.
/// Drawn straight onto an orange panel. The inventory (Tab) shows it at full size and the hotbar hover tooltip shows the same
/// layout scaled down by <c>k</c>, so the two always look alike.
/// </summary>
public static class ItemDetailCard
{
    // Extra lines worth text: price, grow time and strength already have rows, so only a lock notice remains.
    private static readonly string[] KeyLabels = { "Locked" };

    /// <summary>Height the card needs for <paramref name="info"/> in a column <paramref name="width"/> wide.</summary>
    public static float Measure(ItemInfo info, float width, float k, bool showSummary = true) => Layout(new Rect(0f, 0f, width, 100000f), info, 0, k, false, showSummary);

    /// <summary>Draws the card into <paramref name="r"/> (only the top-left and width matter). Returns the height used.</summary>
    public static float Draw(Rect r, ItemInfo info, int owned, float k, bool showSummary = true) => Layout(r, info, owned, k, true, showSummary);

    /// <summary>The icon of the season the crop grows best in (flower, sun, leaf, snowflake); the plain sun if there is none.</summary>
    public static Texture GrowIcon(ItemInfo info, UiTheme theme)
        => info.bestSeason != null && info.bestSeason.icon != null ? info.bestSeason.icon : theme != null ? theme.growIcon : null;

    private static float Layout(Rect r, ItemInfo info, int owned, float k, bool draw, bool showSummary)
    {
        float s = ImGuiKit.Scale * k;   // every authored size below is multiplied by k through this scale
        UiTheme theme = ImGuiKit.Theme;
        float x = r.x, w = r.width;
        float y = r.y;

        // Title: shrink a long name until it fits on one line.
        float titleSize = 70f * k;
        while (titleSize > 22f * k && ImGuiKit.TextWidth(info.title, titleSize, ImGuiKit.Face.Display) > w) titleSize -= 2f * k;
        if (draw)
        {
            ImGuiKit.Heading(new Rect(x, y, w + 20f * s, 100f * s), info.title, titleSize, ImGuiKit.Text);
            ImGuiKit.Italic(new Rect(x, y + 92f * s, w, 44f * s), $"owned {owned}", 32f * k, ImGuiKit.Muted);
            ImGuiKit.DashedRule(x, y + 140f * s, w);
        }
        y += 158f * s;

        if (info.price >= 0)
        {
            if (draw) ImGuiKit.IconValue(new Rect(x, y, w, 62f * s), theme != null ? theme.coin : null, info.price.ToString(), 40f * k, ImGuiKit.Text);
            y += 70f * s;
        }
        if (showSummary && !string.IsNullOrEmpty(info.summary))
        {
            float h = ImGuiKit.TextHeight(info.summary, 26f * k, w);
            if (draw) ImGuiKit.Label(new Rect(x, y, w, h + 4f * s), info.summary, 26f * k, ImGuiKit.Text, TextAnchor.UpperLeft);
            y += h + 18f * s;
        }
        if (!string.IsNullOrEmpty(info.growTime))
        {
            if (draw)
            {
                // The sun is orange on orange here: sit it on a cream disc like the design.
                ImGuiKit.Circle(new Vector2(x + 38f * s, y + 38f * s), 40f * s, ImGuiKit.Cream);
                ImGuiKit.IconValue(new Rect(x, y, w, 76f * s), GrowIcon(info, theme), info.growTime, 38f * k, ImGuiKit.Text);
            }
            y += 84f * s;
        }
        if (!string.IsNullOrEmpty(info.fullness))
        {
            if (draw) ImGuiKit.IconValue(new Rect(x, y, w, 76f * s), theme != null ? theme.bread : null, info.fullness, 38f * k, ImGuiKit.Text);
            y += 84f * s;
        }

        foreach (InfoLine line in info.lines)
        {
            if (System.Array.IndexOf(KeyLabels, line.label) < 0) continue;
            float h = Mathf.Max(ImGuiKit.TextHeight(line.value, 22f * k, w, true), 28f * s);
            if (draw)
            {
                ImGuiKit.Label(new Rect(x, y, w, 28f * s), line.label, 20f * k, ImGuiKit.Muted, TextAnchor.UpperLeft);
                ImGuiKit.Label(new Rect(x, y + 24f * s, w, h + 4f * s), line.value, 22f * k, line.tone == InfoTone.Bad ? new Color(0.7f, 0.15f, 0.1f) : ImGuiKit.Text, TextAnchor.UpperLeft, true);
            }
            y += h + 34f * s;
        }
        return y - r.y;
    }
}
