using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The permanent shop, paid for with vault coins: a scrollable list of upgrades on the left, the selected one on the right
/// with a Purchase button. Drawn on the title screen's slot page and inside the end-of-year summary.
/// </summary>
public static class UpgradeList
{
    private struct Entry
    {
        public string name, detail;
        public int price;
        public bool done, canBuy;
        public Action buy;
        public string tag;   // short note on the list row, e.g. "owned"
    }

    private static float scroll;
    private static string selectedName;

    /// <summary>Draws the whole orange panel (frame, list, details) filling <paramref name="panel"/>.</summary>
    public static void Draw(RunDirector director, Rect panel)
    {
        float s = ImGuiKit.Scale;
        MetaProgress meta = director.Meta;

        ImGuiKit.TabRight(panel, "Upgrades");
        ImGuiKit.Rounded(panel, ImGuiKit.Orange);

        List<Entry> entries = Build(director);
        if (entries.Count == 0)
        {
            ImGuiKit.Label(panel, "Nothing to buy yet.", 30f, ImGuiKit.Text, TextAnchor.MiddleCenter);
            return;
        }
        int sel = entries.FindIndex(e => e.name == selectedName);
        if (sel < 0) { sel = 0; selectedName = entries[0].name; }

        // Left: the list on a cream card.
        float pad = 22f * s;
        Rect list = new Rect(panel.x + pad, panel.y + pad, Mathf.Min(panel.width * 0.42f, 625f * s), panel.height - pad * 2f);
        ImGuiKit.Rounded(list, ImGuiKit.Cream);

        float rowH = 76f * s, rowStep = 90f * s, inset = 22f * s;
        float contentH = entries.Count * rowStep + inset;
        float maxScroll = Mathf.Max(0f, contentH - list.height);
        Event e = Event.current;
        if (e.type == EventType.ScrollWheel && list.Contains(e.mousePosition)) { scroll = Mathf.Clamp(scroll + e.delta.y * 30f, 0f, maxScroll); e.Use(); }
        scroll = Mathf.Clamp(scroll, 0f, maxScroll);

        bool scrolls = maxScroll > 0f;
        float rowW = list.width - inset * 2f - (scrolls ? 26f * s : 0f);
        GUI.BeginClip(list);
        float y = inset - scroll;
        for (int i = 0; i < entries.Count; i++)
        {
            Rect row = new Rect(inset, y, rowW, rowH);
            bool isSel = i == sel;
            if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
            {
                selectedName = entries[i].name;
                e.Use();
            }
            ImGuiKit.Rounded(row, isSel ? ImGuiKit.Orange : ImGuiKit.PanelLight);
            ImGuiKit.Label(new Rect(row.x + 20f * s, row.y, row.width - 40f * s, row.height), ImGuiKit.Fit(entries[i].name, 30f, row.width - 160f * s, 1), 30f, ImGuiKit.Text, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(entries[i].tag))
                ImGuiKit.Italic(new Rect(row.xMax - 150f * s, row.y, 130f * s, row.height), entries[i].tag, 22f, ImGuiKit.Muted, TextAnchor.MiddleRight);
            y += rowStep;
        }
        GUI.EndClip();
        if (scrolls)
        {
            Rect track = new Rect(list.xMax - 30f * s, list.y + inset, 12f * s, list.height - inset * 2f);
            float knobH = Mathf.Max(40f * s, track.height * list.height / contentH);
            ImGuiKit.Rounded(new Rect(track.x, track.y + (track.height - knobH) * (scroll / maxScroll), track.width, knobH), ImGuiKit.Orange);
        }

        // Right: the selected upgrade.
        Entry cur = entries[sel];
        float rx = list.xMax + 42f * s, rw = panel.xMax - rx - 44f * s;
        ImGuiKit.Heading(new Rect(rx, panel.y + 34f * s, rw - 170f * s, 100f * s), cur.name, 66f, ImGuiKit.Text);
        UiTheme theme = ImGuiKit.Theme;
        ImGuiKit.IconValue(new Rect(panel.xMax - 220f * s, panel.y + 40f * s, 190f * s, 64f * s), theme != null ? theme.piggyBank : null, meta.vaultCoins.ToString(), 34f, ImGuiKit.Text, TextAnchor.MiddleRight);
        ImGuiKit.DashedRule(rx, panel.y + 142f * s, rw);

        ImGuiKit.IconValue(new Rect(rx, panel.y + 160f * s, 330f * s, 70f * s), theme != null ? theme.coin : null, cur.done ? "Done" : $"{cur.price} Coins", 36f, ImGuiKit.Text);
        float descH = ImGuiKit.TextHeight(cur.detail, 30f, rw);
        ImGuiKit.Label(new Rect(rx, panel.y + 244f * s, rw, descH + 6f * s), cur.detail, 30f, ImGuiKit.Text, TextAnchor.UpperLeft);

        string buyLabel = cur.done ? "Owned" : "Purchase";
        if (ImGuiKit.Pill(new Rect(panel.xMax - 44f * s - 390f * s, panel.yMax - 22f * s - 90f * s, 390f * s, 76f * s), buyLabel, ImGuiKit.PillKind.Cream, cur.canBuy, 34f))
            cur.buy?.Invoke();
        if (!cur.done && !cur.canBuy)
            ImGuiKit.Italic(new Rect(rx, panel.yMax - 22f * s - 90f * s, rw - 420f * s, 76f * s), "Not enough in the vault", 26f, ImGuiKit.Muted, TextAnchor.MiddleLeft);
    }

