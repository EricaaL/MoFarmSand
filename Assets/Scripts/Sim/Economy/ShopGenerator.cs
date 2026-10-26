using System;
using System.Collections.Generic;

public enum OfferKind { Seed, Modifier }

public struct ShopOffer
{
    public OfferKind kind;
    public string id;
    /// <summary>Base price without live disaster shocks.</summary>
    public int price;
    public int stock;
}

public static class ShopGenerator
{
    /// <summary>
    /// In-season seeds plus (optionally) passive modifiers the player doesn't own yet. Deterministic for the given rng.
    /// <paramref name="seedStock"/> is the units per seed; pass a value &lt;= 0 to use the full Merchant-day restock.
    /// </summary>
    public static List<ShopOffer> Generate(GameDatabase db, RunState run, CalendarDate date, SeededRng rng,
        bool includeModifiers = true, int seedStock = 0)
    {
        List<ShopOffer> offers = new List<ShopOffer>();
        if (db == null || db.prices == null || db.economy == null) return offers;

        // The Bigger Stockroom upgrade stocks more units, one more kind of seed (level 2) and one more charm (level 3).
        int level = run != null ? run.stockroomLevel : 0;
        if (seedStock <= 0) seedStock = StockroomLogic.RestockUnits(db.economy.restockSeedStock, level, db.stockroom);
        int seedKinds = StockroomLogic.SeedKinds(db.economy.shopSeedSlots, level, db.stockroom);
        int charmSlots = StockroomLogic.CharmSlots(db.economy.shopModifierSlots, level, db.stockroom);

        List<PriceTable.PriceRow> rows = new List<PriceTable.PriceRow>();
        foreach (PriceTable.PriceRow r in db.prices.rows)
            if (r.seed != null && r.StockedIn(date.season) && CropAvailability.IsSeedAvailable(r.seed, run.unlockedIds, db)) rows.Add(r);
        rng.Shuffle(rows);
        for (int i = 0; i < rows.Count && i < seedKinds; i++)
        {
            offers.Add(new ShopOffer
            {
                kind = OfferKind.Seed,
                id = rows[i].seed.Id,
                price = EconomyModel.SeedPrice(rows[i], date.season, db.economy),
                stock = seedStock
            });
        }

        if (includeModifiers && db.modifiers != null)
        {
            List<RunModifierData> pool = new List<RunModifierData>();
            foreach (RunModifierData m in db.modifiers.All)
                if (m != null && m.shopItem && !run.modifierIds.Contains(m.Id)) pool.Add(m);
            rng.Shuffle(pool);
            for (int i = 0; i < pool.Count && i < charmSlots; i++)
                offers.Add(new ShopOffer { kind = OfferKind.Modifier, id = pool[i].Id, price = pool[i].price, stock = Math.Max(1, pool[i].shopStock) });
        }
        return offers;
    }
}
