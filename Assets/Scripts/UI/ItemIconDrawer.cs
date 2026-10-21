using UnityEngine;

/// <summary>Draws an item's placeholder icon (IMGUI). Shared by the hotbar slots and the held-item card so they always match.</summary>
public static class ItemIconDrawer
{
    /// <summary>Crops are a coloured square; seeds are a narrow packet with a pale label band. Fits inside <paramref name="box"/>.</summary>
    public static void Draw(Rect box, ItemData item, float alpha = 1f)
    {
        if (item == null) return;
        Sprite sprite = item.Icon;
        if (sprite != null && sprite.texture != null)
        {
            Rect tr = sprite.textureRect;
            Texture2D tex = sprite.texture;
            Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            float fit = Mathf.Min(box.width / tr.width, box.height / tr.height);
            Vector2 size = new Vector2(tr.width * fit, tr.height * fit);
            Rect dst = new Rect(box.center.x - size.x * 0.5f, box.center.y - size.y * 0.5f, size.x, size.y);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTextureWithTexCoords(dst, tex, uv);
            GUI.color = prev;
            return;
        }

        Color c = item.ItemColor; c.a = alpha;
        if (item is SeedData)
        {
            Rect packet = new Rect(box.center.x - box.width * (10f / 28f), box.y, box.width * (20f / 28f), box.height);
            ImGuiKit.Fill(packet, c);
            ImGuiKit.Fill(new Rect(packet.x, packet.y, packet.width, box.height * (8f / 28f)), new Color(1f, 1f, 1f, 0.55f * alpha));
        }
        else
        {
            ImGuiKit.Fill(box, c);
        }
    }
}
