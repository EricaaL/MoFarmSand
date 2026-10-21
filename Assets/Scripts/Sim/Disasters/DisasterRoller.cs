using System;
using System.Collections.Generic;
using UnityEngine;

public static class DisasterRoller
{
    /// <summary>
    /// Deterministic roll. Always consumes the same number of rng values so later decisions on the
    /// same stream stay stable. <paramref name="resistFor"/> returns 0..1 severity reduction for a disaster id.
    /// </summary>
    public static DisasterRoll Roll(CalendarDate date, float nodeChance, DisasterRegistry registry,
        SeededRng rng, Func<string, float> resistFor = null)
    {
        float chanceRoll = rng.NextFloat();
        float pickRoll = rng.NextFloat();
        float severityRoll = rng.NextFloat();

        if (registry == null || date.season == null) return DisasterRoll.None;

        bool forced = nodeChance >= 1f;
        float chance = Mathf.Clamp01(nodeChance + (nodeChance > 0f ? date.season.disasterChanceBonus : 0f));
        if (!forced && chanceRoll >= chance) return DisasterRoll.None;

        List<DisasterData> candidates = new List<DisasterData>();
        IReadOnlyList<DisasterData> all = registry.All;
        for (int i = 0; i < all.Count; i++)
        {
            DisasterData d = all[i];
            if (d != null && date.dayIndex >= d.minDay && d.WeightFor(date.season) > 0f) candidates.Add(d);
        }
        if (candidates.Count == 0) return DisasterRoll.None;

        float total = 0f;
        for (int i = 0; i < candidates.Count; i++) total += candidates[i].WeightFor(date.season);
        float acc = pickRoll * total;
        DisasterData chosen = candidates[candidates.Count - 1];
        for (int i = 0; i < candidates.Count; i++)
        {
            float w = candidates[i].WeightFor(date.season);
            if (acc < w) { chosen = candidates[i]; break; }
            acc -= w;
        }

        float severity = Mathf.Lerp(chosen.severityRange.x, chosen.severityRange.y, severityRoll);
        float resist = resistFor != null ? Mathf.Clamp01(resistFor(chosen.Id)) : 0f;
        severity = Mathf.Clamp01(severity * (1f - resist));
        return new DisasterRoll { disasterId = chosen.Id, severity01 = severity };
    }
}
