using System.Collections.Generic;
using UnityEngine;

public enum InfoTone { Normal, Good, Bad, Muted }

public struct InfoLine
{
    public string label;
    public string value;
    public InfoTone tone;
}

/// <summary>Display-ready description of one item. Built by <see cref="ItemInfoBuilder"/>; the UI only draws it.</summary>
public class ItemInfo
{
    public string title;
    public string subtitle;
    public string summary;
    public Color color = Color.white;
    public List<InfoLine> lines = new List<InfoLine>();

    public void Add(string label, string value, InfoTone tone = InfoTone.Normal)
        => lines.Add(new InfoLine { label = label, value = value, tone = tone });
}

/// <summary>Today's conditions, supplied by the scene so the builder itself stays pure.</summary>
public class ItemInfoContext
{
    public GameDatabase db;
    public CalendarDate date;
    public DayWeather weather;
    /// <summary>Growth modifiers on top of climate right now (disasters, rest stop, fertilizer...).</summary>
    public float extraGrowth = 1f;
    public float sellShock = 1f;
    public float seedShock = 1f;
    public MarketState market = new MarketState();
    public int owned;
    /// <summary>Average strength of the owned stack (1 = normal).</summary>
    public float ownedQuality = 1f;
    /// <summary>Set when the item is a locked crop variant: the permanent unlock that makes it available.</summary>
    public UnlockData lockedBy;
    /// <summary>This year's boss scaling, so hit counts match the boss you'll actually face.</summary>
    public BossScale bossScale = BossScale.Identity;
}

/// <summary>Turns an item plus today's conditions into readable facts: grow times, best seasons, prices, what a crop does.</summary>
public static class ItemInfoBuilder
{
    public static ItemInfo Build(ItemData item, ItemInfoContext ctx)
    {
        ItemInfo info;
        if (item is SeedData seed) info = BuildSeed(seed, ctx);
        else if (item is CropData crop) info = BuildCrop(crop, ctx);
        else return new ItemInfo { title = item != null ? item.DisplayName : "Unknown" };

        // A locked variant still shows its full stats, so you can decide whether it's worth unlocking.
        if (ctx.lockedBy != null)
        {
            info.subtitle += "  -  LOCKED";
            info.lines.Insert(0, new InfoLine
            {
                label = "Locked",
                value = $"Buy \"{ctx.lockedBy.displayName}\" in the upgrade shop ({ctx.lockedBy.cost} vault coins)",
                tone = InfoTone.Bad
            });
        }
        return info;
    }

    // ------------------------------------------------------------------ seeds

    private static ItemInfo BuildSeed(SeedData seed, ItemInfoContext ctx)
    {
        CropData crop = seed.grows;
        ItemInfo info = new ItemInfo
        {
            title = seed.DisplayName,
            subtitle = "Seed",
            color = seed.ItemColor,
            summary = crop != null ? $"Plant it in an empty plot to grow {crop.displayName} - press E at the plot, or drag it from the hotbar onto a plot. Planting uses up one seed."
                                   : "A seed with no crop assigned."
        };
        info.Add("You have", ctx.owned.ToString(), ctx.owned > 0 ? InfoTone.Normal : InfoTone.Muted);
        if (crop == null) return info;

        SeasonData current = ctx.date.season;
        string seasonName = current != null ? current.displayName : "this season";

        info.Add("Grows into", $"{crop.displayName} (x{crop.harvestYield} per harvest)");

        // Grow time: typical for the current season, and what it will actually be today.
        float typicalMult = SeasonMultiplier(crop, current, ctx);
        float todayMult = ClimateModel.GrowthMultiplier(crop, ctx.date, ctx.weather, ctx.db.climate) * ctx.extraGrowth;
        info.Add($"Grow time in {seasonName}", $"about {Seconds(crop.growTime, typicalMult, ctx)}  (base {crop.growTime:0}s)", Tone(typicalMult));
        info.Add("Grow time today", $"about {Seconds(crop.growTime, todayMult, ctx)}  ({WeatherNote(ctx)}, speed x{todayMult:0.0#})", Tone(todayMult));

        // Out-of-season crops come out weaker: less damage, fewer shield charges.
        float quality = ClimateModel.Quality(crop, ctx.date, ctx.weather, ctx.db.climate);
        info.Add("Crop strength if planted now", $"{Percent(quality)}  ({EffectText(crop, quality)})", QualityTone(quality));

        // Season guide, best first marked.
        SeasonData best = BestSeason(crop, ctx, out float bestMult);
        SeasonData worst = WorstSeason(crop, ctx, out float worstMult);
        if (best != null) info.Add("Best season to plant", $"{best.displayName}  (speed x{bestMult:0.0#}, about {Seconds(crop.growTime, bestMult, ctx)})", InfoTone.Good);

        foreach (SeasonData s in ctx.db.calendar.seasons)
        {
            if (s == null) continue;
            float m = SeasonMultiplier(crop, s, ctx);
            string tag = s == best ? "  (best)" : s == worst && worstMult < bestMult - 0.01f ? "  (worst)" : string.Empty;
            string now = s == current ? "  <- now" : string.Empty;
            float q = SeasonQuality(crop, s, ctx);
            info.Add("  " + s.displayName, $"x{m:0.0#}  about {Seconds(crop.growTime, m, ctx)}  strength {Percent(q)}{tag}{now}", s == best ? InfoTone.Good : s == worst ? InfoTone.Bad : InfoTone.Normal);
        }

        if (current != null && best != null)
        {
            float ratio = bestMult > 0f ? typicalMult / bestMult : 1f;
            if (current == best || ratio >= 0.95f) info.Add("Planting now", "Great time to plant", InfoTone.Good);
            else if (ratio >= 0.65f) info.Add("Planting now", $"Okay, but {best.displayName} is better");
            else info.Add("Planting now", $"Poor time. Wait for {best.displayName} if you can", InfoTone.Bad);
        }

        // Where to buy it.
        PriceTable.PriceRow row = ctx.db.prices != null ? ctx.db.prices.RowForSeed(seed.Id) : null;
        if (row != null)
        {
            if (row.StockedIn(current)) info.Add("Merchant price", $"{EconomyModel.SeedPrice(row, current, ctx.db.economy, ctx.seedShock)} coins today");
            else info.Add("Merchant price", $"not sold in {seasonName}", InfoTone.Muted);
        }
        return info;
    }

