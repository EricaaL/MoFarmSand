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

    /// <summary>A second one (the tip card's close button).</summary>
    public static Rect BlockRect2 { get; set; }
    /// <summary>True while the hover preview is on screen (the interact prompt hides so it doesn't sit on top of it).</summary>
    public static bool TooltipOpen { get; private set; }

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

    public static void Reset() { PointerBusy = false; TooltipOpen = false; BlockRect = Rect.zero; BlockRect2 = Rect.zero; }

    public void Draw(float screenW, float screenH)
    {
        if (inventory == null) return;
        Event e = Event.current;
        float s = ImGuiKit.Scale;
        float size = 88f * s, gap = 12f * s;
        int count = PlayerInventory.SlotCount;
        float total = count * size + (count - 1) * gap;
        float left = screenW * 0.5f - total * 0.5f;
        float top = screenH - size - 22f * s;
        Rect bar = new Rect(left - 14f * s, top - 14f * s, total + 28f * s, size + 28f * s);
        ImGuiKit.Rounded(bar, ImGuiKit.Brown);

        Vector2 mouse = e.mousePosition;
        int hover = -1;
        for (int i = 0; i < count; i++)
            if (SlotRect(i, left, top, size, gap).Contains(mouse)) { hover = i; break; }

        HandleInput(e, hover, bar);
        PointerBusy = dragging || dragSlot >= 0 || bar.Contains(mouse) || BlockRect.Contains(mouse) || BlockRect2.Contains(mouse);

        for (int i = 0; i < count; i++) DrawSlot(i, SlotRect(i, left, top, size, gap), s, i == dragSlot && dragging);

        TooltipOpen = !dragging && hover >= 0 && inventory.GetSlot(hover).item != null && inventory.GetSlot(hover).count > 0;
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
        // Cream slot; the active one sits in an orange ring.
        if (active) ImGuiKit.Rounded(new Rect(r.x - 6f * s, r.y - 6f * s, r.width + 12f * s, r.height + 12f * s), ImGuiKit.Orange);
        ImGuiKit.Rounded(r, ImGuiKit.Cream);

        PlayerInventory.Slot slot = inventory.GetSlot(i);
        if (slot.item != null && slot.count > 0)
        {
            float fade = beingDragged ? 0.35f : 1f;
            // The icon fills the slot; the name is only shown in the hover tooltip.
            float iconSize = r.width - 18f * s;
            ItemIconDrawer.Draw(new Rect(r.center.x - iconSize * 0.5f, r.center.y - iconSize * 0.5f, iconSize, iconSize), slot.item, fade);

            ImGuiKit.Label(new Rect(r.xMax - 80f * s, r.yMax - 36f * s, 74f * s, 32f * s), "x" + slot.count, 20f, ImGuiKit.Text, TextAnchor.LowerRight, false, false);

            // Crops grown out of season are weaker: show their strength so you know what you're throwing.
            float q = slot.EffectiveQuality;
            if (slot.item is CropData && Mathf.Abs(q - 1f) > 0.05f)
            {
                ImGuiKit.Label(new Rect(r.xMax - 80f * s, r.y + 4f * s, 74f * s, 26f * s), $"{Mathf.RoundToInt(q * 100f)}%", 15f, ImGuiKit.Accent, TextAnchor.UpperRight, true, false);
            }
        }
        ImGuiKit.Label(new Rect(r.x + 8f * s, r.y + 4f * s, 40f * s, 32f * s), (i + 1).ToString(), 20f, ImGuiKit.Text, TextAnchor.UpperLeft, false, false);
    }

    private void DrawDragGhost(Vector2 mouse, float s, Rect bar)
    {
        PlayerInventory.Slot slot = inventory.GetSlot(dragSlot);
        if (slot.item == null) return;
        bool isSeed = slot.item is SeedData;
        Rect icon = new Rect(mouse.x - 16f * s, mouse.y - 16f * s, isSeed ? 24f * s : 32f * s, 32f * s);
        ImGuiKit.Rounded(new Rect(icon.x - 6f * s, icon.y - 6f * s, icon.width + 12f * s, icon.height + 12f * s), new Color(0.996f, 0.843f, 0.647f, 0.85f));
        ItemIconDrawer.Draw(icon, slot.item);

        string hint = bar.Contains(mouse) ? "Drop here to cancel"
            : isSeed ? "Drop on a plot to plant"
            : slot.item is FarmlandPlotItem ? "Drop on open ground to place"
            : slot.item is CropData crop && crop.type == CropType.Projectile ? "Drop to throw it there"
            : slot.item is CropData shooter && shooter.type == CropType.Shooter ? "Drop to shoot there"
            : "Drop to eat";
        ImGuiKit.HudLabel(new Rect(mouse.x + 22f * s, mouse.y - 12f * s, 320f * s, 26f * s), hint, 18f, TextAnchor.MiddleLeft, true);
    }

    /// <summary>Hover preview: the same description block as the inventory, on a small orange panel above the slot.</summary>
    private void DrawTooltip(int index, Rect slotRect, float screenW, float s)
    {
        PlayerInventory.Slot slot = inventory.GetSlot(index);
        if (slot.item == null || slot.count <= 0) return;

        ItemInfo info = InfoFor(slot);
        const float k = 0.6f;   // the card is drawn at this fraction of its inventory size
        float pad = 22f * s;
        float w = 430f * s, inner = w - pad * 2f;
        float contentH = ItemDetailCard.Measure(info, inner, k, false);
        float h = contentH + pad * 2f - 8f * s;

        float x = Mathf.Clamp(slotRect.center.x - w * 0.5f, 10f * s, screenW - w - 10f * s);
        float y = Mathf.Max(10f * s, slotRect.y - h - 24f * s);
        Rect box = new Rect(x, y, w, h);

        ImGuiKit.Rounded(box, ImGuiKit.Orange);
        ItemDetailCard.Draw(new Rect(box.x + pad, box.y + pad * 0.7f, inner, contentH), info, inventory.CountOf(slot.item), k, false);
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
