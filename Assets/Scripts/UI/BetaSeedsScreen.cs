using System.Collections.Generic;
using UnityEngine;

/// <summary>Beta-scene tool: pick any seed and add it to the hotbar, so every crop can be tested without visiting the merchant.</summary>
public class BetaSeedsScreen : IRunScreen
{
    private static readonly int[] Amounts = { 1, 5, 10 };

    private readonly RunDirector director;
    private readonly List<SeedData> seeds = new List<SeedData>();
    private string message = string.Empty;
    private float messageUntil;

    public BetaSeedsScreen(RunDirector director) { this.director = director; }

    public void OnOpen()
    {
        message = string.Empty;
        seeds.Clear();
        if (director.Db == null || director.Db.items == null) return;
        foreach (ItemData item in director.Db.items.All)
            if (item is SeedData seed && seed.grows != null) seeds.Add(seed);
    }

    public void Draw()
    {
        float s = ImGuiKit.Scale;
        Rect panel = ImGuiKit.Modal(820f, 720f, "Beta: Get Seeds");
        ImGuiKit.Label(new Rect(panel.xMax - 420 * s, panel.y + 26 * s, 396 * s, 30 * s), "Esc to go back", 18f, ImGuiKit.Muted, TextAnchor.MiddleRight);

        float x = panel.x + 40 * s, w = panel.width - 80 * s;
        float top = panel.y + 100 * s;
        float bottom = panel.yMax - 140 * s;
        float gap = 6 * s;
        float rowH = Mathf.Clamp((bottom - top) / Mathf.Max(1, seeds.Count) - gap, 30 * s, 48 * s);

        float y = top;
        foreach (SeedData seed in seeds)
        {
            DrawRow(seed, new Rect(x, y, w, rowH), s);
            y += rowH + gap;
        }

        // Status line: the last thing that happened.
        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            ImGuiKit.Label(new Rect(x, panel.yMax - 126 * s, w, 30 * s), message, 20f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);

        if (ImGuiKit.Button(new Rect(x, panel.yMax - 66 * s, 300 * s, 44 * s), "One of each seed", seeds.Count > 0, 20f)) GiveEach();
        if (ImGuiKit.Button(new Rect(panel.xMax - 190 * s, panel.yMax - 66 * s, 166 * s, 44 * s), "Close", true, 20f)) director.CloseBetaSeeds();
    }

    private void DrawRow(SeedData seed, Rect row, float s)
    {
        ImGuiKit.Rounded(row, ImGuiKit.PanelLight);
        ImGuiKit.Rounded(new Rect(row.x + 4 * s, row.y + 4 * s, 8 * s, row.height - 8 * s), seed.ItemColor);

        ImGuiKit.Label(new Rect(row.x + 24 * s, row.y, 300 * s, row.height), seed.DisplayName, 22f, ImGuiKit.Text, TextAnchor.MiddleLeft, true);
        int have = director.Inventory != null ? director.Inventory.CountOf(seed) : 0;
        ImGuiKit.Label(new Rect(row.x + 330 * s, row.y, 140 * s, row.height), have > 0 ? $"have {have}" : string.Empty, 18f, ImGuiKit.Muted, TextAnchor.MiddleLeft);

        float bw = 76 * s, bh = row.height - 8 * s;
        float bx = row.xMax - (bw + 8 * s) * Amounts.Length;
        foreach (int amount in Amounts)
        {
            if (ImGuiKit.Button(new Rect(bx, row.y + 4 * s, bw, bh), "+" + amount, true, 20f)) Give(seed, amount);
            bx += bw + 8 * s;
        }
    }

    private bool Give(SeedData seed, int amount)
    {
        if (director.Inventory == null || !director.Inventory.AddItem(seed, amount))
        {
            Say("Hotbar is full: use up or drop a slot first");
            return false;
        }
        Say($"+{amount} {seed.DisplayName}");
        return true;
    }

    private void GiveEach()
    {
        int given = 0;
        foreach (SeedData seed in seeds)
        {
            if (director.Inventory == null || !director.Inventory.AddItem(seed, 1)) break;
            given++;
        }
        Say(given == seeds.Count ? $"Added 1 of each ({given} seeds)" : $"Hotbar filled up after {given} of {seeds.Count} seeds");
    }

    private void Say(string text)
    {
        message = text;
        messageUntil = Time.unscaledTime + 3f;
    }
}