    // ------------------------------------------------------------------ crops

    private static ItemInfo BuildCrop(CropData crop, ItemInfoContext ctx)
    {
        bool shooter = crop.type == CropType.Shooter;
        bool attack = crop.type == CropType.Projectile || shooter;
        ItemInfo info = new ItemInfo
        {
            title = crop.displayName,
            subtitle = shooter ? "Crop  -  Shooter" : attack ? "Crop  -  Attack" : "Crop  -  Defense",
            color = crop.color,
            summary = shooter ? $"Hold left-click to fire it at the boss at night. One crop holds {crop.ammoPerCrop} shots. Drag it off the hotbar to fire one shot there."
                    : attack ? "Throw it at the boss at night to feed it. Hold left-click to aim, release to throw (right-click cancels), or drag it from the hotbar to where you want it to land."
                             : "Eat it for a Bark Shield that protects you. Right-click to eat, or drag it off the hotbar."
        };
        info.Add("You have", ctx.owned.ToString(), ctx.owned > 0 ? InfoTone.Normal : InfoTone.Muted);

        // Strength of the stack you carry: grown out of season means weaker.
        float stack = ctx.owned > 0 ? ctx.ownedQuality : 1f;
        if (ctx.owned > 0)
            info.Add("Your crops' strength", $"{Percent(stack)}  ({EffectText(crop, stack)})", QualityTone(stack));

        if (attack)
        {
            string unit = shooter ? "shot" : "hit";
            info.Add("What it does", shooter
                ? $"Shooter: +{crop.fullnessValue:0.#} fullness per shot, {crop.ammoPerCrop} shots per crop at full strength"
                : $"Attack: +{crop.fullnessValue:0} fullness to the boss per hit at full strength", InfoTone.Good);
            if (crop.scareSeconds > 0f)
                info.Add("Scares the boss", $"it runs away for {crop.scareSeconds:0.#}s, then chases you again", InfoTone.Good);
            BossData boss = ctx.db.mapGen != null && ctx.db.mapGen.bossType != null ? ctx.db.mapGen.bossType.encounter : null;
            if (boss != null && crop.fullnessValue > 0f)
                info.Add($"Versus the {boss.displayName}", $"about {Mathf.CeilToInt(boss.maxFullness * ctx.bossScale.fullnessMult / crop.fullnessValue)} {unit}s to fill it at full strength");
            info.Add("Used", "at night only. The boss ignores crops by day", InfoTone.Muted);
        }
        else
        {
            // The shield is a single flag: it absorbs exactly one hit however many charges the crop lists.
            info.Add("What it does", $"Defense: grants a Bark Shield that blocks the next {Mathf.Max(1, crop.shieldCharges)} hit(s) at full strength", InfoTone.Good);
            info.Add("Heals?", "No. It stops damage instead of restoring hearts", InfoTone.Muted);
        }

        // Price in the current season.
        SeasonData current = ctx.date.season;
        string seasonName = current != null ? current.displayName : "this season";
        PriceTable.PriceRow row = ctx.db.prices != null ? ctx.db.prices.RowForCrop(crop.Id) : null;
        if (row != null)
        {
            float demand = ctx.market != null ? ctx.market.GetDemand(crop.Id) : 1f;
            int today = EconomyModel.SellPrice(row, current, demand, ctx.sellShock);
            float seasonMult = row.SeasonMultFor(current);
            info.Add($"Sells for ({seasonName})", $"{today} coins each", InfoTone.Good);
            info.Add("  base price", $"{row.baseSell} coins, season x{seasonMult:0.0#}");
            info.Add("  market demand", $"{Mathf.RoundToInt(demand * 100f)}%", demand < 0.8f ? InfoTone.Bad : demand > 1.05f ? InfoTone.Good : InfoTone.Normal);

            SeasonData bestSell = null; float bestSellMult = 0f;
            foreach (SeasonData s in ctx.db.calendar.seasons)
            {
                float m = row.SeasonMultFor(s);
                if (s != null && m > bestSellMult) { bestSellMult = m; bestSell = s; }
            }
            if (bestSell != null)
                info.Add("Best season to sell", $"{bestSell.displayName}  ({EconomyModel.SellPrice(row, bestSell, 1f)} coins at normal demand)", bestSell == current ? InfoTone.Good : InfoTone.Normal);
        }

        // Growing it.
        float todayMult = ClimateModel.GrowthMultiplier(crop, ctx.date, ctx.weather, ctx.db.climate) * ctx.extraGrowth;
        info.Add("Grows in", $"about {Seconds(crop.growTime, todayMult, ctx)} today  (base {crop.growTime:0}s)", Tone(todayMult));
        SeasonData bestGrow = BestSeason(crop, ctx, out _);
        if (bestGrow != null) info.Add("Best season to grow", bestGrow.displayName);
        info.Add("Harvest", $"{crop.harvestYield} per plant");
        return info;
    }

