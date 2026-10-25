using UnityEngine;

/// <summary>The Credits tab of the Options page: the team and every third-party asset, from <see cref="CreditsData"/>. Mouse wheel scrolls.</summary>
public class CreditsScreen
{
    private float scroll;

    public void OnOpen() { scroll = 0f; }

    /// <summary>Name on top, the detail wrapped below it; a detail wider than the column (about 13 px a character at the 26 px size) gets a second row.</summary>
    private static float EntryHeight(CreditsData.Line line, float textW, float nameH, float detailRowH, float s)
    {
        if (string.IsNullOrEmpty(line.detail)) return nameH;
        int rows = Mathf.Max(1, Mathf.CeilToInt(line.detail.Length * 13f * s / textW));
        return nameH + rows * detailRowH + 4f * s;
    }

    /// <summary>Draws into <paramref name="area"/> (the cream inner panel).</summary>
    public void DrawBody(Rect area, CreditsData data)
    {
        float s = ImGuiKit.Scale;
        if (data == null || data.sections.Count == 0)
        {
            ImGuiKit.Label(area, "No credits yet.", 32f, ImGuiKit.Muted, TextAnchor.MiddleCenter);
            return;
        }

        Rect view = new Rect(area.x + 50f * s, area.y + 24f * s, area.width - 100f * s, area.height - 48f * s);
        float nameH = 40f * s, detailRowH = 34f * s, headH = 76f * s, gap = 26f * s, entryGap = 14f * s;
        float textW = view.width - 40f * s;
        float contentH = 0f;
        foreach (CreditsData.Section sec in data.sections)
        {
            contentH += headH + gap;
            foreach (CreditsData.Line line in sec.lines) contentH += EntryHeight(line, textW, nameH, detailRowH, s) + entryGap;
        }

        float maxScroll = Mathf.Max(0f, contentH - view.height);
        Event e = Event.current;
        if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition)) { scroll = Mathf.Clamp(scroll + e.delta.y * 30f, 0f, maxScroll); e.Use(); }
        scroll = Mathf.Clamp(scroll, 0f, maxScroll);

        GUI.BeginClip(view);
        float y = -scroll;
        foreach (CreditsData.Section sec in data.sections)
        {
            if (y + headH > 0f && y < view.height)
            {
                ImGuiKit.Heading(new Rect(0f, y, view.width, 64f * s), sec.heading, 46f, ImGuiKit.Text);
                ImGuiKit.DashedRule(0f, y + 66f * s, Mathf.Min(view.width, 520f * s));
            }
            y += headH;
            foreach (CreditsData.Line line in sec.lines)
            {
                float h = EntryHeight(line, textW, nameH, detailRowH, s);
                if (y + h > 0f && y < view.height)
                {
                    ImGuiKit.Label(new Rect(0f, y, textW, nameH), line.name, 30f, ImGuiKit.Text, TextAnchor.MiddleLeft, true, false);
                    if (!string.IsNullOrEmpty(line.detail))
                        ImGuiKit.Label(new Rect(0f, y + nameH, textW, h - nameH), line.detail, 26f, ImGuiKit.Muted, TextAnchor.UpperLeft);
                }
                y += h + entryGap;
            }
            y += gap;
        }
        GUI.EndClip();

        if (maxScroll > 0f)
        {
            Rect track = new Rect(view.xMax - 14f * s, view.y + 10f * s, 14f * s, view.height - 20f * s);
            float knobH = Mathf.Max(50f * s, track.height * view.height / contentH);
            ImGuiKit.Rounded(new Rect(track.x, track.y + (track.height - knobH) * (scroll / maxScroll), track.width, knobH), ImGuiKit.Orange);
        }
    }
}
