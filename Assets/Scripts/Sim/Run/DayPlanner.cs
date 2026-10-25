using System.Collections.Generic;
using UnityEngine;

/// <summary>Everything decided about one farm day, derived deterministically from (runSeed, nodeId).</summary>
public class DayPlan
{
    public int nodeId;
    public CalendarDate date;
    public DayWeather weather;
    public NodeTypeData node;
    public DisasterRoll disaster;
    public BossData encounter;
    public List<ShopOffer> offers = new List<ShopOffer>();
    /// <summary>The day skips farming and begins at night with the boss fight.</summary>
    public bool startsAtNight;
    /// <summary>Daylight for the farming phase. 0 = unlimited.</summary>
    public float dayLengthSeconds;
    /// <summary>Time to feed the boss before dawn. 0 = unlimited.</summary>
    public float nightLengthSeconds;
    /// <summary>True on Merchant days: the shelf was refilled for this day.</summary>
    public bool restocked;
    public EventData evt;
    /// <summary>The season-long market swing in force today (inflation / deflation), or inactive.</summary>
    public MarketEventStatus market;
    /// <summary>The factor on every merchant price today: 1 when no market event is active.</summary>
    public float MarketMultiplier => market.Multiplier;
    public int coinReward;
    /// <summary>Product of node growth bonus and owned modifiers.</summary>
    public float growthMultiplier = 1f;
}

public static class DayPlanner
{
    /// <summary>Same inputs always give an equal plan, so resuming a save mid-day shows the same day.</summary>
    public static DayPlan Plan(RunState run, int nodeId, GameDatabase db)
    {
        DayPlan plan = new DayPlan { nodeId = nodeId };
        MapNode mapNode = run.map.Get(nodeId);
        if (mapNode == null) return plan;

        db.nodeTypes.TryGet(mapNode.nodeTypeId, out plan.node);
        plan.date = db.Calendar.DateOf(mapNode.layer);

        plan.dayLengthSeconds = db.run.DayLengthFor(plan.date.season);
        plan.nightLengthSeconds = Mathf.Max(0f, db.run.nightLengthSeconds);

        plan.weather = ClimateModel.SampleDay(plan.date, db.climate, SeededRng.Derive(run.runSeed, nodeId, RngStreams.Weather));

        plan.market = MarketEventRoller.For(run.runSeed, mapNode.layer - plan.date.dayInSeason, mapNode.layer, db.economy != null ? db.economy.marketEvents : null);

        float nodeChance = plan.node != null ? plan.node.disasterChance : 0f;
        plan.disaster = DisasterRoller.Roll(plan.date, nodeChance, db.disasters,
            SeededRng.Derive(run.runSeed, nodeId, RngStreams.Disaster), id => ResistFor(run, db, id));

        if (plan.node != null)
        {
            plan.encounter = plan.node.encounter;
            // Only meaningful when there is a fight to start.
            plan.startsAtNight = plan.node.startsAtNight && plan.encounter != null;
            plan.growthMultiplier = plan.node.growthBonus;

            SeededRng rewardRng = SeededRng.Derive(run.runSeed, nodeId, RngStreams.Rewards);
            plan.coinReward = rewardRng.NextInt(plan.node.coinReward.x, plan.node.coinReward.y + 1);

            // The merchant is always at his booth, but only restocks on Merchant days (fresh seeds plus passive charms).
            // On other days you can only buy what is left on the shelf from last time.
            plan.restocked = plan.node.merchantActive;
            plan.offers = plan.restocked
                ? ShopGenerator.Generate(db, run, plan.date, SeededRng.Derive(run.runSeed, nodeId, RngStreams.Shop), includeModifiers: true)
                : ShopStock.ToOffers(run, db, plan.date);

            if (plan.node.eventPool != null && plan.node.eventPool.All.Count > 0)
            {
                // Season-only events (see EventData.seasons) are left out of the other seasons' pools.
                List<EventData> pool = new List<EventData>();
                foreach (EventData e in plan.node.eventPool.All) if (e != null && e.AllowedIn(plan.date.season)) pool.Add(e);
                if (pool.Count > 0)
                {
                    SeededRng evRng = SeededRng.Derive(run.runSeed, nodeId, RngStreams.Event);
                    plan.evt = pool[evRng.NextInt(0, pool.Count)];
                }
            }
        }

        plan.growthMultiplier *= ModifierGrowth(run, db);
        // A lost boss fight leaves the next season's soil sluggish.
        if (run.growthDebuffSeason >= 0 && plan.date.seasonIndex == run.growthDebuffSeason) plan.growthMultiplier *= run.growthDebuffMult;
        return plan;
    }

    /// <summary>0..1 severity reduction from owned modifiers that list this disaster.</summary>
    public static float ResistFor(RunState run, GameDatabase db, string disasterId)
    {
        if (db.modifiers == null) return 0f;
        float keep = 1f;
        foreach (string id in run.modifierIds)
        {
            if (!db.modifiers.TryGet(id, out RunModifierData m)) continue;
            if (m.resistDisasterIds.Contains(disasterId)) keep *= 1f - Mathf.Clamp01(m.resistAmount);
        }
        return 1f - keep;
    }

    public static float ModifierGrowth(RunState run, GameDatabase db)
    {
        if (db.modifiers == null) return 1f;
        float g = 1f;
        foreach (string id in run.modifierIds)
            if (db.modifiers.TryGet(id, out RunModifierData m)) g *= m.growthMult;
        return g;
    }
}