    // ------------------------------------------------------------------ shared helpers

    /// <summary>Growth speed multiplier in a typical (average, noise-free) day of the season.</summary>
    public static float SeasonMultiplier(CropData crop, SeasonData season, ItemInfoContext ctx)
    {
        if (season == null) return 1f;
        CalendarDate mid = new CalendarDate { season = season, seasonProgress01 = 0.5f };
        DayWeather typical = new DayWeather
        {
            temperature = season.temperatureCurve.Evaluate(0.5f),
            precipitation = Mathf.Clamp01(season.precipitationCurve.Evaluate(0.5f))
        };
        return ClimateModel.GrowthMultiplier(crop, mid, typical, ctx.db.climate);
    }

    /// <summary>Crop strength in a typical day of the season.</summary>
    public static float SeasonQuality(CropData crop, SeasonData season, ItemInfoContext ctx)
    {
        if (season == null) return 1f;
        CalendarDate mid = new CalendarDate { season = season, seasonProgress01 = 0.5f };
        DayWeather typical = new DayWeather
        {
            temperature = season.temperatureCurve.Evaluate(0.5f),
            precipitation = Mathf.Clamp01(season.precipitationCurve.Evaluate(0.5f))
        };
        return ClimateModel.Quality(crop, mid, typical, ctx.db.climate);
    }

    private static string Percent(float q) => $"{Mathf.RoundToInt(q * 100f)}%";

    private static InfoTone QualityTone(float q) => q >= 0.95f ? InfoTone.Good : q < 0.6f ? InfoTone.Bad : InfoTone.Normal;

    /// <summary>What a given strength means in play: boss damage for attack crops, shield charges for support crops.</summary>
    public static string EffectText(CropData crop, float quality)
    {
        if (crop.type == CropType.Projectile || crop.type == CropType.Shooter)
            return $"+{crop.fullnessValue * quality:0.#} fullness per {(crop.type == CropType.Shooter ? "shot" : "hit")} instead of +{crop.fullnessValue:0.#}";
        int charges = ClimateModel.ShieldCharges(crop, quality);
        return $"{charges} shield charge{(charges == 1 ? "" : "s")} instead of {Mathf.Max(1, crop.shieldCharges)}";
    }

    public static SeasonData BestSeason(CropData crop, ItemInfoContext ctx, out float multiplier)
    {
        SeasonData best = null; multiplier = 0f;
        foreach (SeasonData s in ctx.db.calendar.seasons)
        {
            if (s == null) continue;
            float m = SeasonMultiplier(crop, s, ctx);
            if (best == null || m > multiplier) { best = s; multiplier = m; }
        }
        return best;
    }

    public static SeasonData WorstSeason(CropData crop, ItemInfoContext ctx, out float multiplier)
    {
        SeasonData worst = null; multiplier = 0f;
        foreach (SeasonData s in ctx.db.calendar.seasons)
        {
            if (s == null) continue;
            float m = SeasonMultiplier(crop, s, ctx);
            if (worst == null || m < multiplier) { worst = s; multiplier = m; }
        }
        return worst;
    }

    public static float GrowSeconds(float baseSeconds, float speedMultiplier) => baseSeconds / Mathf.Max(0.05f, speedMultiplier);

    private static string Seconds(float baseSeconds, float speedMultiplier, ItemInfoContext ctx)
        => $"{Mathf.RoundToInt(GrowSeconds(baseSeconds, ClimateModel.CapRate(baseSeconds, speedMultiplier, ctx.db.climate)))}s";

    private static InfoTone Tone(float mult) => mult >= 1.1f ? InfoTone.Good : mult < 0.75f ? InfoTone.Bad : InfoTone.Normal;

    private static string WeatherNote(ItemInfoContext ctx) => $"{ctx.weather.kind.ToString().ToLower()}, {ctx.weather.temperature:0} C";
}
