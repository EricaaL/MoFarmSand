using UnityEngine;

/// <summary>
/// The permanent shop, paid for with vault coins: upgrades first, then new crop variants (one per season).
/// Drawn inside the end-of-year summary (so it pops up when a year ends) and the title slot menu.
/// Scrolls with the mouse wheel.
/// </summary>
public static class UpgradeList
{
    private static float scroll;

    public static void Draw(RunDirector director, Rect area)
    {
        float s = ImGuiKit.Scale;
        MetaProgress meta = director.Meta;
        GameDatabase db = director.Db;

        ImGuiKit.Label(new Rect(area.x, area.y, area.width, 36 * s), "Upgrades", 30f, Color.white, TextAnchor.MiddleLeft, true);
        ImGuiKit.Label(new Rect(area.x, area.y + 34 * s, area.width, 26 * s),
            $"Vault: {meta.vaultCoins} / {(db.bank != null ? VaultLogic.Cap(meta, db.bank) : meta.vaultCoins)} coins  -  permanent, for every future year", 18f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);
        if (db.unlocks == null) return;

        Rect view = new Rect(area.x, area.y + 68 * s, area.width, area.height - 68 * s);
        float rowH = 84 * s, headerH = 34 * s;

        int upgrades = 0, crops = 0;
        foreach (UnlockData u in db.unlocks.All)
        {
            if (u == null) continue;
            if (u.UnlocksCrops) crops++; else upgrades++;
        }
        bool hasFarmland = db.farmland != null;
        bool hasStockroom = db.stockroom != null;
        bool hasVault = db.bank != null;
        int tiered = (hasFarmland ? 1 : 0) + (hasStockroom ? 1 : 0) + (hasVault ? 1 : 0);
        float contentH = (upgrades + crops) * rowH + (crops > 0 ? headerH : 0f) + (tiered > 0 ? tiered * rowH + headerH : 0f);
        float maxScroll = Mathf.Max(0f, contentH - view.height);

        Event e = Event.current;
        if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition)) { scroll = Mathf.Clamp(scroll + e.delta.y * 30f, 0f, maxScroll); e.Use(); }
        scroll = Mathf.Clamp(scroll, 0f, maxScroll);

        GUI.BeginClip(view);
        float y = -scroll;
        if (tiered > 0)
        {
            ImGuiKit.Label(new Rect(0f, y, view.width, headerH), "Your farm and the merchant", 20f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);
            y += headerH;
            if (hasFarmland) y = DrawFarmland(director, view.width, y, rowH);
            if (hasStockroom) y = DrawStockroom(director, view.width, y, rowH);
            if (hasVault) y = DrawVault(director, view.width, y, rowH);
        }
        y = DrawGroup(director, view.width, y, rowH, wantCrops: false);
        if (crops > 0)
        {
            ImGuiKit.Label(new Rect(0f, y, view.width, headerH), "New crops - one for every season", 20f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);
            y += headerH;
            DrawGroup(director, view.width, y, rowH, wantCrops: true);
        }
        GUI.EndClip();

        if (maxScroll > 0f && scroll < maxScroll - 1f)
            ImGuiKit.Label(new Rect(view.x, view.yMax - 22 * s, view.width, 22 * s), "scroll for more", 14f, ImGuiKit.Muted, TextAnchor.MiddleCenter);
    }

    /// <summary>The repeatable "Extra Farmland" purchase. Each one you own makes the next more expensive.</summary>
    private static float DrawFarmland(RunDirector director, float width, float y, float rowH)
    {
        float s = ImGuiKit.Scale;
        MetaProgress meta = director.Meta;
        FarmlandConfig cfg = director.Db.farmland;
        int owned = meta.farmlands;
        bool maxed = FarmlandLogic.IsMaxed(owned, cfg);
        int price = FarmlandLogic.PriceFor(owned, cfg);

        Rect row = new Rect(0f, y, width, rowH - 6 * s);
        ImGuiKit.Fill(row, new Color(0.1f, 0.09f, 0.16f, 1f));
        ImGuiKit.Fill(new Rect(row.x + 10 * s, row.y + 10 * s, 6 * s, row.height - 20 * s), new Color(0.55f, 0.4f, 0.22f));

        ImGuiKit.Label(new Rect(row.x + 26 * s, row.y + 4 * s, width - 200 * s, 28 * s),
            cfg.maxFarmlands > 0 ? $"Extra Farmland  ({owned}/{cfg.maxFarmlands})" : $"Extra Farmland  (owned {owned})", 22f, maxed ? ImGuiKit.Accent : Color.white, TextAnchor.MiddleLeft, true);
        string detail = maxed
            ? $"Your farm is as big as it gets: {owned * director.PlotsPerFarmland} extra plots."
            : $"Adds {director.PlotsPerFarmland} more plots to your field, every year. Each farmland costs more than the last.";
        ImGuiKit.Label(new Rect(row.x + 26 * s, row.y + 32 * s, width - 200 * s, row.height - 34 * s), detail, 16f, ImGuiKit.Muted, TextAnchor.UpperLeft);

        string label = maxed ? "Maxed" : $"{price} coins";
        bool canBuy = !maxed && meta.vaultCoins >= price;
        if (ImGuiKit.Button(new Rect(row.xMax - 150 * s, row.y + 16 * s, 138 * s, 44 * s), label, canBuy, 18f))
            director.BuyFarmland();
        return y + rowH;
    }

    /// <summary>The repeatable "Bigger Stockroom": the merchant carries more. Each level costs more than the last.</summary>
    private static float DrawStockroom(RunDirector director, float width, float y, float rowH)
    {
        float s = ImGuiKit.Scale;
        MetaProgress meta = director.Meta;
        StockroomConfig cfg = director.Db.stockroom;
        int level = meta.stockroomLevel;
        bool maxed = StockroomLogic.IsMaxed(level, cfg);
        int price = StockroomLogic.PriceFor(level, cfg);

        Rect row = new Rect(0f, y, width, rowH - 6 * s);
        ImGuiKit.Fill(row, new Color(0.1f, 0.09f, 0.16f, 1f));
        ImGuiKit.Fill(new Rect(row.x + 10 * s, row.y + 10 * s, 6 * s, row.height - 20 * s), new Color(0.95f, 0.75f, 0.25f));

        ImGuiKit.Label(new Rect(row.x + 26 * s, row.y + 4 * s, width - 200 * s, 28 * s),
            cfg.maxLevel > 0 ? $"Bigger Stockroom  (level {level}/{cfg.maxLevel})" : $"Bigger Stockroom  (level {level})", 22f, maxed ? ImGuiKit.Accent : Color.white, TextAnchor.MiddleLeft, true);
        string detail = maxed
            ? "The merchant's shelves are full to the brim."
            : $"Next level: {StockroomLogic.DescribeLevel(level + 1, cfg)}. Each level costs more than the last.";
        ImGuiKit.Label(new Rect(row.x + 26 * s, row.y + 32 * s, width - 200 * s, row.height - 34 * s), detail, 16f, ImGuiKit.Muted, TextAnchor.UpperLeft);

        string label = maxed ? "Maxed" : $"{price} coins";
        bool canBuy = !maxed && meta.vaultCoins >= price;
        if (ImGuiKit.Button(new Rect(row.xMax - 150 * s, row.y + 16 * s, 138 * s, 44 * s), label, canBuy, 18f))
            director.BuyStockroom();
        return y + rowH;
    }

    /// <summary>The repeatable "Bigger Vault": raises how many coins the vault holds. Each level costs more than the last.</summary>
    private static float DrawVault(RunDirector director, float width, float y, float rowH)
    {
        float s = ImGuiKit.Scale;
        MetaProgress meta = director.Meta;
        BankConfig cfg = director.Db.bank;
        int level = meta.vaultLevel;
        int price = VaultLogic.PriceFor(level, cfg);

        Rect row = new Rect(0f, y, width, rowH - 6 * s);
        ImGuiKit.Fill(row, new Color(0.1f, 0.09f, 0.16f, 1f));
        ImGuiKit.Fill(new Rect(row.x + 10 * s, row.y + 10 * s, 6 * s, row.height - 20 * s), new Color(0.45f, 0.7f, 0.95f));

        ImGuiKit.Label(new Rect(row.x + 26 * s, row.y + 4 * s, width - 200 * s, 28 * s), $"Bigger Vault  (level {level})", 22f, Color.white, TextAnchor.MiddleLeft, true);
        string detail = $"Vault holds {VaultLogic.CapFor(level, cfg)} -> {VaultLogic.CapFor(level + 1, cfg)} coins. Each level costs more than the last.";
        ImGuiKit.Label(new Rect(row.x + 26 * s, row.y + 32 * s, width - 200 * s, row.height - 34 * s), detail, 16f, ImGuiKit.Muted, TextAnchor.UpperLeft);

        bool canBuy = meta.vaultCoins >= price;
        if (ImGuiKit.Button(new Rect(row.xMax - 150 * s, row.y + 16 * s, 138 * s, 44 * s), $"{price} coins", canBuy, 18f))
            director.BuyVault();
        return y + rowH;
    }

    private static float DrawGroup(RunDirector director, float width, float y, float rowH, bool wantCrops)
    {
        float s = ImGuiKit.Scale;
        MetaProgress meta = director.Meta;
        foreach (UnlockData u in director.Db.unlocks.All)
        {
            if (u == null || u.UnlocksCrops != wantCrops) continue;

            Rect row = new Rect(0f, y, width, rowH - 6 * s);
            ImGuiKit.Fill(row, new Color(0.1f, 0.09f, 0.16f, 1f));
            bool owned = meta.unlockedIds.Contains(u.Id);

            float textX = row.x + 12 * s;
            if (wantCrops)
            {
                // A swatch in the crop's colour, so the variants are recognisable.
                SeedData seed = u.unlocksSeeds[0];
                ImGuiKit.Fill(new Rect(row.x + 10 * s, row.y + 10 * s, 6 * s, row.height - 20 * s), seed != null ? seed.ItemColor : Color.white);
                textX = row.x + 26 * s;
            }

            ImGuiKit.Label(new Rect(textX, row.y + 4 * s, width - 190 * s, 28 * s), u.displayName, 22f, owned ? ImGuiKit.Accent : Color.white, TextAnchor.MiddleLeft, true);
            ImGuiKit.Label(new Rect(textX, row.y + 32 * s, width - 190 * s, row.height - 34 * s), u.description, 16f, ImGuiKit.Muted, TextAnchor.UpperLeft);

            string label = owned ? "Unlocked" : $"{u.cost} coins";
            bool canBuy = !owned && meta.vaultCoins >= u.cost;
            if (ImGuiKit.Button(new Rect(row.xMax - 150 * s, row.y + 16 * s, 138 * s, 44 * s), label, canBuy, 18f))
                director.BuyUnlock(u);
            y += rowH;
        }
        return y;
    }
}
