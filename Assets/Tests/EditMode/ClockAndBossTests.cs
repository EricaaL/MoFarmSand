using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ClockAndBossTests
{
    private readonly List<Object> created = new List<Object>();

    private T Track<T>(T o) where T : Object { created.Add(o); return o; }

    [TearDown]
    public void TearDown()
    {
        SetStatic(typeof(GameManager), "Instance", null);
        foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    private static void SetStatic(System.Type type, string prop, object value)
    {
        type.GetProperty(prop, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SetValue(null, value);
    }

    private static void SetProp(object target, string prop, object value)
    {
        target.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);
    }

    // ------------------------------------------------------------------ timer

    [Test]
    public void TimerCountsDownAndExpiresExactlyOnce()
    {
        DayTimer t = new DayTimer();
        t.Start(10f);

        Assert.IsFalse(t.Tick(4f));
        Assert.AreEqual(6f, t.Remaining, 1e-5f);
        Assert.AreEqual(0.4f, t.Progress01, 1e-5f);
        Assert.IsFalse(t.Tick(5.9f));
        Assert.IsTrue(t.Tick(0.2f), "runs out on this tick");
        Assert.AreEqual(0f, t.Remaining);
        Assert.AreEqual(1f, t.Progress01);
        Assert.IsFalse(t.Tick(1f), "and never fires again");
    }

    [Test]
    public void UnlimitedTimerNeverExpires()
    {
        DayTimer t = new DayTimer();
        t.Start(0f);
        Assert.IsTrue(t.Unlimited);
        for (int i = 0; i < 100; i++) Assert.IsFalse(t.Tick(100f));
        Assert.AreEqual(0f, t.Progress01);
    }

    [Test]
    public void NegativeDeltaTimeCannotAddTime()
    {
        DayTimer t = new DayTimer();
        t.Start(5f);
        t.Tick(-3f);
        Assert.AreEqual(5f, t.Remaining, 1e-5f);
    }

    [Test]
    public void DayPlanScalesDaylightBySeasonAndKeepsTheNightLimit()
    {
        GameDatabase db = TestUtil.LoadDb();
        RunState run = RunLogic.NewRun(4, db, new MetaProgress());

        float summer = 0f, winter = 0f, autumn = 0f, spring = 0f;
        foreach (MapNode n in run.map.nodes)
        {
            DayPlan p = DayPlanner.Plan(run, n.id, db);
            Assert.AreEqual(db.run.dayLengthSeconds * p.date.season.dayLengthMultiplier, p.dayLengthSeconds, 1e-3f);
            Assert.AreEqual(db.run.nightLengthSeconds, p.nightLengthSeconds, 1e-3f);
            if (p.date.season.seasonId == "summer") summer = p.dayLengthSeconds;
            if (p.date.season.seasonId == "winter") winter = p.dayLengthSeconds;
            if (p.date.season.seasonId == "autumn") autumn = p.dayLengthSeconds;
            if (p.date.season.seasonId == "spring") spring = p.dayLengthSeconds;
        }
        Assert.Greater(summer, winter, "winter days are shorter than summer days");
        Assert.Greater(spring, autumn, "autumn days are shorter than spring days");
        Assert.Greater(autumn, winter, "winter days are the shortest");
        Assert.Greater(db.run.dayLengthSeconds, 0f, "time is limited by default");
        Assert.Greater(db.run.nightLengthSeconds, 0f);
    }

    // ------------------------------------------------------------------ boss only eats at night

    private BossHunger NewBoss(out BossData data)
    {
        data = Track(TestUtil.Make<BossData>());
        data.maxFullness = 40f;
        BossHunger hunger = Track(new GameObject("Boss")).AddComponent<BossHunger>();
        SetField(hunger, "data", data);
        return hunger;
    }

    private GameManager NewManager(GamePhase phase)
    {
        GameManager gm = Track(new GameObject("GameManager")).AddComponent<GameManager>();
        SetStatic(typeof(GameManager), "Instance", gm);
        SetProp(gm, "Phase", phase);
        return gm;
    }

    [Test]
    public void BossCannotBeFedOrKilledDuringTheDay()
    {
        BossHunger hunger = NewBoss(out _);
        bool satisfied = false;
        hunger.OnBossSatisfied += () => satisfied = true;
        NewManager(GamePhase.Day);

        Assert.IsFalse(hunger.CanBeFed);
        for (int i = 0; i < 10; i++) hunger.Feed(20f);

        Assert.IsFalse(satisfied, "a daytime hit must not kill the boss");
        Assert.IsFalse(hunger.IsSatisfied);
        Assert.AreEqual(0f, hunger.Fullness01);
        Assert.IsTrue(hunger.gameObject.activeSelf, "the boss stays in the scene for the night");
    }

    [Test]
    public void BossEatsNormallyAtNight()
    {
        BossHunger hunger = NewBoss(out _);
        bool satisfied = false;
        hunger.OnBossSatisfied += () => satisfied = true;
        NewManager(GamePhase.Night);

        Assert.IsTrue(hunger.CanBeFed);
        hunger.Feed(20f);
        Assert.AreEqual(0.5f, hunger.Fullness01, 1e-5f);
        hunger.Feed(20f);
        Assert.IsTrue(satisfied);
    }

    [Test]
    public void BossStillEatsWithNoGameManagerSoUnitTestsAndPrototypesKeepWorking()
    {
        BossHunger hunger = NewBoss(out _);
        Assert.IsTrue(hunger.CanBeFed);
    }

    [Test]
    public void NightFallingOnAnAlreadySatisfiedBossFinishesTheDayInsteadOfHanging()
    {
        // Safety net for the old "stuck in night" bug: even if a boss somehow ended up satisfied during the day.
        BossHunger hunger = NewBoss(out _);
        GameManager gm = NewManager(GamePhase.Day);
        SetField(gm, "bossHunger", hunger);
        SetProp(hunger, "IsSatisfied", true);

        GamePhase final = GamePhase.Day;
        gm.OnPhaseChanged += p => final = p;
        gm.StartNight();

        Assert.AreEqual(GamePhase.Won, final, "the day resolves as a win rather than waiting forever");
    }

    [Test]
    public void NightTimeUpLosesOnlyDuringTheNight()
    {
        GameManager gm = NewManager(GamePhase.Day);
        gm.NightTimeUp();
        Assert.AreEqual(GamePhase.Day, gm.Phase, "ignored outside the night");

        SetProp(gm, "Phase", GamePhase.Night);
        gm.NightTimeUp();
        Assert.AreEqual(GamePhase.Lost, gm.Phase);
    }
}
