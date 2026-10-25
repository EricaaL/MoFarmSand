using System.Collections.Generic;
using UnityEngine;

/// <summary>Which market event (if any) is in force on a day, and whether it struck that very day.</summary>
public struct MarketEventStatus
{
    public MarketEventData data;
    public bool startedToday;

    public bool Active => data != null;
    /// <summary>The factor on every merchant price: 1 when nothing is active.</summary>
    public float Multiplier => data != null ? data.priceMultiplier : 1f;
}

/// <summary>
/// Pure and deterministic: every day of a season rolls once from (runSeed, layer); the first hit lasts until the season ends.
/// Nothing is saved, so reloading a day can't re-roll it.
/// </summary>
public static class MarketEventRoller
{
    /// <param name="seasonStartLayer">The map layer of the season's first day.</param>
    /// <param name="layer">The map layer of the day being planned.</param>
    public static MarketEventStatus For(int runSeed, int seasonStartLayer, int layer, IReadOnlyList<MarketEventData> events)
    {
        if (events == null || events.Count == 0) return default;
        for (int l = seasonStartLayer; l <= layer; l++)
        {
            MarketEventData hit = Roll(runSeed, l, events);
            if (hit != null) return new MarketEventStatus { data = hit, startedToday = l == layer };
        }
        return default;
    }

    /// <summary>One day's roll: the events' chances stack in list order, so 5% + 5% is 5% inflation, 5% deflation, 90% nothing.</summary>
    public static MarketEventData Roll(int runSeed, int layer, IReadOnlyList<MarketEventData> events)
    {
        float r = SeededRng.Derive(runSeed, layer, RngStreams.MarketEvent).NextFloat();
        float edge = 0f;
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i] == null) continue;
            edge += Mathf.Clamp01(events[i].chancePerDay);
            if (r < edge) return events[i];
        }
        return null;
    }
}
