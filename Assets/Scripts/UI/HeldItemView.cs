using UnityEngine;

/// <summary>
/// Bottom-right card showing what you are holding: its icon, name and count. For shooters (peas, corn) the count is
/// the loaded rounds out of the magazine plus the spare crops. All wording lives in <see cref="HeldItemText"/>.
/// </summary>
public class HeldItemView
{
    private readonly PlayerInventory inventory;
    private readonly PlayerCombat combat;

    public HeldItemView(PlayerInventory inventory)
    {
        this.inventory = inventory;
        combat = inventory != null ? inventory.GetComponent<PlayerCombat>() : null;
    }

    public void Draw(float screenW, float screenH)
    {
        if (inventory == null) return;
        ItemData item = inventory.ActiveItem;
        CropData crop = item as CropData;
        int loaded = crop != null && combat != null ? combat.LoadedRounds(crop) : 0;
        HeldItemLabel label = HeldItemText.Format(item, inventory.ActiveCount, loaded);
        if (!label.visible) return;

        const float k = 1.45f;   // the card is drawn 45% larger than its base size
        float s = ImGuiKit.Scale * k;
        ItemInfo info = InfoFor(item, inventory.ActiveCount);
        UiTheme theme = ImGuiKit.Theme;
        Texture statIcon;
        string stat = StatFor(item, info, theme, out statIcon);
        float w = 200f * s, h = 144f * s, margin = 16f * s;
        Rect card = new Rect(screenW - w - margin, screenH - h - margin, w, h);

        ImGuiKit.Rounded(card, ImGuiKit.Orange);
        ImGuiKit.Rounded(new Rect(card.x + 5f * s, card.y + 5f * s, card.width - 10f * s, card.height - 10f * s), ImGuiKit.Cream);

        // Icon on the left with the item name under it.
        // The left column (icon over name) fills the card's height; the stat row in the corner is kept small enough to clear it.
        float nameW = 92f * s;
        float iconSize = 76f * s;
        // Seed names take two lines and fill the column; every other item has a one-line name, so its icon and name sit lower, nearer the middle.
        float drop = item is SeedData ? 0f : 12f * s;
        Rect icon = new Rect(card.x + 5f * s + (nameW - iconSize) * 0.5f, card.y + 11f * s + drop, iconSize, iconSize);
        ItemIconDrawer.Draw(icon, item);
        // Every seed is a two-line name ("Corn" over "Seeds"), so they all line up. Fit measures in the same bold face the label is
        // drawn in, and the box holds two full lines, so a long name is never clipped.
        string name = item is SeedData ? TwoLines(item.DisplayName) : item.DisplayName;
        ImGuiKit.Label(new Rect(card.x + 5f * s, card.y + 89f * s + drop, nameW, 46f * s), ImGuiKit.Fit(name, 15f * k, nameW, 2, true), 15f * k, ImGuiKit.Text, TextAnchor.UpperCenter, true);
        // Crops grown out of season are weaker; same cue as the hotbar slot.
        float q = inventory.ActiveQuality;
        if (crop != null && Mathf.Abs(q - 1f) > 0.05f)
            ImGuiKit.Label(new Rect(icon.xMax - 6f * s, icon.y, 34f * s, 16f * s), $"{Mathf.RoundToInt(q * 100f)}%", 11f * k, ImGuiKit.Accent, TextAnchor.UpperLeft, true);

        // The count, big, on the right.
        float alpha = label.outOfAmmo ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : 1f;
        Color color = label.low ? new Color(0.8f, 0.16f, 0.12f) : ImGuiKit.Text;
        color.a = alpha;
        // Sized so "10 / 10" and "Out of ammo" stay on one row inside the box (IMGUI labels word-wrap).
        // A little above the panel's middle (the card is 144 tall), leaving room for the spare-crops line right under it.
        Rect countRect = new Rect(card.x + 90f * s, card.y + 26f * s, card.width - 96f * s, 56f * s);
        // A plain count ("x12") is short and can be big; the shooter's "10 / 10" is the widest thing the box has to hold.
        bool shooterCount = label.main.Contains("/");
        float size = (label.outOfAmmo ? 15f : shooterCount ? 27f : 36f) * k;
        ImGuiKit.Label(countRect, label.main, size, color, TextAnchor.MiddleCenter, true);
        if (label.sub.Length > 0)
        {
            // The spare-crops "+3" sits just under the count, lined up with the count's left edge.
            float countW = ImGuiKit.TextWidth(label.main, size, ImGuiKit.Face.Bold);
            float left = Mathf.Max(countRect.x, countRect.center.x - countW * 0.5f);
            ImGuiKit.Label(new Rect(left, card.y + 70f * s, countRect.xMax - left, 28f * s), label.sub, 12f * k + 2f, ImGuiKit.Muted, TextAnchor.UpperLeft, true);
        }

        // Under the count: seconds to grow for a seed, the bread (fullness) for an attack crop, the Bark Shield time for a support crop.
        if (stat != null)
        {
            // Big, and tucked into the bottom-right corner of the card (the icon hugs the number at the right edge).
            float rowH = 32f * s;
            Rect row = new Rect(card.x + 100f * s, card.yMax - 7f * s - rowH, card.width - 100f * s - 12f * s, rowH);
            ImGuiKit.IconValue(row, statIcon, stat, 17f * k, ImGuiKit.Text, TextAnchor.MiddleRight, true, 0.7f);   // 70% opacity
        }
    }

    /// <summary>"Corn Seeds" becomes "Corn" over "Seeds": the break goes at the last space, so a long first word stays whole.</summary>
    private static string TwoLines(string name)
    {
        int space = name.LastIndexOf(' ');
        return space > 0 ? name.Substring(0, space) + "\n" + name.Substring(space + 1) : name;
    }

    // ------------------------------------------------------------------ the stat row

    private RunDirector director;
    private bool directorSearched;
    private ItemInfo cachedInfo;
    private ItemData cachedItem;
    private int cachedCount;
    private float cachedAt;

    /// <summary>Today's numbers for the held item (same source as the hotbar tooltip), rebuilt when the stack changes or twice a second.</summary>
    private ItemInfo InfoFor(ItemData item, int count)
    {
        if (cachedItem == item && cachedCount == count && Time.unscaledTime - cachedAt < 0.5f) return cachedInfo;
        if (!directorSearched)
        {
            director = Object.FindFirstObjectByType<RunDirector>();
            directorSearched = true;
        }
        cachedInfo = director != null && director.IsRoguelike && director.IsDayRunning && director.Plan != null && director.Run != null
            ? ItemInfoBuilder.Build(item, director.BuildItemContext(item))
            : null;
        cachedItem = item;
        cachedCount = count;
        cachedAt = Time.unscaledTime;
        return cachedInfo;
    }

    /// <summary>The short value for the row ("22s", "30"), with the strength note dropped; null when the item has none.</summary>
    private static string StatFor(ItemData item, ItemInfo info, UiTheme theme, out Texture icon)
    {
        icon = null;
        if (info == null) return null;
        string text;
        if (item is SeedData) { text = info.growTime; icon = ItemDetailCard.GrowIcon(info, theme); }
        else if (item is CropData crop && crop.type == CropType.Support) { text = info.shield; icon = theme != null ? theme.barkShield : null; }
        else if (item is CropData) { text = info.fullness; icon = theme != null ? theme.bread : null; }
        else return null;
        if (string.IsNullOrEmpty(text)) return null;
        int note = text.IndexOf(" (", System.StringComparison.Ordinal);
        return note > 0 ? text.Substring(0, note) : text;
    }
}
