using System.Collections.Generic;
using NUnit.Framework;

public class SeededRngTests
{
    [Test]
    public void SameSeedGivesSameSequence()
    {
        SeededRng a = new SeededRng(42), b = new SeededRng(42);
        for (int i = 0; i < 50; i++) Assert.AreEqual(a.NextULong(), b.NextULong());
    }

    [Test]
    public void DeriveStreamsAreIndependentButStable()
    {
        Assert.AreEqual(SeededRng.Derive(7, 3, 1).NextULong(), SeededRng.Derive(7, 3, 1).NextULong());
        Assert.AreNotEqual(SeededRng.Derive(7, 3, 1).NextULong(), SeededRng.Derive(7, 3, 2).NextULong(), "stream changes the sequence");
        Assert.AreNotEqual(SeededRng.Derive(7, 3, 1).NextULong(), SeededRng.Derive(7, 4, 1).NextULong(), "salt changes the sequence");
        Assert.AreNotEqual(SeededRng.Derive(7, 3, 1).NextULong(), SeededRng.Derive(8, 3, 1).NextULong(), "seed changes the sequence");
    }

    [Test]
    public void RangesStayInBounds()
    {
        SeededRng r = new SeededRng(1);
        for (int i = 0; i < 5000; i++)
        {
            int n = r.NextInt(2, 9);
            Assert.That(n, Is.InRange(2, 8));
            float f = r.NextFloat();
            Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            Assert.That(r.NextSigned(), Is.InRange(-1f, 1f));
        }
        Assert.AreEqual(5, r.NextInt(5, 5));
    }

    [Test]
    public void WeightedPickNeverChoosesZeroWeightAndTracksWeights()
    {
        SeededRng r = new SeededRng(99);
        int[] weights = { 0, 1, 3 };
        List<int> items = new List<int> { 0, 1, 2 };
        int[] counts = new int[3];
        const int draws = 20000;
        for (int i = 0; i < draws; i++) counts[r.PickWeighted(items, x => weights[x])]++;

        Assert.AreEqual(0, counts[0]);
        Assert.That(counts[2] / (float)draws, Is.EqualTo(0.75f).Within(0.03f));
    }

    [Test]
    public void WeightedPickReturnsDefaultWhenNothingQualifies()
    {
        Assert.AreEqual(0, new SeededRng(1).PickWeighted(new List<int> { 5, 6 }, x => 0f));
    }

    [Test]
    public void ModifierStackMultipliesAndRemovesBySource()
    {
        ModifierStack m = new ModifierStack();
        Assert.AreEqual(1f, m.Product);
        m.Set("a", 0.5f); m.Set("b", 2f); m.Set("a", 0.25f);
        Assert.AreEqual(0.5f, m.Product, 1e-5f);
        Assert.IsTrue(m.Remove("a"));
        Assert.AreEqual(2f, m.Product, 1e-5f);
        Assert.IsFalse(m.Remove("missing"));
    }
}
