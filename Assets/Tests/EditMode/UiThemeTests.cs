using NUnit.Framework;
using UnityEngine;

/// <summary>The Canva UI design is data: one UiTheme asset, season / node-type icons, and the quick facts the item cards show.</summary>
public class UiThemeTests
{
    private GameDatabase db;

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    [Test]
    public void DatabaseHasAThemeWithItsFontsAndTextures()
    {
        UiTheme t = db.uiTheme;
        Assert.IsNotNull(t, "GameDatabase.uiTheme is empty. Run MoFarmSand > Add UI Theme.");
        Assert.IsNotNull(t.body, "body font"); Assert.IsNotNull(t.bold, "bold font"); Assert.IsNotNull(t.italic, "italic font");
        // t.display (Beautiful Magic) is personal-use and git-ignored: it may be missing on a fresh clone, headings then fall back to bold.
        Assert.IsNotNull(t.coin, "coin"); Assert.IsNotNull(t.piggyBank, "piggy bank"); Assert.IsNotNull(t.bread, "bread"); Assert.IsNotNull(t.growIcon, "grow icon");
        Assert.IsNotNull(t.heartFull, "full heart"); Assert.IsNotNull(t.heartGone, "empty heart"); Assert.IsNotNull(t.dayNightDial, "day/night dial");
        Assert.IsNotNull(t.merchantPanel, "merchant panel"); Assert.IsNotNull(t.legend, "legend"); Assert.IsNotNull(t.eventSilhouette, "event silhouette");
    }

    [Test]
    public void EverySeasonHasAnIconAndAMapColour()
    {
        foreach (SeasonData s in db.calendar.seasons)
        {
            Assert.IsNotNull(s.icon, $"season '{s.seasonId}' has no icon");
            Assert.Greater(s.mapColor.a, 0.01f, $"season '{s.seasonId}' has no map colour");
        }
    }

    [Test]
    public void TheRouteMapLegendTypesHaveArtAndALabel()
    {
        foreach (string id in new[] { "farm", "merchant", "event", "disaster", "boss" })
        {
            Assert.IsTrue(db.nodeTypes.TryGet(id, out NodeTypeData type), $"no node type '{id}'");
            Assert.IsNotNull(type.icon, $"node type '{id}' has no icon");
            Assert.IsFalse(string.IsNullOrWhiteSpace(type.legendLabel), $"node type '{id}' has no legend label");
        }
    }

    private ItemInfo Info(string id)
    {
        CalendarDate date = db.Calendar.DateOf(0);
        var ctx = new ItemInfoContext { db = db, date = date, weather = ClimateModel.SampleDay(date, db.climate, new SeededRng(1)) };
        return ItemInfoBuilder.Build(db.items.Get(id), ctx);
    }

    [Test]
    public void SeedCardsCarryGrowTimeAndWhatTheCropFeeds()
    {
        ItemInfo tomato = Info("seed_tomato");
        Assert.That(tomato.growTime, Does.EndWith("s"));
        Assert.IsFalse(string.IsNullOrEmpty(tomato.fullness), "a lobbed crop feeds the boss");

        ItemInfo corn = Info("seed_corn");
        Assert.That(corn.fullness, Does.Contain("per ammo"), "shooters show the per-shot value beside the per-crop total");
    }

    [Test]
    public void SupportCropsFeedNothing()
    {
        Assert.IsNull(Info("seed_lettuce").fullness);
    }

    [Test]
    public void FullnessShowsTheCropStrengthWhenItIsNotFull()
    {
        CropData tomato = (CropData)db.items.Get("tomato");
        CalendarDate date = db.Calendar.DateOf(0);
        var ctx = new ItemInfoContext { db = db, date = date, weather = ClimateModel.SampleDay(date, db.climate, new SeededRng(1)), owned = 5, ownedQuality = 0.5f };
        ItemInfo weak = ItemInfoBuilder.Build(tomato, ctx);
        Assert.That(weak.fullness, Does.Contain("(50%)"), "a half-strength stack says so beside the bread number");

        ctx.ownedQuality = 1f;
        Assert.That(ItemInfoBuilder.Build(tomato, ctx).fullness, Does.Not.Contain("%"), "full strength adds no note");
    }

    [Test]
    public void TheGrowTimeIconFollowsTheCropsBestSeason()
    {
        Assert.AreEqual("autumn", Info("seed_pumpkin").bestSeason.seasonId, "pumpkin grows best in autumn: the fallen leaf");
        Assert.AreEqual("summer", Info("seed_tomato").bestSeason.seasonId, "tomato grows best in summer: the sun");
        foreach (string id in new[] { "seed_pumpkin", "seed_tomato", "seed_peas", "seed_garlic" })
            Assert.IsNotNull(Info(id).bestSeason.icon, $"{id}: the best season has no icon");
    }
}
