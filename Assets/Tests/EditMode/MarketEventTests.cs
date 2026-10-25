using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class MarketEventTests
{
    private MarketEventData inflation, deflation;
    private List<MarketEventData> events;

    [SetUp]
    public void SetUp()
    {
        inflation = Make("inflation", 1.2f, 0.05f);
        deflation = Make("deflation", 0.8f, 0.05f);
        events = new List<MarketEventData> { inflation, deflation };
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(inflation);
        Object.DestroyImmediate(deflation);
    }

    private static MarketEventData Make(string id, float mult, float chance)
    {
        MarketEventData d = ScriptableObject.CreateInstance<MarketEventData>();
        d.eventId = id; d.displayName = id; d.priceMultiplier = mult; d.chancePerDay = chance;
        return d;
    }

    [Test]
    public void NoEventsMeansNoSwing()
    {
        Assert.IsFalse(MarketEventRoller.For(1, 0, 5, null).Active);
        Assert.IsFalse(MarketEventRoller.For(1, 0, 5, new List<MarketEventData>()).Active);
        Assert.AreEqual(1f, MarketEventRoller.For(1, 0, 5, null).Multiplier);
    }

    [Test]
    public void SameSeedAndDayAlwaysGiveTheSameAnswer()
    {
        for (int seed = 0; seed < 50; seed++)
            for (int layer = 0; layer < 12; layer++)
                Assert.AreEqual(MarketEventRoller.For(seed, 0, layer, events).data, MarketEventRoller.For(seed, 0, layer, events).data);
    }

    [Test]
    public void AnEventLastsFromItsDayToTheEndOfTheSeason()
    {
        int checkedSeasons = 0;
        for (int seed = 0; seed < 400 && checkedSeasons < 10; seed++)
        {
            int hitLayer = -1;
            for (int layer = 0; layer < 6; layer++)
                if (MarketEventRoller.Roll(seed, layer, events) != null) { hitLayer = layer; break; }
            if (hitLayer < 0) continue;
            checkedSeasons++;

            MarketEventData first = MarketEventRoller.Roll(seed, hitLayer, events);
            for (int layer = 0; layer < 6; layer++)
            {
                MarketEventStatus s = MarketEventRoller.For(seed, 0, layer, events);
                if (layer < hitLayer) Assert.IsFalse(s.Active, $"seed {seed} day {layer} is before the swing");
                else
                {
                    Assert.AreSame(first, s.data, $"seed {seed} day {layer}");
                    Assert.AreEqual(layer == hitLayer, s.startedToday);
                }
            }
        }
        Assert.Greater(checkedSeasons, 0, "no season rolled an event in 400 seeds");
    }

    [Test]
    public void EachEventStrikesAboutFivePercentOfDays()
    {
        int inflations = 0, deflations = 0, days = 20000;
        for (int layer = 0; layer < days; layer++)
        {
            MarketEventData hit = MarketEventRoller.Roll(7, layer, events);
            if (hit == inflation) inflations++;
            else if (hit == deflation) deflations++;
        }
        Assert.That(inflations / (float)days, Is.InRange(0.04f, 0.06f));
        Assert.That(deflations / (float)days, Is.InRange(0.04f, 0.06f));
    }

    [Test]
    public void PricesFollowTheMultiplier()
    {
        PriceTable.PriceRow row = new PriceTable.PriceRow { baseSell = 10, seedCost = 10 };
        Assert.AreEqual(12, EconomyModel.SellPrice(row, null, 1f, inflation.priceMultiplier));
        Assert.AreEqual(8, EconomyModel.SellPrice(row, null, 1f, deflation.priceMultiplier));
    }
}
