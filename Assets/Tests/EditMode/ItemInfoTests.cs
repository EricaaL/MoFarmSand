using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class ItemInfoTests
{
    private GameDatabase db;

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    private ItemInfoContext Ctx(int day, float extraGrowth = 1f, float demand = 1f, float sellShock = 1f, int owned = 0)
    {
        CalendarDate date = db.Calendar.DateOf(day);
        MarketState market = new MarketState();
        market.SetDemand("tomato", demand);
        return new ItemInfoContext
        {
            db = db, date = date, weather = ClimateModel.SampleDay(date, db.climate, new SeededRng(day)),
            extraGrowth = extraGrowth, market = market, sellShock = sellShock, owned = owned
        };
    }

    private static InfoLine Find(ItemInfo info, string labelStart)
    {
        foreach (InfoLine l in info.lines) if (l.label.StartsWith(labelStart)) return l;
        Assert.Fail($"no line starting with '{labelStart}' in: {string.Join(" | ", info.lines.ConvertAll(x => x.label))}");
        return default;
    }

    private static int FirstNumber(string text) => int.Parse(Regex.Match(text, @"\d+").Value);

    private SeedData Seed(string id) => (SeedData)db.items.Get(id);
    private CropData Crop(string id) => (CropData)db.items.Get(id);

    // ------------------------------------------------------------------ seeds

    [Test]
    public void SeedShowsGrowTimeForTheCurrentSeasonAndToday()
    {
        ItemInfo info = ItemInfoBuilder.Build(Seed("seed_tomato"), Ctx(day: 0, owned: 3));

        Assert.AreEqual("Seed", info.subtitle);
        Assert.AreEqual("3", Find(info, "You have").value);
        Assert.AreEqual("Tomato", Find(info, "Grows into").value.Split(' ')[0]);

        InfoLine season = Find(info, "Grow time in Spring");
        InfoLine today = Find(info, "Grow time today");
        Assert.That(season.value, Does.Contain("about"));
        Assert.That(today.value, Does.Contain("about"));
    }

    [Test]
    public void TodaysGrowTimeFollowsTheMultiplierAndIsNeverFasterThanBaseWhenSlowed()
    {
        SeedData seed = Seed("seed_tomato");
        CropData tomato = Crop("tomato");
        int normal = FirstNumber(Find(ItemInfoBuilder.Build(seed, Ctx(0, extraGrowth: 1f)), "Grow time today").value);
        int slowed = FirstNumber(Find(ItemInfoBuilder.Build(seed, Ctx(0, extraGrowth: 0.5f)), "Grow time today").value);
        int boosted = FirstNumber(Find(ItemInfoBuilder.Build(seed, Ctx(0, extraGrowth: 2f)), "Grow time today").value);

        Assert.Greater(slowed, normal, "a drought or frost makes it take longer");
        Assert.Less(boosted, normal, "fertilizer makes it faster");

        ItemInfoContext c = Ctx(0);
        float mult = ClimateModel.GrowthMultiplier(tomato, c.date, c.weather, db.climate);
        Assert.AreEqual(Mathf.RoundToInt(tomato.growTime / mult), normal);
    }

    [Test]
    public void SeedNamesTheBestSeasonAndListsEverySeason()
    {
        ItemInfo info = ItemInfoBuilder.Build(Seed("seed_tomato"), Ctx(0));

        Assert.That(Find(info, "Best season to plant").value, Does.StartWith("Summer"));
        foreach (SeasonData s in db.calendar.seasons) Find(info, "  " + s.displayName);
        Assert.That(Find(info, "  Summer").value, Does.Contain("(best)"));
        Assert.That(Find(info, "  Spring").value, Does.Contain("<- now"));
    }

    [Test]
    public void EachSeedsBestSeasonIsReallyItsFastestSeason()
    {
        ItemInfoContext ctx = Ctx(0);
        foreach (ItemData item in db.items.All)
        {
            if (!(item is SeedData seed)) continue;
            SeasonData best = ItemInfoBuilder.BestSeason(seed.grows, ctx, out float bestMult);
            foreach (SeasonData s in db.calendar.seasons)
                Assert.LessOrEqual(ItemInfoBuilder.SeasonMultiplier(seed.grows, s, ctx), bestMult + 1e-5f, $"{seed.Id}: {s.displayName} beats the reported best {best.displayName}");
        }
        Assert.AreEqual("Winter", ItemInfoBuilder.BestSeason(Crop("spinach"), ctx, out _).displayName);
    }

    [Test]
    public void PlantingAdviceReflectsHowGoodTheCurrentSeasonIs()
    {
        // Tomato is best in summer: day 3 is the first summer day, day 0 is spring.
        Assert.That(Find(ItemInfoBuilder.Build(Seed("seed_tomato"), Ctx(3)), "Planting now").value, Does.Contain("Great"));
        string spring = Find(ItemInfoBuilder.Build(Seed("seed_tomato"), Ctx(0)), "Planting now").value;
        Assert.That(spring, Does.Contain("Summer"), "points to the better season");
        Assert.That(Find(ItemInfoBuilder.Build(Seed("seed_spinach"), Ctx(3)), "Planting now").value, Does.Contain("Poor"), "spinach in summer");
    }

    [Test]
    public void SeedShowsMerchantPriceOnlyInSeasonsItIsStocked()
    {
        SeedData seed = Seed("seed_tomato"); // stocked spring, summer; not winter
        Assert.That(Find(ItemInfoBuilder.Build(seed, Ctx(0)), "Merchant price").value, Does.Contain("coins"));
        Assert.That(Find(ItemInfoBuilder.Build(seed, Ctx(10)), "Merchant price").value, Does.Contain("not sold in Winter"));
    }

    // ------------------------------------------------------------------ crops

    [Test]
    public void AttackCropExplainsItIsThrownAtTheBoss()
    {
        ItemInfo info = ItemInfoBuilder.Build(Crop("tomato"), Ctx(0, owned: 2));

        Assert.That(info.subtitle, Does.Contain("Attack"));
        Assert.That(Find(info, "What it does").value, Does.Contain("Attack").And.Contain("fullness"));
        Assert.That(Find(info, "Versus").value, Does.Contain("hits"));
        Assert.That(info.summary, Does.Contain("Throw"));
    }

    [Test]
    public void SupportCropExplainsItIsADefensiveShieldNotAHeal()
    {
        ItemInfo info = ItemInfoBuilder.Build(Crop("turnip"), Ctx(0));

        Assert.That(info.subtitle, Does.Contain("Defense"));
        Assert.That(Find(info, "What it does").value, Does.Contain("Bark Shield"));
        Assert.That(Find(info, "Heals?").value, Does.StartWith("No"));
    }

    [Test]
    public void CropPriceMatchesTheEconomyForTheCurrentSeasonDemandAndShock()
    {
        CropData tomato = Crop("tomato");
        PriceTable.PriceRow row = db.prices.RowForCrop("tomato");

        ItemInfoContext spring = Ctx(0);
        int expectedSpring = EconomyModel.SellPrice(row, spring.date.season, 1f);
        Assert.AreEqual(expectedSpring, FirstNumber(Find(ItemInfoBuilder.Build(tomato, spring), "Sells for (Spring)").value));

        ItemInfoContext winter = Ctx(10);
        int expectedWinter = EconomyModel.SellPrice(row, winter.date.season, 1f);
        Assert.AreEqual(expectedWinter, FirstNumber(Find(ItemInfoBuilder.Build(tomato, winter), "Sells for (Winter)").value));
        Assert.AreNotEqual(expectedSpring, expectedWinter, "prices change with the season");

        int flooded = FirstNumber(Find(ItemInfoBuilder.Build(tomato, Ctx(0, demand: 0.5f)), "Sells for").value);
        int shocked = FirstNumber(Find(ItemInfoBuilder.Build(tomato, Ctx(0, sellShock: 2f)), "Sells for").value);
        Assert.Less(flooded, expectedSpring, "a flooded market pays less");
        Assert.Greater(shocked, expectedSpring, "a price shock pays more");
    }

    [Test]
    public void CropNamesTheBestSeasonToSell()
    {
        ItemInfo info = ItemInfoBuilder.Build(Crop("tomato"), Ctx(0));
        Assert.That(Find(info, "Best season to sell").value, Does.StartWith("Winter"), "tomato sells best in winter (x1.3)");
    }

    [Test]
    public void EveryItemInTheGameHasADescription()
    {
        foreach (ItemData item in db.items.All)
        {
            ItemInfo info = ItemInfoBuilder.Build(item, Ctx(0));
            Assert.IsNotEmpty(info.title, item.name);
            Assert.IsNotEmpty(info.subtitle, item.name);
            Assert.IsNotEmpty(info.summary, item.name);
            Assert.GreaterOrEqual(info.lines.Count, 4, item.name);
        }
    }

    [Test]
    public void UnknownItemDoesNotThrow()
    {
        Assert.DoesNotThrow(() => ItemInfoBuilder.Build(null, Ctx(0)));
    }
}