    private static List<Entry> Build(RunDirector director)
    {
        MetaProgress meta = director.Meta;
        GameDatabase db = director.Db;
        var list = new List<Entry>();

        if (db.farmland != null)
        {
            FarmlandConfig cfg = db.farmland;
            int owned = meta.farmlands;
            bool maxed = FarmlandLogic.IsMaxed(owned, cfg);
            int price = FarmlandLogic.PriceFor(owned, cfg);
            list.Add(new Entry
            {
                name = "Extra Farmland",
                tag = owned > 0 ? (cfg.maxFarmlands > 0 ? $"{owned}/{cfg.maxFarmlands}" : $"x{owned}") : null,
                detail = maxed
                    ? $"Your farm is as big as it gets: {owned * director.PlotsPerFarmland} extra plots."
                    : $"Gives you {director.PlotsPerFarmland} plots in your hotbar to place anywhere on open ground. Each farmland costs more than the last.",
                price = price, done = maxed, canBuy = !maxed && meta.vaultCoins >= price,
                buy = () => director.BuyFarmland(),
            });
        }

        if (db.stockroom != null)
        {
            StockroomConfig cfg = db.stockroom;
            int level = meta.stockroomLevel;
            bool maxed = StockroomLogic.IsMaxed(level, cfg);
            int price = StockroomLogic.PriceFor(level, cfg);
            list.Add(new Entry
            {
                name = "Bigger Stockroom",
                tag = level > 0 ? $"lv {level}" : null,
                detail = maxed ? "The merchant's shelves are full to the brim."
                    : $"Next level: {StockroomLogic.DescribeLevel(level + 1, cfg)}. Each level costs more than the last.",
                price = price, done = maxed, canBuy = !maxed && meta.vaultCoins >= price,
                buy = () => director.BuyStockroom(),
            });
        }

        if (db.bank != null)
        {
            BankConfig cfg = db.bank;
            int level = meta.vaultLevel;
            int price = VaultLogic.PriceFor(level, cfg);
            list.Add(new Entry
            {
                name = "Bigger Vault",
                tag = level > 0 ? $"lv {level}" : null,
                detail = $"The vault holds {VaultLogic.CapFor(level, cfg)} -> {VaultLogic.CapFor(level + 1, cfg)} coins (you have {meta.vaultCoins}). Each level costs more than the last.",
                price = price, canBuy = meta.vaultCoins >= price,
                buy = () => director.BuyVault(),
            });
        }

        if (db.unlocks != null)
        {
            foreach (bool crops in new[] { false, true })
                foreach (UnlockData u in db.unlocks.All)
                {
                    if (u == null || u.UnlocksCrops != crops) continue;
                    UnlockData unlock = u;
                    bool owned = meta.unlockedIds.Contains(u.Id);
                    list.Add(new Entry
                    {
                        name = u.displayName,
                        tag = owned ? "owned" : null,
                        detail = u.description,
                        price = u.cost, done = owned, canBuy = !owned && meta.vaultCoins >= u.cost,
                        buy = () => director.BuyUnlock(unlock),
                    });
                }
        }
        return list;
    }
}
