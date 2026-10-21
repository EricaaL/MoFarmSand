using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameplayRulesTests
{
    private GameObject go;

    [TearDown]
    public void TearDown()
    {
        if (go != null) Object.DestroyImmediate(go);
    }

    // Awake does not run on AddComponent in EditMode, so call it explicitly.
    private static void CallAwake(Component c)
    {
        c.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(c, null);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(f, "Missing field " + name);
        f.SetValue(target, value);
    }

    private static CropData MakeCrop(CropType type, float fullness = 20f)
    {
        CropData c = ScriptableObject.CreateInstance<CropData>();
        c.cropId = type.ToString();
        c.type = type;
        c.fullnessValue = fullness;
        return c;
    }

    [Test]
    public void Ballistics_LaunchVelocityLandsOnTarget()
    {
        Vector3 p0 = new Vector3(0f, 1f, 0f);
        Vector3 target = new Vector3(5f, 0.1f, 3f);
        Vector3 g = new Vector3(0f, -9.81f, 0f);
        Vector3 v0 = Ballistics.SolveLaunchVelocity(p0, target, 1.2f, g);
        Vector3 landed = Ballistics.Evaluate(p0, v0, g, 1.2f);
        Assert.AreEqual(0f, Vector3.Distance(landed, target), 1e-4f);
    }

    [Test]
    public void PlayerHealth_ShieldAbsorbsExactlyOneHit()
    {
        go = new GameObject("p");
        PlayerHealth h = go.AddComponent<PlayerHealth>();
        CallAwake(h);
        h.GrantShield();
        h.TakeDamage(1);
        Assert.AreEqual(3, h.Hp);
        Assert.IsFalse(h.HasShield);
        h.TakeDamage(1);
        Assert.AreEqual(2, h.Hp);
    }

    [Test]
    public void PlayerHealth_DiedFiresOnce()
    {
        go = new GameObject("p");
        PlayerHealth h = go.AddComponent<PlayerHealth>();
        CallAwake(h);
        int died = 0;
        h.OnDied += () => died++;
        for (int i = 0; i < 6; i++) h.TakeDamage(1);
        Assert.AreEqual(0, h.Hp);
        Assert.AreEqual(1, died);
    }

    [Test]
    public void PlayerInventory_StacksConsumesAndCounts()
    {
        go = new GameObject("p");
        PlayerInventory inv = go.AddComponent<PlayerInventory>();
        CallAwake(inv);
        CropData tomato = MakeCrop(CropType.Projectile);
        CropData turnip = MakeCrop(CropType.Support);

        Assert.IsTrue(inv.AddItem(tomato, 2));
        Assert.IsTrue(inv.AddItem(tomato, 3));
        Assert.AreEqual(5, inv.GetSlot(0).count, "same crop stacks into one slot");
        Assert.IsFalse(inv.TryConsume(turnip, 1), "wrong crop in active slot");
        Assert.IsTrue(inv.TryConsume(tomato, 5));
        Assert.IsFalse(inv.TryConsume(tomato, 1), "empty slot");
        Assert.AreEqual(0, inv.TotalCount());

        Object.DestroyImmediate(tomato);
        Object.DestroyImmediate(turnip);
    }

    [Test]
    public void BossHunger_FiveFeedsSatisfyOnce()
    {
        go = new GameObject("boss");
        BossHunger hunger = go.AddComponent<BossHunger>();
        BossData data = ScriptableObject.CreateInstance<BossData>();
        data.maxFullness = 100f;
        SetField(hunger, "data", data);
        CallAwake(hunger);

        int satisfied = 0;
        hunger.OnBossSatisfied += () => satisfied++;
        for (int i = 0; i < 4; i++) hunger.Feed(20f);
        Assert.AreEqual(0.8f, hunger.Fullness01, 1e-4f);
        Assert.AreEqual(0, satisfied);
        hunger.Feed(20f);
        hunger.Feed(20f);   // already satisfied: ignored
        Assert.AreEqual(1f, hunger.Fullness01, 1e-4f);
        Assert.AreEqual(1, satisfied);

        Object.DestroyImmediate(data);
    }

    [Test]
    public void BossHunger_EvolvesThenSatisfies()
    {
        go = new GameObject("boss");
        BossHunger hunger = go.AddComponent<BossHunger>();
        BossData butterfly = ScriptableObject.CreateInstance<BossData>();
        butterfly.maxFullness = 60f;
        BossData caterpillar = ScriptableObject.CreateInstance<BossData>();
        caterpillar.maxFullness = 100f;
        caterpillar.nextStage = butterfly;
        SetField(hunger, "data", caterpillar);
        SetField(hunger, "evolveGraceSeconds", 0f);
        CallAwake(hunger);

        int evolved = 0, satisfied = 0;
        hunger.OnBossEvolved += _ => evolved++;
        hunger.OnBossSatisfied += () => satisfied++;

        hunger.Feed(100f);
        Assert.AreEqual(1, evolved);
        Assert.AreEqual(0, satisfied);
        Assert.IsFalse(hunger.IsSatisfied);
        Assert.AreSame(butterfly, hunger.Data);
        Assert.AreEqual(0f, hunger.Fullness01, 1e-4f);

        hunger.Feed(60f);
        Assert.AreEqual(1, evolved);
        Assert.AreEqual(1, satisfied);

        Object.DestroyImmediate(caterpillar);
        Object.DestroyImmediate(butterfly);
    }
}
