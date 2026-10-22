using UnityEngine;

/// <summary>
/// The IMGUI hotbar: eight slots, a hover tooltip describing the item, and drag-to-use.
/// Drag an item off the bar and drop it in the world: a seed plants on the plot there, an attack crop is thrown
/// at that spot, a defense crop is eaten. Dropping it back on the bar (or right-click) cancels.
/// </summary>
public class HotbarView
{
    private const float DragThreshold = 8f;

    /// <summary>True while the pointer is on the hotbar or an item is being dragged, so the world ignores the click.</summary>
    public static bool PointerBusy { get; private set; }

    /// <summary>Another clickable HUD control (e.g. the beta "Get Seeds" button) that the world should also ignore clicks on. Set each frame by its owner.</summary>
    public static Rect BlockRect { get; set; }

    private readonly PlayerInventory inventory;
    private readonly PlayerCombat combat;
    private RunDirector director;
    private bool directorSearched;

    private int dragSlot = -1;
    private bool dragging;
    private Vector2 dragStart;

    private string note = string.Empty;
    private float noteUntil;

    private ItemData cachedItem;
    private int cachedCount = -1;
    private float cachedAt;
    private ItemInfo cachedInfo;

    public HotbarView(PlayerInventory inventory)
    {
        this.inventory = inventory;
        combat = inventory != null ? inventory.GetComponent<PlayerCombat>() : null;
    }

    public static void Reset() { PointerBusy = false; BlockRect = Rect.zero; }

    public void Draw(float screenW, float screenH)
    {
        if (inventory == null) return;
        Event e = Event.current;
        float s = ImGuiKit.Scale;
        float size = 72f * s, gap = 6f * s;
        int count = PlayerInventory.SlotCount;
        float total = count * size + (count - 1) * gap;
        float left = screenW * 0.5f - total * 0.5f;
        float top = screenH - size - 16f * s;
        Rect bar = new Rect(left - 6f * s, top - 6f * s, total + 12f * s, size + 12f * s);

        Vector2 mouse = e.mousePosition;
        int hover = -1;
        for (int i = 0; i < count; i++)
            if (SlotRect(i, left, top, size, gap).Contains(mouse)) { hover = i; break; }

        HandleInput(e, hover, bar);
        PointerBusy = dragging || dragSlot >= 0 || bar.Contains(mouse) || BlockRect.Contains(mouse);

        for (int i = 0; i < count; i++) DrawSlot(i, SlotRect(i, left, top, size, gap), s, i == dragSlot && dragging);

        if (!dragging && hover >= 0) DrawTooltip(hover, SlotRect(hover, left, top, size, gap), screenW, s);
        if (dragging) DrawDragGhost(mouse, s, bar);

        if (Time.unscaledTime < noteUntil)
            ImGuiKit.HudLabel(new Rect(screenW * 0.5f - 300f * s, top - 44f * s, 600f * s, 30f * s), note, 20f, TextAnchor.MiddleCenter, true);
    }

    private static Rect SlotRect(int i, float left, float top, float size, float gap) => new Rect(left + i * (size + gap), top, size, size);

    // ------------------------------------------------------------------ input

