using UnityEngine;

/// <summary>A random event: the blurred farm, the stranger's silhouette on the right, and an orange card with the story and your choices.</summary>
public class EventScreen : IRunScreen
{
    private readonly RunDirector director;

    public EventScreen(RunDirector director) { this.director = director; }

    public void Draw()
    {
        EventData evt = director.Plan != null ? director.Plan.evt : null;
        if (evt == null) return;
        float s = ImGuiKit.Scale;
        UiTheme theme = ImGuiKit.Theme;

        ImGuiKit.Backdrop(0f);
        if (theme != null && theme.eventSilhouette != null)
            GUI.DrawTexture(new Rect(0f, 0f, ImGuiKit.CanvasSize.x, ImGuiKit.CanvasSize.y), theme.eventSilhouette, ScaleMode.StretchToFill, true);

        Rect panel = new Rect(58f * s, 67f * s, 694f * s, 951f * s);
        ImGuiKit.Rounded(panel, ImGuiKit.Orange);
        float x = panel.x + 26f * s, w = panel.width - 52f * s;

        ImGuiKit.Heading(new Rect(x, panel.y + 24f * s, w, 100f * s), evt.title, 66f, ImGuiKit.Text);
        ImGuiKit.DashedRule(x, panel.y + 128f * s, w - 80f * s);
        // The coin count sits right under the story text (never lower than the first choice allows).
        float optH = 92f * s, gap = 14f * s;
        float optionsTop = panel.yMax - 26f * s - evt.options.Count * (optH + gap) + gap;
        float bodyH = ImGuiKit.TextHeight(evt.body, 34f, w);
        ImGuiKit.Label(new Rect(x, panel.y + 150f * s, w, bodyH + 8f * s), evt.body, 34f, ImGuiKit.Text, TextAnchor.UpperLeft);
        float coinY = Mathf.Min(panel.y + 150f * s + bodyH + 24f * s, optionsTop - 70f * s);
        ImGuiKit.IconValue(new Rect(x, coinY, 300f * s, 56f * s), theme != null ? theme.coin : null, director.Run.coins.ToString(), 32f, ImGuiKit.Text);

        // The choices stack up from the bottom of the card.
        float y = optionsTop;
        for (int i = 0; i < evt.options.Count; i++)
        {
            EventData.EventOption o = evt.options[i];
            string effects = "";
            if (o.effects != null)
                foreach (RunEffect e in o.effects) if (e != null) effects += (effects.Length > 0 ? ", " : "") + e.Describe();
            string label = o.label + (o.coinCost > 0 ? $"  (costs {o.coinCost})" : "") + (effects.Length > 0 ? $"  —  {effects}" : "");
            bool afford = director.Run.coins >= o.coinCost;
            if (OptionPill(new Rect(x + 8f * s, y, w - 16f * s, optH), label, afford, s))
            {
                director.ResolveEvent(i);
                return;
            }
            y += optH + gap;
        }
    }

    /// <summary>A wide cream pill whose text may wrap onto two lines (long effect descriptions).</summary>
    private static bool OptionPill(Rect r, string text, bool enabled, float s)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        ImGuiKit.Rounded(r, hover ? Color.white : enabled ? ImGuiKit.Cream : new Color(0.85f, 0.7f, 0.55f));
        ImGuiKit.Label(new Rect(r.x + 20f * s, r.y, r.width - 40f * s, r.height), text, 27f, enabled ? ImGuiKit.Text : ImGuiKit.Muted, TextAnchor.MiddleLeft);
        return enabled && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover && Consume();
    }

    private static bool Consume() => ImGuiKit.ConsumeClick();
}
