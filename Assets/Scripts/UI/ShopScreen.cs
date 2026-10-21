using UnityEngine;

/// <summary>The merchant: buy seeds and run modifiers on the left, sell harvested crops on the right.</summary>
public class ShopScreen : IRunScreen
{
    private readonly RunDirector director;

    public ShopScreen(RunDirector director) { this.director = director; }

    public void Draw()
    {
        DayPlan plan = director.Plan;
        GameDatabase db = director.Db;
        if (plan == null) return;
        float s = ImGuiKit.Scale;

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { director.CloseShop(); return; }

        Rect panel = ImGuiKit.Modal(1200f, 700f, "The Merchant");
        float colW = (panel.width - 24 * s * 3) / 2f;
        float lx = panel.x + 24 * s;
        float rx = lx + colW + 24 * s;
        float top = panel.y + 90 * s;

        ImGuiKit.Label(new Rect(panel.xMax - 340 * s, panel.y + 24 * s, 316 * s, 40 * s), $"Purse: {director.Wallet.Coins}", 30f, Color.white, TextAnchor.MiddleRight, true);

        // Buy column.
        ImGuiKit.Label(new Rect(lx, top, colW, 34 * s), plan.restocked ? "Fresh stock!" : "Limited stock", 26f, plan.restocked ? new Color(0.5f, 0.9f, 0.5f) : ImGuiKit.Accent, TextAnchor.MiddleLeft, true);
        ImGuiKit.Label(new Rect(lx, top + 32 * s, colW, 24 * s),
            plan.restocked ? "Restocked today, plus charms."
                           : "What's left on the shelf. He restocks on Merchant days (M on the map).",
            15f, ImGuiKit.Muted, TextAnchor.UpperLeft);
        float y = top + 66 * s;
        for (int i = 0; i < plan.offers.Count; i++)
        {
            ShopOffer o = plan.offers[i];
            string title, desc;
            if (o.kind == OfferKind.Seed)
            {
                db.items.TryGet(o.id, out ItemData item);
                title = item != null ? item.DisplayName : o.id;
                PriceTable.PriceRow row = db.prices.RowForSeed(o.id);
                float mult = row != null ? row.SeasonMultFor(plan.date.season) : 1f;
                desc = $"Plants for a harvest.  Season price {(mult > 1.02f ? "▲" : mult < 0.98f ? "▼" : "•")}";
            }
            else
            {
                db.modifiers.TryGet(o.id, out RunModifierData m);
                title = m != null ? m.displayName : o.id;
                desc = m != null ? m.description : "";
            }
            int price = director.OfferPrice(o);
            Rect row2 = new Rect(lx, y, colW, 86 * s);
            ImGuiKit.Fill(row2, new Color(0.1f, 0.09f, 0.16f, 1f));
            ImGuiKit.Label(new Rect(row2.x + 12 * s, row2.y + 6 * s, colW - 190 * s, 30 * s), ImGuiKit.Fit(title, 22f, colW - 190 * s, 1, true), 22f, Color.white, TextAnchor.MiddleLeft, true);
            ImGuiKit.Label(new Rect(row2.x + 12 * s, row2.y + 36 * s, colW - 190 * s, 46 * s), ImGuiKit.Fit(desc, 15f, colW - 190 * s, 3), 15f, ImGuiKit.Muted, TextAnchor.UpperLeft);
            bool can = o.stock > 0 && director.Wallet.Coins >= price;
            if (ImGuiKit.Button(new Rect(row2.xMax - 170 * s, row2.y + 14 * s, 158 * s, 58 * s), o.stock > 0 ? $"{price} coins" : "Sold out", can, 20f))
                director.Buy(i);
            y += 94 * s;
        }
        bool anyLeft = plan.offers.Exists(o => o.stock > 0);
        if (!anyLeft) ImGuiKit.Label(new Rect(lx, y, colW, 60 * s), "Sold out! Come back on a Merchant day for more.", 20f, ImGuiKit.Muted, TextAnchor.UpperLeft);

        // Sell column.
        ImGuiKit.Label(new Rect(rx, top, colW, 34 * s), "I'll buy your crops", 26f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);
        y = top + 44 * s;
        bool any = false;
        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            PlayerInventory.Slot slot = director.Inventory.GetSlot(i);
            CropData crop = slot.item as CropData;
            if (crop == null || slot.count <= 0) continue;
            any = true;

            int price = director.SellPriceOf(crop);
            float demand = director.Run.market.GetDemand(crop.Id);
            Rect row = new Rect(rx, y, colW, 86 * s);
            ImGuiKit.Fill(row, new Color(0.1f, 0.09f, 0.16f, 1f));
            ImGuiKit.Fill(new Rect(row.x + 12 * s, row.y + 14 * s, 24 * s, 24 * s), crop.color);
            ImGuiKit.Label(new Rect(row.x + 46 * s, row.y + 6 * s, colW - 300 * s, 30 * s), ImGuiKit.Fit($"{crop.displayName} x{slot.count}", 22f, colW - 300 * s, 1, true), 22f, Color.white, TextAnchor.MiddleLeft, true);
            ImGuiKit.Label(new Rect(row.x + 12 * s, row.y + 40 * s, 150 * s, 24 * s), $"{price} coins each", 16f, ImGuiKit.Muted);

            // Demand bar: shrinks as you flood the market.
            Rect bar = new Rect(row.x + 12 * s, row.y + 66 * s, colW - 300 * s, 8 * s);
            ImGuiKit.Fill(bar, new Color(0f, 0f, 0f, 0.5f));
            float f = Mathf.Clamp01((demand - db.economy.minDemand) / Mathf.Max(0.01f, db.economy.maxDemand - db.economy.minDemand));
            ImGuiKit.Fill(new Rect(bar.x, bar.y, bar.width * f, bar.height), Color.Lerp(new Color(0.9f, 0.3f, 0.25f), new Color(0.35f, 0.85f, 0.4f), f));

            if (ImGuiKit.Button(new Rect(row.xMax - 270 * s, row.y + 18 * s, 120 * s, 50 * s), "Sell 1", true, 18f)) director.Sell(crop, 1);
            if (ImGuiKit.Button(new Rect(row.xMax - 140 * s, row.y + 18 * s, 128 * s, 50 * s), "Sell all", true, 18f)) director.Sell(crop, slot.count);
            y += 94 * s;
        }
        if (!any) ImGuiKit.Label(new Rect(rx, y, colW, 60 * s), "Harvest some crops first, then come back to sell.", 20f, ImGuiKit.Muted, TextAnchor.UpperLeft);

        if (ImGuiKit.Button(new Rect(panel.x + 24 * s, panel.yMax - 76 * s, panel.width - 48 * s, 52 * s), "Close (Esc)", true, 22f))
            director.CloseShop();
    }
}
