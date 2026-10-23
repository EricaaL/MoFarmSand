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
        float w = 200f * s, h = 104f * s, margin = 16f * s;
        Rect card = new Rect(screenW - w - margin, screenH - h - margin, w, h);

        ImGuiKit.Rounded(card, ImGuiKit.Orange);
        ImGuiKit.Rounded(new Rect(card.x + 5f * s, card.y + 5f * s, card.width - 10f * s, card.height - 10f * s), ImGuiKit.Cream);

        // Icon on the left with the item name under it.
        float iconSize = 44f * s;
        Rect icon = new Rect(card.x + 14f * s, card.y + 12f * s, iconSize, iconSize);
        ItemIconDrawer.Draw(icon, item);
        float nameW = 84f * s;
        ImGuiKit.Label(new Rect(card.x + 6f * s, card.y + 60f * s, nameW, 38f * s), ImGuiKit.Fit(item.DisplayName, 12f * k, nameW, 2), 12f * k, ImGuiKit.Text, TextAnchor.UpperCenter, true);
        // Crops grown out of season are weaker; same cue as the hotbar slot.
        float q = inventory.ActiveQuality;
        if (crop != null && Mathf.Abs(q - 1f) > 0.05f)
            ImGuiKit.Label(new Rect(icon.xMax + 2f * s, icon.y, 34f * s, 16f * s), $"{Mathf.RoundToInt(q * 100f)}%", 11f * k, ImGuiKit.Accent, TextAnchor.UpperLeft, true);

        // The count, big, on the right.
        float alpha = label.outOfAmmo ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : 1f;
        Color color = label.low ? new Color(0.8f, 0.16f, 0.12f) : ImGuiKit.Text;
        color.a = alpha;
        // Sized so "10 / 10" and "Out of ammo" stay on one row inside the box (IMGUI labels word-wrap).
        Rect countRect = new Rect(card.x + 90f * s, card.y + 8f * s, card.width - 96f * s, 56f * s);
        float size = (label.outOfAmmo ? 15f : 26f) * k;
        ImGuiKit.Label(countRect, label.main, size, color, TextAnchor.MiddleCenter, true);
        if (label.sub.Length > 0)
            ImGuiKit.Label(new Rect(countRect.x, card.y + 62f * s, countRect.width, 28f * s), label.sub, 16f * k, ImGuiKit.Muted, TextAnchor.UpperCenter, true);
    }
}
