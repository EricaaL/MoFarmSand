using UnityEngine;

/// <summary>The merchant: a slate panel with the selected item's card on the left and a BUY / SELL grid of cards on the right.</summary>
public class ShopScreen : IRunScreen
{
    private readonly RunDirector director;
    private bool selling;
    private int selectedOffer;
    private CropData selectedCrop;
    private float scroll;

    public ShopScreen(RunDirector director) { this.director = director; }

    public void OnOpen()
    {
        selling = false;
        selectedOffer = 0;
        selectedCrop = null;
        scroll = 0f;
    }

    public void Draw()
    {
        DayPlan plan = director.Plan;
        if (plan == null) return;
        float s = ImGuiKit.Scale;
        UiTheme theme = ImGuiKit.Theme;

        // The world behind, then the slate panel (the art spans the whole 1920x1080 canvas).
        ImGuiKit.Backdrop(0f);
        // The slate panel is the 1920x1080 canvas; slate bars fill any window area outside it.
        Vector2 canvas = ImGuiKit.CanvasSize;
        Rect win = ImGuiKit.ScreenLocal;
        ImGuiKit.Fill(new Rect(win.x, win.y, -win.x, win.height), ImGuiKit.Slate);                         // left
        ImGuiKit.Fill(new Rect(canvas.x, win.y, win.xMax - canvas.x, win.height), ImGuiKit.Slate);          // right
        ImGuiKit.Fill(new Rect(0f, win.y, canvas.x, -win.y), ImGuiKit.Slate);                               // top
        ImGuiKit.Fill(new Rect(0f, canvas.y, canvas.x, win.yMax - canvas.y), ImGuiKit.Slate);               // bottom
        if (theme != null && theme.merchantPanel != null) GUI.DrawTexture(new Rect(0f, 0f, canvas.x, canvas.y), theme.merchantPanel, ScaleMode.StretchToFill, true);

        // Right side first: it decides what is selected, and the left card shows it.
        DrawRight(plan, s, theme);
        DrawLeft(plan, s, theme);
    }

    // ------------------------------------------------------------------ right: toggle + grid

    private void DrawRight(DayPlan plan, float s, UiTheme theme)
    {
        Color light = new Color(0.93f, 0.92f, 0.91f);
        Color dim = new Color(0.67f, 0.67f, 0.68f);

        // Big heading for the open side; the other side is a small grey toggle in the corner.
        ImGuiKit.Heading(new Rect(1000f * s, 70f * s, 800f * s, 130f * s), selling ? "SELL" : "BUY", 112f, light, TextAnchor.MiddleCenter);
        Rect toggle = new Rect(1700f * s, 70f * s, 180f * s, 76f * s);
        bool hover = toggle.Contains(Event.current.mousePosition);
        ImGuiKit.Heading(toggle, selling ? "BUY" : "SELL", 56f, hover ? Color.white : dim, TextAnchor.MiddleRight);
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover)
        {
            ImGuiKit.ConsumeSmallClick();
            selling = !selling;
            scroll = 0f;
        }
        ImGuiKit.DashedRule(942f * s, 187f * s, 916f * s, new Color(0.85f, 0.85f, 0.88f), 3f);

        // The restock note.
        if (!selling)
            ImGuiKit.Italic(new Rect(962f * s, 84f * s, 280f * s, 90f * s), plan.restocked ? "Fresh stock" : "Limited stock", 22f, dim, TextAnchor.UpperLeft);

        // Cards: BUY lists the offers, SELL lists the crops you carry.
        const int cols = 3;
        float cardW = 268f * s, cardH = 373f * s, gapX = 31f * s, gapY = 26f * s;
        float x0 = 966f * s, y0 = 216f * s;
        Rect view = new Rect(x0 - 10f * s, y0 - 6f * s, cols * cardW + (cols - 1) * gapX + 20f * s, 988f * s - y0 + 12f * s);

