using UnityEngine;

public class EventScreen : IRunScreen
{
    private readonly RunDirector director;

    public EventScreen(RunDirector director) { this.director = director; }

    public void Draw()
    {
        EventData evt = director.Plan != null ? director.Plan.evt : null;
        if (evt == null) return;
        float s = ImGuiKit.Scale;
        Rect panel = ImGuiKit.Modal(900f, 220f + 90f * evt.options.Count, evt.title);

        ImGuiKit.Label(new Rect(panel.x + 24 * s, panel.y + 80 * s, panel.width - 48 * s, 90 * s), evt.body, 22f, Color.white, TextAnchor.UpperLeft);
        ImGuiKit.Label(new Rect(panel.x + 24 * s, panel.y + 160 * s, panel.width - 48 * s, 26 * s), $"Purse: {director.Run.coins} coins", 18f, ImGuiKit.Muted);

        for (int i = 0; i < evt.options.Count; i++)
        {
            EventData.EventOption o = evt.options[i];
            string effects = "";
            if (o.effects != null)
                foreach (RunEffect e in o.effects) if (e != null) effects += (effects.Length > 0 ? ", " : "") + e.Describe();
            string label = o.label + (o.coinCost > 0 ? $"  (costs {o.coinCost})" : "") + (effects.Length > 0 ? $"  —  {effects}" : "");
            bool afford = director.Run.coins >= o.coinCost;
            if (ImGuiKit.Button(new Rect(panel.x + 24 * s, panel.y + (200 + i * 84) * s, panel.width - 48 * s, 68 * s), label, afford, 20f))
            {
                director.ResolveEvent(i);
                return;
            }
        }
    }
}
