using System.Collections.Generic;
using UnityEngine;

/// <summary>The Items tab (Tab key): every seed and crop on the left, what the selected one does on the right.</summary>
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
        GameDatabase db = director.Db;
        float s = ImGuiKit.Scale;
        Rect panel = ImGuiKit.Modal(1200f, 720f, "Items");
        ImGuiKit.Label(new Rect(panel.xMax - 420 * s, panel.y + 26 * s, 396 * s, 30 * s), "Tab or Esc to close", 18f, ImGuiKit.Muted, TextAnchor.MiddleRight);

        List<ItemData> seeds = new List<ItemData>(), crops = new List<ItemData>();
        foreach (ItemData it in db.items.All)
        {
            if (it is SeedData) seeds.Add(it);
            else if (it is CropData) crops.Add(it);
        }
        if (selected == null) selected = seeds.Count > 0 ? seeds[0] : crops.Count > 0 ? crops[0] : null;

        float listW = 330 * s;
        float top = panel.y + 90 * s;
        float bottom = panel.yMax - 24 * s;
        Rect list = new Rect(panel.x + 24 * s, top, listW, bottom - top);
        ImGuiKit.Fill(list, new Color(0.1f, 0.09f, 0.16f, 1f));

        // Mouse wheel scrolls the list if it ever outgrows the panel.
        float rowH = 52 * s, headerH = 30 * s;
        float contentH = headerH * 2 + (seeds.Count + crops.Count) * rowH;
        float maxScroll = Mathf.Max(0f, contentH - list.height);
        Event e = Event.current;
        if (e.type == EventType.ScrollWheel && list.Contains(e.mousePosition)) { scroll = Mathf.Clamp(scroll + e.delta.y * 30f, 0f, maxScroll); e.Use(); }

        GUI.BeginClip(list);
        float y = -scroll;
        y = DrawGroup("Seeds", seeds, list.width, y, headerH, rowH, list.position);
        y = DrawGroup("Crops", crops, list.width, y, headerH, rowH, list.position);
        GUI.EndClip();

        // Details.
        Rect detail = new Rect(list.xMax + 24 * s, top, panel.xMax - list.xMax - 48 * s, bottom - top);
        if (selected != null) DrawDetail(selected, detail);

        if (ImGuiKit.Button(new Rect(panel.xMax - 190 * s, panel.yMax - 66 * s, 166 * s, 44 * s), "Close", true, 20f)) director.CloseItems();
    }

    private float DrawGroup(string name, List<ItemData> items, float width, float y, float headerH, float rowH, Vector2 origin)
    {
        float s = ImGuiKit.Scale;
        ImGuiKit.Label(new Rect(12 * s, y, width - 24 * s, headerH), name, 20f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);
        y += headerH;
        foreach (ItemData it in items)
        {
            Rect row = new Rect(6 * s, y, width - 12 * s, rowH - 4 * s);
            int owned = director.Inventory != null ? director.Inventory.CountOf(it) : 0;
            bool isSel = it == selected;
            bool locked = CropAvailability.LockedBehind(it, director.Run.unlockedIds, director.Db) != null;
            Vector2 mouseInList = Event.current.mousePosition; // inside BeginClip the mouse is already clip-relative

            ImGuiKit.Fill(row, isSel ? new Color(0.32f, 0.28f, 0.45f, 1f) : new Color(0.14f, 0.12f, 0.2f, 1f));
            ItemIconDrawer.Draw(new Rect(row.x + 10 * s, row.y + 12 * s, 26 * s, 26 * s), it);
            ImGuiKit.Label(new Rect(row.x + 48 * s, row.y, row.width - 110 * s, row.height), it.DisplayName, 20f, owned > 0 ? Color.white : ImGuiKit.Muted, TextAnchor.MiddleLeft, isSel);
            ImGuiKit.Label(new Rect(row.xMax - 74 * s, row.y, 66 * s, row.height), locked ? "locked" : owned > 0 ? "x" + owned : "-", 16f,
                locked ? new Color(1f, 0.55f, 0.5f) : owned > 0 ? ImGuiKit.Accent : ImGuiKit.Muted, TextAnchor.MiddleRight, true);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(mouseInList))
            {
                selected = it;
                Event.current.Use();
            }
            y += rowH;
        }
        return y;
    }

    private void DrawDetail(ItemData item, Rect r)
    {
        float s = ImGuiKit.Scale;
        ItemInfo info = ItemInfoBuilder.Build(item, director.BuildItemContext(item));

        ImGuiKit.Fill(r, new Color(0.1f, 0.09f, 0.16f, 1f));
        ImGuiKit.Fill(new Rect(r.x, r.y, 8 * s, r.height), info.color);

        float x = r.x + 28 * s, w = r.width - 48 * s;
        ImGuiKit.Label(new Rect(x, r.y + 12 * s, w, 44 * s), info.title, 36f, Color.white, TextAnchor.MiddleLeft, true);
        ImGuiKit.Label(new Rect(x, r.y + 54 * s, w, 28 * s), info.subtitle, 20f, info.color, TextAnchor.MiddleLeft, true);
        float summaryH = ImGuiKit.TextHeight(info.summary, 18f, w);
        ImGuiKit.Label(new Rect(x, r.y + 88 * s, w, summaryH), info.summary, 18f, ImGuiKit.Muted, TextAnchor.UpperLeft);

        float y = r.y + 96 * s + summaryH;
        float lineH = 30 * s;
        float labelW = w * 0.40f;
        foreach (InfoLine line in info.lines)
        {
            Color c = line.tone == InfoTone.Good ? new Color(0.55f, 0.92f, 0.55f)
                : line.tone == InfoTone.Bad ? new Color(1f, 0.55f, 0.5f)
                : line.tone == InfoTone.Muted ? ImGuiKit.Muted : Color.white;

            // Long values wrap onto more lines instead of running past the panel.
            float h = Mathf.Max(lineH, ImGuiKit.TextHeight(line.value, 18f, w - labelW, true) + 4 * s, ImGuiKit.TextHeight(line.label, 18f, labelW) + 4 * s);
            if (y + h > r.yMax) break;
            ImGuiKit.Label(new Rect(x, y, labelW, h), line.label, 18f, ImGuiKit.Muted, TextAnchor.UpperLeft);
            ImGuiKit.Label(new Rect(x + labelW, y, w - labelW, h), line.value, 18f, c, TextAnchor.UpperLeft, true);
            y += h;
        }
    }
}
