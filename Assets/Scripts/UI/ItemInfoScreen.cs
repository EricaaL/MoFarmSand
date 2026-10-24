using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Inventory (Tab key): what you carry as a grid of cards on the right, the selected item's facts on the left.
/// <see cref="DrawBody"/> is shared with the Item List tab of the Options page, which lists every seed and crop.
/// </summary>
public class ItemInfoScreen : IRunScreen
{
    private readonly RunDirector director;
    private ItemData selected;
    private float scroll;

    public ItemInfoScreen(RunDirector director) { this.director = director; }

    /// <summary>Starts on the item in the active hotbar slot so the tab opens on what you're holding.</summary>
    public void OnOpen()
    {
        selected = director.Inventory != null ? director.Inventory.ActiveItem : null;
        scroll = 0f;
    }

    public void Draw()
    {
        float s = ImGuiKit.Scale;
        ImGuiKit.Backdrop(0f);
        if (ImGuiKit.BackButton(new Rect(42f * s, 36f * s, 130f * s, 130f * s))) { director.CloseItems(); return; }

        Rect panel = new Rect(209f * s, 119f * s, 1502f * s, 842f * s);
        ImGuiKit.TabRight(panel, "Inventory");
        ImGuiKit.Rounded(panel, ImGuiKit.Orange);
        DrawBody(panel, true);
    }

    /// <summary>Detail on the left (drawn straight onto <paramref name="panel"/>), card grid on the right. <paramref name="ownedOnly"/> hides what you don't carry.</summary>
    public void DrawBody(Rect panel, bool ownedOnly)
    {
        float s = ImGuiKit.Scale;
        GameDatabase db = director.Db;

        var items = new List<ItemData>();
        foreach (ItemData it in db.items.All)
        {
            if (!(it is SeedData) && !(it is CropData)) continue;
            if (ownedOnly && (director.Inventory == null || director.Inventory.CountOf(it) <= 0)) continue;
            items.Add(it);
        }
        SortBySeason(items);
        if (selected == null || !items.Contains(selected)) selected = items.Count > 0 ? items[0] : null;

        // Right: cream card grid.
        float pad = 22f * s;
        Rect grid = new Rect(panel.x + panel.width * 0.469f, panel.y + pad, panel.width * 0.515f, panel.height - pad * 2f);
        ImGuiKit.Rounded(grid, ImGuiKit.Cream);
        DrawGrid(grid, items, s);

        // Left: details.
        Rect detail = new Rect(panel.x + 34f * s, panel.y + 26f * s, grid.x - panel.x - 70f * s, panel.height - 52f * s);
        if (selected != null) DrawDetail(selected, detail, s);
        else ImGuiKit.Label(detail, ownedOnly ? "Your pockets are empty.\nHarvest or buy something first." : "Nothing to show yet.", 34f, ImGuiKit.Text, TextAnchor.UpperLeft);
    }

    /// <summary>Groups by the crop's best season in calendar order; inside a season the crops come first, then their seeds.</summary>
    private void SortBySeason(List<ItemData> items)
    {
        var seasons = director.Db.calendar.seasons;
        var keyed = new List<KeyValuePair<long, ItemData>>();
        for (int i = 0; i < items.Count; i++)
        {
            ItemData it = items[i];
            CropData crop = it as CropData ?? (it as SeedData)?.grows;
            int season = seasons.Count;
            if (crop != null)
            {
                SeasonData best = ItemInfoBuilder.BestSeason(crop, director.BuildItemContext(it), out _);
                int idx = best != null ? seasons.IndexOf(best) : -1;
                if (idx >= 0) season = idx;
            }
            int kind = it is CropData ? 0 : 1;
            keyed.Add(new KeyValuePair<long, ItemData>((season * 2L + kind) * 10000L + i, it));
        }
        keyed.Sort((a, b) => a.Key.CompareTo(b.Key));
        for (int i = 0; i < items.Count; i++) items[i] = keyed[i].Value;
    }

    private void DrawGrid(Rect grid, List<ItemData> items, float s)
    {
        const int cols = 3;
        float cardW = 224f * s, cardH = 312f * s, gapX = 19f * s, gapY = 21f * s, inset = 24f * s;
        float left = grid.x + (grid.width - (cols * cardW + (cols - 1) * gapX)) * 0.5f - 6f * s;
        int rows = Mathf.CeilToInt(items.Count / (float)cols);
        float contentH = inset + rows * (cardH + gapY);
        float maxScroll = Mathf.Max(0f, contentH - grid.height);
        Event e = Event.current;
        if (e.type == EventType.ScrollWheel && grid.Contains(e.mousePosition)) { scroll = Mathf.Clamp(scroll + e.delta.y * 30f, 0f, maxScroll); e.Use(); }
        scroll = Mathf.Clamp(scroll, 0f, maxScroll);

        GUI.BeginClip(grid);
        Vector2 mouse = e.mousePosition; // clip-relative inside BeginClip
        for (int i = 0; i < items.Count; i++)
        {
            ItemData it = items[i];
            Rect card = new Rect(left - grid.x + (i % cols) * (cardW + gapX), inset - scroll + (i / cols) * (cardH + gapY), cardW, cardH);
            if (card.yMax < 0f || card.y > grid.height) continue;

            if (it == selected) ImGuiKit.Rounded(new Rect(card.x - 6f * s, card.y - 6f * s, card.width + 12f * s, card.height + 12f * s), ImGuiKit.Brown);
            ImGuiKit.Rounded(card, ImGuiKit.Orange);
            Rect art = new Rect(card.x + 11f * s, card.y + 18f * s, card.width - 22f * s, 232f * s);
            ImGuiKit.Rounded(art, ImGuiKit.PanelLight);
            ItemIconDrawer.Draw(new Rect(art.x + 8f * s, art.y + 8f * s, art.width - 16f * s, art.height - 16f * s), it);

            int owned = director.Inventory != null ? director.Inventory.CountOf(it) : 0;
            bool locked = director.Run != null && CropAvailability.LockedBehind(it, director.Run.unlockedIds, director.Db) != null;
            string tag = locked ? "locked" : owned > 0 ? "x" + owned : "-";
            ImGuiKit.Label(new Rect(card.x + 10f * s, card.yMax - 56f * s, card.width - 26f * s, 46f * s), tag, 32f, ImGuiKit.Text, TextAnchor.MiddleRight);

            if (e.type == EventType.MouseDown && e.button == 0 && card.Contains(mouse))
            {
                selected = it;
                e.Use();
            }
        }
        GUI.EndClip();

        if (maxScroll > 0f)
        {
            Rect track = new Rect(grid.xMax - 30f * s, grid.y + 34f * s, 14f * s, grid.height - 68f * s);
            float knobH = Mathf.Max(50f * s, track.height * grid.height / contentH);
            ImGuiKit.Rounded(new Rect(track.x, track.y + (track.height - knobH) * (scroll / maxScroll), track.width, knobH), ImGuiKit.Orange);
        }
    }

    private void DrawDetail(ItemData item, Rect r, float s)
    {
        ItemInfo info = ItemInfoBuilder.Build(item, director.BuildItemContext(item));
        int owned = director.Inventory != null ? director.Inventory.CountOf(item) : 0;
        ItemDetailCard.Draw(r, info, owned, 1f);
    }
}