        int count = selling ? SellCrops().Count : plan.offers.Count;
        int slots = Mathf.Max(6, Mathf.CeilToInt(count / (float)cols) * cols);
        int rows = slots / cols;
        float contentH = rows * cardH + (rows - 1) * gapY;
        float maxScroll = Mathf.Max(0f, contentH - (988f * s - y0));
        if (maxScroll < 4f * s) maxScroll = 0f;
        Event e = Event.current;
        if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition)) { scroll = Mathf.Clamp(scroll + e.delta.y * 30f, 0f, maxScroll); e.Use(); }
        scroll = Mathf.Clamp(scroll, 0f, maxScroll);

        var crops = SellCrops();
        if (selling && (selectedCrop == null || !crops.Contains(selectedCrop))) selectedCrop = crops.Count > 0 ? crops[0] : null;
        if (!selling) selectedOffer = plan.offers.Count == 0 ? -1 : Mathf.Clamp(selectedOffer, 0, plan.offers.Count - 1);

        GUI.BeginClip(view);
        Vector2 mouse = e.mousePosition;
        for (int i = 0; i < slots; i++)
        {
            Rect card = new Rect(10f * s + (i % cols) * (cardW + gapX), 6f * s - scroll + (i / cols) * (cardH + gapY), cardW, cardH);
            if (card.yMax < 0f || card.y > view.height) continue;

            bool filled = i < count;
            bool sel = filled && (selling ? crops[i] == selectedCrop : i == selectedOffer);
            if (sel) ImGuiKit.Rounded(new Rect(card.x - 6f * s, card.y - 6f * s, card.width + 12f * s, card.height + 12f * s), ImGuiKit.Orange);
            ImGuiKit.Rounded(card, dim);
            if (!filled) continue;

            if (selling) DrawSellCard(card, crops[i], s, theme);
            else DrawOfferCard(card, plan.offers[i], s, theme);

            if (e.type == EventType.MouseDown && e.button == 0 && card.Contains(mouse))
            {
                if (selling) selectedCrop = crops[i]; else selectedOffer = i;
                ImGuiKit.ConsumeSmallClick();
            }
        }
        GUI.EndClip();

        if (maxScroll > 0f)
        {
            Rect track = new Rect(1850f * s, y0, 14f * s, 988f * s - y0);
            float knobH = Mathf.Max(60f * s, track.height * view.height / contentH);
            ImGuiKit.Rounded(new Rect(track.x, track.y + (track.height - knobH) * (scroll / maxScroll), track.width, knobH), dim);
        }
    }

    private System.Collections.Generic.List<CropData> SellCrops()
    {
        var list = new System.Collections.Generic.List<CropData>();
        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            PlayerInventory.Slot slot = director.Inventory.GetSlot(i);
            if (slot.item is CropData crop && slot.count > 0 && !list.Contains(crop)) list.Add(crop);
        }
        return list;
    }

    private void DrawOfferCard(Rect card, ShopOffer o, float s, UiTheme theme)
    {
        bool soldOut = o.stock <= 0;
        Rect art = new Rect(card.x + 22f * s, card.y + 14f * s, card.width - 44f * s, 296f * s);
        if (o.kind == OfferKind.Seed)
        {
            director.Db.items.TryGet(o.id, out ItemData item);
            ImGuiKit.Rounded(art, ImGuiKit.PanelLight);
            ItemIconDrawer.Draw(new Rect(art.x + 6f * s, art.y + 6f * s, art.width - 12f * s, art.height - 12f * s), item, soldOut ? 0.35f : 1f);
        }
        else
        {
            director.Db.modifiers.TryGet(o.id, out RunModifierData m);
            ImGuiKit.Rounded(art, new Color(0.78f, 0.62f, 0.36f));
            ImGuiKit.Label(art, "Charm", 28f, ImGuiKit.Text, TextAnchor.UpperCenter, true);
            ImGuiKit.Label(new Rect(art.x + 10f * s, art.y + 50f * s, art.width - 20f * s, art.height - 60f * s), m != null ? m.displayName : o.id, 34f, ImGuiKit.Text, TextAnchor.MiddleCenter, true);
        }
        PricePill(card, soldOut ? "Sold out" : director.OfferPrice(o).ToString(), !soldOut, s, theme);
    }

    private void DrawSellCard(Rect card, CropData crop, float s, UiTheme theme)
    {
        Rect art = new Rect(card.x + 22f * s, card.y + 14f * s, card.width - 44f * s, 296f * s);
        ImGuiKit.Rounded(art, ImGuiKit.PanelLight);
        ItemIconDrawer.Draw(new Rect(art.x + 10f * s, art.y + 10f * s, art.width - 20f * s, art.height - 20f * s), crop);
        PricePill(card, director.SellPriceOf(crop).ToString(), true, s, theme);
    }

    private static void PricePill(Rect card, string text, bool coin, float s, UiTheme theme)
    {
        Rect pill = new Rect(card.center.x - 82f * s, card.yMax - 56f * s, 164f * s, 46f * s);
        ImGuiKit.Rounded(pill, ImGuiKit.PanelLight);
        if (coin)
        {
            ImGuiKit.Icon(new Rect(pill.x + 14f * s, pill.y + 5f * s, 36f * s, 36f * s), theme != null ? theme.coin : null);
            ImGuiKit.Label(new Rect(pill.x + 54f * s, pill.y, pill.width - 56f * s, pill.height), text, 30f, ImGuiKit.Text, TextAnchor.MiddleLeft, false, false);
        }
        else ImGuiKit.Label(pill, text, 26f, ImGuiKit.Muted, TextAnchor.MiddleCenter);
    }

    // ------------------------------------------------------------------ left: the selected card

    private void DrawLeft(DayPlan plan, float s, UiTheme theme)
    {
        Rect card = new Rect(34f * s, 70f * s, 745f * s, 968f * s);
        ImGuiKit.Rounded(card, ImGuiKit.PanelLight);
        float x = card.x + 36f * s, w = card.width - 72f * s;

        // Purse, top right of the card.
        string purse = director.Wallet.Coins.ToString();
        float purseW = ImGuiKit.TextWidth(purse, 30f) + 4f * s;
        float purseRowW = 46f * s + 10f * s + purseW + 24f * s;   // icon + gap + number + slack
        Rect purseRow = new Rect(card.xMax - 24f * s - purseRowW, card.y + 22f * s, purseRowW, 46f * s);
        ImGuiKit.IconValue(purseRow, theme != null ? theme.coin : null, purse, 30f, ImGuiKit.Muted);

        string title = null, sub = null, desc = null;
        int price = -1;
        string grow = null, fullness = null;
        Texture growIcon = theme != null ? theme.growIcon : null;
        string action = null;
        bool canAct = false;
        System.Action act = null, actAll = null;
        float demand01 = -1f;

        if (!selling)
        {
            if (selectedOffer >= 0 && selectedOffer < plan.offers.Count)
            {
                ShopOffer o = plan.offers[selectedOffer];
                int p = director.OfferPrice(o);
                if (o.kind == OfferKind.Seed && director.Db.items.TryGet(o.id, out ItemData item))
                {
                    ItemInfo info = ItemInfoBuilder.Build(item, director.BuildItemContext(item));
                    title = info.title; desc = info.summary; grow = info.growTime; fullness = info.fullness; growIcon = ItemDetailCard.GrowIcon(info, theme);
                    PriceTable.PriceRow row = director.Db.prices.RowForSeed(o.id);
                    float mult = row != null ? row.SeasonMultFor(plan.date.season) : 1f;
                    if (mult > 1.02f) desc += "  (Season price is up today.)"; else if (mult < 0.98f) desc += "  (Season price is down today.)";
                }
                else if (director.Db.modifiers.TryGet(o.id, out RunModifierData m)) { title = m.displayName; desc = m.description; }
                else title = o.id;
                sub = o.stock > 0 ? $"{o.stock} in stock" : "sold out";
                price = p;
                action = "BUY";
                canAct = o.stock > 0 && director.Wallet.Coins >= p;
                int index = selectedOffer;
                act = () => director.Buy(index);
            }
            else
            {
                title = "Sold out!";
                desc = "Come back on a Merchant day for more.";
            }
        }
        else if (selectedCrop != null)
        {
            CropData crop = selectedCrop;
            ItemInfo info = ItemInfoBuilder.Build(crop, director.BuildItemContext(crop));
            int owned = director.Inventory.CountOf(crop);
            title = crop.displayName; sub = $"owned {owned}"; desc = info.summary; fullness = info.fullness;
            price = director.SellPriceOf(crop);
            action = "SELL";
            canAct = owned > 0;
            act = () => director.Sell(crop, 1);
            actAll = () => director.Sell(crop, director.Inventory.CountOf(crop));
            float demand = director.Run.market.GetDemand(crop.Id);
            EconomyConfig eco = director.Db.economy;
            demand01 = Mathf.Clamp01((demand - eco.minDemand) / Mathf.Max(0.01f, eco.maxDemand - eco.minDemand));
        }
        else
        {
            title = "Nothing to sell";
            desc = "Harvest some crops first, then come back to sell.";
        }

        ImGuiKit.Heading(new Rect(x, card.y + 24f * s, w - 200f * s, 110f * s), title, 76f, ImGuiKit.Text);
        if (sub != null) ImGuiKit.Italic(new Rect(x, card.y + 128f * s, w, 40f * s), sub, 30f, ImGuiKit.Muted);
        ImGuiKit.DashedRule(x, card.y + 168f * s, 480f * s);

        float y = card.y + 190f * s;
        if (price >= 0)
        {
            ImGuiKit.IconValue(new Rect(x, y, 320f * s, 64f * s), theme != null ? theme.coin : null, price.ToString(), 44f, ImGuiKit.Text);
            y += 72f * s;
        }
        if (!string.IsNullOrEmpty(desc))
        {
            float h = ImGuiKit.TextHeight(desc, 30f, w);
            ImGuiKit.Label(new Rect(x, y, w, h + 6f * s), desc, 30f, ImGuiKit.Text, TextAnchor.UpperLeft);
            y += h + 24f * s;
        }
        if (!string.IsNullOrEmpty(grow))
        {
            ImGuiKit.IconValue(new Rect(x, y, w, 82f * s), growIcon, grow, 42f, ImGuiKit.Text);
            y += 92f * s;
        }
        if (!string.IsNullOrEmpty(fullness))
        {
            ImGuiKit.IconValue(new Rect(x, y, w, 82f * s), theme != null ? theme.bread : null, fullness, 42f, ImGuiKit.Text);
            y += 92f * s;
        }
        if (demand01 >= 0f)
        {
            // Demand drops as you flood the market.
            ImGuiKit.Label(new Rect(x, y, w, 30f * s), "Market demand", 22f, ImGuiKit.Muted);
            Rect bar = new Rect(x, y + 32f * s, 420f * s, 16f * s);
            ImGuiKit.Rounded(bar, new Color(0.75f, 0.74f, 0.72f));
            ImGuiKit.Rounded(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * demand01), bar.height), Color.Lerp(new Color(0.85f, 0.3f, 0.25f), new Color(0.35f, 0.75f, 0.4f), demand01));
        }

        // Action button(s) at the bottom; Esc or Close leaves.
        float by = card.yMax - 160f * s;
        if (action != null)
        {
            if (actAll != null)
            {
                if (ImGuiKit.Pill(new Rect(card.center.x - 330f * s, by, 300f * s, 82f * s), action, ImGuiKit.PillKind.Primary, canAct, 50f)) act?.Invoke();
                if (ImGuiKit.Pill(new Rect(card.center.x + 10f * s, by, 300f * s, 82f * s), "SELL ALL", ImGuiKit.PillKind.Cream, canAct, 38f)) actAll();
            }
            else if (ImGuiKit.Pill(new Rect(card.center.x - 130f * s, by, 260f * s, 82f * s), action, ImGuiKit.PillKind.Primary, canAct, 50f)) act?.Invoke();
        }
        if (ImGuiKit.BackButton(new Rect(24f * s, 6f * s, 58f * s, 58f * s))) director.CloseShop();
    }
}
