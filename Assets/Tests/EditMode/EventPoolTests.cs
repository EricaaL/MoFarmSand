using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class EventPoolTests
{
    private GameDatabase db;

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    private static List<SlotSave> Bag(params (string id, int count)[] stacks)
    {
        List<SlotSave> slots = new List<SlotSave>();
        foreach ((string id, int count) in stacks) slots.Add(new SlotSave(id, count));
        InventoryOps.EnsureSize(slots);
        return slots;
    }

    // ------------------------------------------------------------------ RemoveRandom

    [Test]
    public void RemoveRandomTakesExactlyTheRequestedUnits()
    {
        List<SlotSave> bag = Bag(("a", 5), ("b", 5));
        int taken = InventoryOps.RemoveRandom(bag, 4, id => true, new SeededRng(1));
        Assert.AreEqual(4, taken);
        Assert.AreEqual(6, InventoryOps.CountOf(bag, "a") + InventoryOps.CountOf(bag, "b"));
    }

    [Test]
    public void RemoveRandomNeverTouchesIneligibleItems()
    {
        List<SlotSave> bag = Bag(("keep", 9), ("lose", 3));
        int taken = InventoryOps.RemoveRandom(bag, 10, id => id == "lose", new SeededRng(2));
        Assert.AreEqual(3, taken, "stops when the eligible items run out");
        Assert.AreEqual(9, InventoryOps.CountOf(bag, "keep"));
        Assert.AreEqual(0, InventoryOps.CountOf(bag, "lose"));
    }

    [Test]
    public void RemoveRandomClearsEmptiedSlots()
    {
        List<SlotSave> bag = Bag(("a", 1));
        InventoryOps.RemoveRandom(bag, 1, id => true, new SeededRng(3));
        Assert.AreEqual(0, bag[0].count);
        Assert.IsTrue(string.IsNullOrEmpty(bag[0].itemId));
    }

    [Test]
    public void RemoveRandomFromAnEmptyBagTakesNothing()
    {
        List<SlotSave> bag = Bag();
        Assert.AreEqual(0, InventoryOps.RemoveRandom(bag, 3, id => true, new SeededRng(4)));
    }

    [Test]
    public void RemoveRandomIsDeterministicForTheSameSeed()
    {
        List<SlotSave> a = Bag(("x", 4), ("y", 4), ("z", 4));
        List<SlotSave> b = Bag(("x", 4), ("y", 4), ("z", 4));
        InventoryOps.RemoveRandom(a, 5, id => true, SeededRng.Derive(7, 3, RngStreams.EventLoss));
        InventoryOps.RemoveRandom(b, 5, id => true, SeededRng.Derive(7, 3, RngStreams.EventLoss));
        for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].count, b[i].count);
    }

    // ------------------------------------------------------------------ LoseItemsEffect

    private RunState RunHolding(params string[] itemIds)
    {
        RunState run = RunLogic.NewRun(3, db, new MetaProgress());
        run.inventory.Clear();
        foreach (string id in itemIds) InventoryOps.Add(run.inventory, id, 6);
        run.pendingNodeId = 5;
        return run;
    }

    private LoseItemsEffect Lose(LoseItemsEffect.Kind kind, int count)
    {
        LoseItemsEffect e = TestUtil.Make<LoseItemsEffect>();
        e.kind = kind; e.count = count;
        return e;
    }

    [Test]
    public void SeedsOnlyNeverTakesACropAndCropsOnlyNeverTakesASeed()
    {
        ItemData seed = db.items.All.OfType<SeedData>().First();
        ItemData crop = db.items.All.OfType<CropData>().First();

        RunState run = RunHolding(seed.Id, crop.Id);
        Lose(LoseItemsEffect.Kind.SeedsOnly, 6).Apply(run, db);
        Assert.AreEqual(6, InventoryOps.CountOf(run.inventory, crop.Id));
        Assert.AreEqual(0, InventoryOps.CountOf(run.inventory, seed.Id));

        run = RunHolding(seed.Id, crop.Id);
        Lose(LoseItemsEffect.Kind.CropsOnly, 6).Apply(run, db);
        Assert.AreEqual(6, InventoryOps.CountOf(run.inventory, seed.Id));
        Assert.AreEqual(0, InventoryOps.CountOf(run.inventory, crop.Id));
    }

    [Test]
    public void LoseItemsGivesTheSameLossForTheSameNode()
    {
        string a = db.items.All.OfType<SeedData>().First().Id;
        string b = db.items.All.OfType<CropData>().First().Id;
        RunState one = RunHolding(a, b), two = RunHolding(a, b);
        LoseItemsEffect effect = Lose(LoseItemsEffect.Kind.SeedsOrCrops, 4);
        effect.Apply(one, db);
        effect.Apply(two, db);
        Assert.AreEqual(InventoryOps.CountOf(one.inventory, a), InventoryOps.CountOf(two.inventory, a));
        Assert.AreEqual(8, InventoryOps.CountOf(one.inventory, a) + InventoryOps.CountOf(one.inventory, b));
    }

    // ------------------------------------------------------------------ the events

    private static readonly string[] BadEvents = { "thieving_cat", "toll_goose", "seed_moles", "scarecrow_hex" };

    [Test]
    public void BadEventsOnlyOfferCostlyChoices()
    {
        foreach (string id in BadEvents)
        {
            Assert.IsTrue(db.events.TryGet(id, out EventData evt), $"run 'MoFarmSand > Add Run Systems' to create {id}");
            Assert.IsTrue(evt.seasons.Count == 0, $"{id} can appear in any season");
            foreach (EventData.EventOption o in evt.options)
            {
                Assert.IsNotEmpty(o.effects, $"{id}: '{o.label}' must cost something");
                foreach (RunEffect e in o.effects)
                    Assert.IsTrue(e is DamageEffect || e is LoseItemsEffect || e is MaxHpEffect { delta: < 0 } || e is AddCoinsEffect { amount: < 0 },
                        $"{id}: '{o.label}' has a non-negative effect ({e.Describe()})");
            }
        }
    }

    [Test]
    public void EachSeasonHasExactlyTwoSeasonOnlyEvents()
    {
        foreach (SeasonData season in db.calendar.seasons)
        {
            List<EventData> only = db.events.All.Where(e => e.seasons.Count > 0 && e.AllowedIn(season)).ToList();
            Assert.AreEqual(2, only.Count, $"{season.name} season-only events");
            foreach (EventData e in only) Assert.AreEqual(1, e.seasons.Count, $"{e.eventId} belongs to one season");
        }
    }

    [Test]
    public void SeasonEventsAreHiddenInOtherSeasons()
    {
        SeasonData first = db.calendar.seasons[0], last = db.calendar.seasons[db.calendar.seasons.Count - 1];
        foreach (EventData e in db.events.All.Where(e => e.seasons.Contains(first)))
            Assert.IsFalse(e.AllowedIn(last), e.eventId);
    }
}