    private void HandleInput(Event e, int hover, Rect bar)
    {
        switch (e.rawType)
        {
            case EventType.MouseDown:
                if (e.button == 0 && hover >= 0)
                {
                    PlayerInventory.Slot slot = inventory.GetSlot(hover);
                    inventory.SetActive(hover);
                    if (slot.item != null && slot.count > 0)
                    {
                        dragSlot = hover;
                        dragging = false;
                        dragStart = e.mousePosition;
                    }
                    e.Use();
                }
                else if (e.button == 1 && dragSlot >= 0)
                {
                    CancelDrag();
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (dragSlot >= 0 && !dragging && (e.mousePosition - dragStart).magnitude > DragThreshold) dragging = true;
                break;

            case EventType.MouseUp:
                if (e.button == 0 && dragSlot >= 0)
                {
                    bool use = dragging && !bar.Contains(e.mousePosition);
                    Vector2 drop = e.mousePosition;
                    CancelDrag();
                    if (use) UseAt(drop);
                }
                break;
        }

        if (dragSlot >= 0 && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) CancelDrag();
    }

    private void CancelDrag() { dragSlot = -1; dragging = false; }

    private void UseAt(Vector2 guiPoint)
    {
        if (combat == null) return;
        Vector2 screenPoint = new Vector2(guiPoint.x, Screen.height - guiPoint.y); // IMGUI y is top-down, screen y is bottom-up
        if (!combat.TryUseAt(screenPoint, out string message) && message.Length > 0) Say(message);
    }

    private void Say(string text) { note = text; noteUntil = Time.unscaledTime + 2f; }

    // ------------------------------------------------------------------ drawing

    private void DrawSlot(int i, Rect r, float s, bool beingDragged)
    {
        bool active = i == inventory.ActiveIndex;
        ImGuiKit.Fill(r, active ? new Color(1f, 0.9f, 0.3f, 0.9f) : new Color(0f, 0f, 0f, 0.6f));
        ImGuiKit.Fill(new Rect(r.x + 3f * s, r.y + 3f * s, r.width - 6f * s, r.height - 6f * s), new Color(0.12f, 0.12f, 0.12f, 0.9f));

        PlayerInventory.Slot slot = inventory.GetSlot(i);
        if (slot.item != null && slot.count > 0)
        {
            float fade = beingDragged ? 0.35f : 1f;
            // The icon fills the slot; the name is only shown in the hover tooltip.
            float iconSize = r.width - 14f * s;
            ItemIconDrawer.Draw(new Rect(r.center.x - iconSize * 0.5f, r.center.y - iconSize * 0.5f, iconSize, iconSize), slot.item, fade);

            ImGuiKit.HudLabel(new Rect(r.xMax - 40f * s, r.yMax - 17f * s, 36f * s, 15f * s), "x" + slot.count, 12f, TextAnchor.LowerRight, true);

            // Crops grown out of season are weaker: show their strength so you know what you're throwing.
            float q = slot.EffectiveQuality;
            if (slot.item is CropData && Mathf.Abs(q - 1f) > 0.05f)
            {
                ImGuiKit.HudLabel(new Rect(r.xMax - 38f * s, r.y + 1f * s, 36f * s, 16f * s), $"{Mathf.RoundToInt(q * 100f)}%", 11f, TextAnchor.UpperRight, true);
            }
        }
        ImGuiKit.HudLabel(new Rect(r.x + 5f * s, r.y + 1f * s, 18f * s, 16f * s), (i + 1).ToString(), 11f, TextAnchor.UpperLeft, true);
    }

    private void DrawDragGhost(Vector2 mouse, float s, Rect bar)
    {
        PlayerInventory.Slot slot = inventory.GetSlot(dragSlot);
        if (slot.item == null) return;
        bool isSeed = slot.item is SeedData;
        Rect icon = new Rect(mouse.x - 16f * s, mouse.y - 16f * s, isSeed ? 24f * s : 32f * s, 32f * s);
        ImGuiKit.Fill(new Rect(icon.x - 4f * s, icon.y - 4f * s, icon.width + 8f * s, icon.height + 8f * s), new Color(0f, 0f, 0f, 0.5f));
        ItemIconDrawer.Draw(icon, slot.item);

        string hint = bar.Contains(mouse) ? "Drop here to cancel"
            : isSeed ? "Drop on a plot to plant"
            : slot.item is FarmlandPlotItem ? "Drop on open ground to place"
            : slot.item is CropData crop && crop.type == CropType.Projectile ? "Drop to throw it there"
            : slot.item is CropData shooter && shooter.type == CropType.Shooter ? "Drop to shoot there"
            : "Drop to eat";
        ImGuiKit.HudLabel(new Rect(mouse.x + 22f * s, mouse.y - 12f * s, 320f * s, 26f * s), hint, 18f, TextAnchor.MiddleLeft, true);
    }

    private void DrawTooltip(int index, Rect slotRect, float screenW, float s)
    {
        PlayerInventory.Slot slot = inventory.GetSlot(index);
        if (slot.item == null || slot.count <= 0) return;

        ItemInfo info = InfoFor(slot);
        float w = 400f * s, pad = 12f * s;
        float inner = w - pad * 2f;
        float labelW = inner * 0.38f;

        float titleH = 30f * s;
        float subH = ImGuiKit.TextHeight(info.subtitle, 15f, inner, true);
        float summaryH = string.IsNullOrEmpty(info.summary) ? 0f : ImGuiKit.TextHeight(info.summary, 15f, inner);
        float h = pad + titleH + subH + 4f * s + summaryH + 8f * s;

        const int maxLines = 7;
        int shown = Mathf.Min(maxLines, info.lines.Count);
        float[] heights = new float[shown];
        for (int i = 0; i < shown; i++)
        {
            InfoLine line = info.lines[i];
            heights[i] = Mathf.Max(ImGuiKit.TextHeight(line.value, 15f, inner - labelW, true), ImGuiKit.TextHeight(line.label, 15f, labelW)) + 3f * s;
            h += heights[i];
        }
        h += pad;
        if (info.lines.Count > shown) h += 20f * s;

        float x = Mathf.Clamp(slotRect.center.x - w * 0.5f, 8f * s, screenW - w - 8f * s);
        float y = Mathf.Max(8f * s, slotRect.y - h - 14f * s);
        Rect box = new Rect(x, y, w, h);

        ImGuiKit.Fill(box, new Color(0.04f, 0.03f, 0.08f, 0.97f));
        ImGuiKit.Fill(new Rect(box.x, box.y, 5f * s, box.height), info.color);

        float cx = box.x + pad + 4f * s;
        float cy = box.y + pad;
        ImGuiKit.HudLabel(new Rect(cx, cy, inner - 4f * s, titleH), info.title, 22f, TextAnchor.MiddleLeft, true);
        cy += titleH;
        ImGuiKit.HudLabel(new Rect(cx, cy, inner - 4f * s, subH), info.subtitle, 15f, TextAnchor.UpperLeft, true);
        cy += subH + 4f * s;
        if (summaryH > 0f)
        {
            ImGuiKit.HudLabel(new Rect(cx, cy, inner - 4f * s, summaryH), info.summary, 15f, TextAnchor.UpperLeft);
            cy += summaryH + 8f * s;
        }
        for (int i = 0; i < shown; i++)
        {
            InfoLine line = info.lines[i];
            ImGuiKit.HudLabel(new Rect(cx, cy, labelW, heights[i]), line.label, 15f, TextAnchor.UpperLeft);
            ImGuiKit.HudLabel(new Rect(cx + labelW, cy, inner - labelW - 4f * s, heights[i]), line.value, 15f, TextAnchor.UpperLeft, true);
            cy += heights[i];
        }
        if (info.lines.Count > shown)
            ImGuiKit.HudLabel(new Rect(cx, cy, inner, 18f * s), "Tab for the full description", 13f, TextAnchor.UpperLeft);
    }

    // ------------------------------------------------------------------ item info

    private ItemInfo InfoFor(PlayerInventory.Slot slot)
    {
        // Rebuilt when the stack changes or every half second, not on every IMGUI event.
        if (cachedInfo != null && cachedItem == slot.item && cachedCount == slot.count && Time.unscaledTime - cachedAt < 0.5f) return cachedInfo;

        if (!directorSearched)
        {
            director = Object.FindFirstObjectByType<RunDirector>();
            directorSearched = true;
        }

        ItemInfo info;
        if (director != null && director.IsRoguelike && director.IsDayRunning && director.Plan != null && director.Run != null)
            info = ItemInfoBuilder.Build(slot.item, director.BuildItemContext(slot.item));
        else
            info = new ItemInfo { title = slot.item.DisplayName, subtitle = slot.item is SeedData ? "Seed" : slot.item is FarmlandPlotItem ? "Farmland" : "Crop", color = slot.item.ItemColor };

        cachedInfo = info;
        cachedItem = slot.item;
        cachedCount = slot.count;
        cachedAt = Time.unscaledTime;
        return info;
    }
}
