using System;
using System.Collections.Generic;

/// <summary>Saved form of one shop line: what's left on the shelf. Prices are always recomputed from the PriceTable.</summary>
[Serializable]
public class ShopStockEntry
{
    public OfferKind kind;
    public string id;
    public int stock;
}

/// <summary>
/// The merchant's shelf persists between days. It is only refilled on Merchant days (when a day finishes,
/// whatever is left becomes the new shelf), so normal days can only buy what is still there.
/// </summary>
public static class ShopStock
{
    public static List<ShopStockEntry> FromOffers(IEnumerable<ShopOffer> offers)
    {
        List<ShopStockEntry> list = new List<ShopStockEntry>();
        foreach (ShopOffer o in offers)
            if (o.stock > 0) list.Add(new ShopStockEntry { kind = o.kind, id = o.id, stock = o.stock });
        return list;
    }

    /// <summary>Rebuilds shop lines from the saved shelf with today's prices. Unknown or already-owned lines are dropped.</summary>
    public static List<ShopOffer> ToOffers(RunState run, GameDatabase db, CalendarDate date)
    {
        List<ShopOffer> offers = new List<ShopOffer>();
        foreach (ShopStockEntry e in run.shopStock)
        {
            if (e.stock <= 0) continue;
            if (e.kind == OfferKind.Seed)
            {
                PriceTable.PriceRow row = db.prices.RowForSeed(e.id);
                if (row == null || !CropAvailability.IsSeedAvailable(row.seed, run.unlockedIds, db)) continue;
                offers.Add(new ShopOffer { kind = OfferKind.Seed, id = e.id, stock = e.stock, price = EconomyModel.SeedPrice(row, date.season, db.economy) });
            }
            else
            {
                if (db.modifiers == null || !db.modifiers.TryGet(e.id, out RunModifierData m) || run.modifierIds.Contains(m.Id)) continue;
                offers.Add(new ShopOffer { kind = OfferKind.Modifier, id = e.id, stock = e.stock, price = m.price });
            }
        }
        return offers;
    }
}
