using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BossScalingTests
{
    private GameDatabase db;
    private readonly List<Object> created = new List<Object>();

    private T Track<T>(T o) where T : Object { created.Add(o); return o; }

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    [TearDown]
    public void TearDown()
    {
        typeof(GameManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SetValue(null, null);
        foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    private static void SetField(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);

    private DifficultyConfig Config(float fullness = 0.25f, float speed = 0.05f, int dmgEvery = 3, int maxDmg = 1, float maxF = 3f, float maxS = 1.3f)
    {
        DifficultyConfig c = Track(TestUtil.Make<DifficultyConfig>());
        c.fullnessGrowthPerYear = fullness; c.speedGrowthPerYear = speed;
        c.damageEveryYears = dmgEvery; c.maxDamageBonus = maxDmg; c.maxFullnessMult = maxF; c.maxSpeedMult = maxS;
        return c;
    }

    // ------------------------------------------------------------------ the pure scaling

    [Test]
    public void YearOneAndMissingConfigAreTheBaseline()
    {
        Assert.IsTrue(BossScaling.ForYear(1, Config()).IsIdentity);
        Assert.IsTrue(BossScaling.ForYear(0, Config()).IsIdentity);
        Assert.IsTrue(BossScaling.ForYear(7, null).IsIdentity);
        Assert.AreEqual(string.Empty, BossScaling.Describe(BossScale.Identity));
    }

    [Test]
    public void BossesGetStrongerEveryYearUntilTheCaps()
    {
        DifficultyConfig cfg = Config();
        BossScale prev = BossScaling.ForYear(1, cfg);
        for (int year = 2; year <= 15; year++)
        {
            BossScale s = BossScaling.ForYear(year, cfg);
            Assert.GreaterOrEqual(s.fullnessMult, prev.fullnessMult, $"year {year}");
            Assert.GreaterOrEqual(s.speedMult, prev.speedMult, $"year {year}");
            Assert.GreaterOrEqual(s.damageBonus, prev.damageBonus, $"year {year}");
            Assert.LessOrEqual(s.fullnessMult, cfg.maxFullnessMult + 1e-5f);
            Assert.LessOrEqual(s.speedMult, cfg.maxSpeedMult + 1e-5f);
            Assert.LessOrEqual(s.damageBonus, cfg.maxDamageBonus);
            prev = s;
        }
        BossScale y2 = BossScaling.ForYear(2, cfg);
        Assert.AreEqual(1.25f, y2.fullnessMult, 1e-5f, "+25% per year");
        Assert.AreEqual(1.05f, y2.speedMult, 1e-5f);
        Assert.AreEqual(0, y2.damageBonus, "damage only rises after a few years");
        Assert.AreEqual(1, BossScaling.ForYear(4, cfg).damageBonus);
        Assert.AreEqual(3f, BossScaling.ForYear(99, cfg).fullnessMult, 1e-5f, "capped");
        Assert.AreEqual(1.3f, BossScaling.ForYear(99, cfg).speedMult, 1e-5f, "capped");
    }

    [Test]
    public void ZeroDamageIntervalMeansNeverHarderHitting()
    {
        Assert.AreEqual(0, BossScaling.ForYear(50, Config(dmgEvery: 0)).damageBonus);
    }

    [Test]
    public void TheRealConfigMakesYearTwoAndLaterNoticeablyHarder()
    {
        Assert.IsNotNull(db.difficulty, "run MoFarmSand > Add Run Systems to create DifficultyConfig");
        BossScale y1 = BossScaling.ForYear(1, db.difficulty);
        BossScale y2 = BossScaling.ForYear(2, db.difficulty);
        BossScale y5 = BossScaling.ForYear(5, db.difficulty);

        Assert.IsTrue(y1.IsIdentity);
        Assert.Greater(y2.fullnessMult, 1.1f, "Year 2 should already need clearly more feeding");
        Assert.Greater(y5.fullnessMult, y2.fullnessMult);
        Assert.Greater(y5.speedMult, y2.speedMult);
        Assert.Greater(BossScaling.ForYear(6, db.difficulty).damageBonus, 0, "eventually it hits harder");
        Assert.That(BossScaling.Describe(y5), Does.Contain("more feeding").And.Contain("faster"));
    }

    [Test]
    public void TheCapsKeepEveryYearFairSoTheCaterpillarNeverOutrunsTheFarmer()
    {
        // The farmer's walking speed (PlayerController default) is 5. No stage may outrun it, even in the far future.
        float cap = BossScaling.ForYear(999, db.difficulty).speedMult;
        for (BossData stage = db.mapGen.bossType.encounter; stage != null; stage = stage.nextStage)
            Assert.Less(stage.chaseSpeed * cap, 5f, $"a capped {stage.displayName} must still be slower than the farmer");
        Assert.LessOrEqual(db.difficulty.maxDamageBonus, 1, "at most one extra heart per hit");
    }

    // ------------------------------------------------------------------ the boss itself

    private BossHunger NewBoss(float maxFullness, float speed, int damage)
    {
        BossData data = Track(TestUtil.Make<BossData>());
        data.maxFullness = maxFullness; data.chaseSpeed = speed; data.damage = damage;
        BossHunger hunger = Track(new GameObject("Boss")).AddComponent<BossHunger>();
        SetField(hunger, "data", data);
        return hunger;
    }

    [Test]
    public void ScalingRaisesHowMuchFeedingTheBossNeeds()
    {
        BossHunger plain = NewBoss(40f, 3f, 1);
        BossHunger tough = NewBoss(40f, 3f, 1);
        tough.ApplyScaling(new BossScale { fullnessMult = 1.5f, speedMult = 1f, damageBonus = 0 });

        bool plainDone = false, toughDone = false;
        plain.OnBossSatisfied += () => plainDone = true;
        tough.OnBossSatisfied += () => toughDone = true;

        plain.Feed(20f); plain.Feed(20f);
        tough.Feed(20f); tough.Feed(20f);
        Assert.IsTrue(plainDone, "40 fullness is enough at baseline");
        Assert.IsFalse(toughDone, "but the tougher boss needs 60");
        Assert.AreEqual(40f / 60f, tough.Fullness01, 1e-4f);

        tough.Feed(20f);
        Assert.IsTrue(toughDone);
    }

    [Test]
    public void ScalingAffectsSpeedAndDamageButNotAZeroDamageBoss()
    {
        BossHunger boss = NewBoss(40f, 4f, 1);
        Assert.AreEqual(4f, boss.ChaseSpeed, 1e-5f);
        Assert.AreEqual(1, boss.Damage);

        boss.ApplyScaling(new BossScale { fullnessMult = 1f, speedMult = 1.25f, damageBonus = 1 });
        Assert.AreEqual(5f, boss.ChaseSpeed, 1e-5f);
        Assert.AreEqual(2, boss.Damage);

        BossHunger harmless = NewBoss(40f, 4f, 0);
        harmless.ApplyScaling(new BossScale { fullnessMult = 1f, speedMult = 1f, damageBonus = 1 });
        Assert.AreEqual(0, harmless.Damage, "a stage that deals no damage stays harmless");
    }

    [Test]
    public void ScalingCarriesOverToTheEvolvedStage()
    {
        BossData butterfly = Track(TestUtil.Make<BossData>());
        butterfly.maxFullness = 20f; butterfly.chaseSpeed = 4f; butterfly.damage = 1;
        BossData cat = Track(TestUtil.Make<BossData>());
        cat.maxFullness = 20f; cat.chaseSpeed = 3f; cat.damage = 1; cat.nextStage = butterfly;

        BossHunger hunger = Track(new GameObject("Boss")).AddComponent<BossHunger>();
        SetField(hunger, "data", cat);
        SetField(hunger, "evolveGraceSeconds", 0f);
        hunger.ApplyScaling(new BossScale { fullnessMult = 2f, speedMult = 1.5f, damageBonus = 1 });

        hunger.Feed(40f); // 20 x 2 = 40 fills the caterpillar stage
        Assert.AreSame(butterfly, hunger.Data, "evolved");
        Assert.AreEqual(6f, hunger.ChaseSpeed, 1e-5f, "the butterfly is scaled too");
        Assert.AreEqual(2, hunger.Damage);
        Assert.AreEqual(40f, hunger.MaxFullness, 1e-5f);
    }

    [Test]
    public void ReconfiguringTheBossStartsFromTheScaleYouApplyNext()
    {
        BossHunger boss = NewBoss(40f, 3f, 1);
        boss.ApplyScaling(new BossScale { fullnessMult = 2f, speedMult = 1f, damageBonus = 0 });
        Assert.AreEqual(80f, boss.MaxFullness, 1e-5f);

        boss.ApplyScaling(BossScale.Identity);
        Assert.AreEqual(40f, boss.MaxFullness, 1e-5f);
    }

    // ------------------------------------------------------------------ what the player is told

    [Test]
    public void ItemTabHitCountsMatchThisYearsBoss()
    {
        CalendarDate date = db.Calendar.DateOf(0);
        ItemInfoContext year1 = new ItemInfoContext { db = db, date = date, weather = ClimateModel.SampleDay(date, db.climate, new SeededRng(1)) };
        ItemInfoContext year4 = new ItemInfoContext
        {
            db = db, date = date, weather = year1.weather, bossScale = BossScaling.ForYear(4, db.difficulty)
        };
        ItemData tomato = db.items.Get("tomato");

        int hits1 = int.Parse(System.Text.RegularExpressions.Regex.Match(ItemInfoBuilder.Build(tomato, year1).lines.Find(l => l.label.StartsWith("Versus")).value, @"\d+").Value);
        int hits4 = int.Parse(System.Text.RegularExpressions.Regex.Match(ItemInfoBuilder.Build(tomato, year4).lines.Find(l => l.label.StartsWith("Versus")).value, @"\d+").Value);

        Assert.Greater(hits4, hits1, "the same Tomato needs more hits in Year 4");
    }
}
