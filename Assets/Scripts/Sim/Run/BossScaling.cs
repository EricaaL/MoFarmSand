using UnityEngine;

/// <summary>The year's boss modifiers. Applied on top of a BossData's own numbers.</summary>
public struct BossScale
{
    public float fullnessMult;
    public float speedMult;
    public int damageBonus;

    public static BossScale Identity => new BossScale { fullnessMult = 1f, speedMult = 1f, damageBonus = 0 };

    public bool IsIdentity => Mathf.Approximately(fullnessMult, 1f) && Mathf.Approximately(speedMult, 1f) && damageBonus == 0;
}

/// <summary>Pure: how much stronger the bosses are in a given year.</summary>
public static class BossScaling
{
    public static BossScale ForYear(int year, DifficultyConfig cfg)
    {
        if (cfg == null || year <= 1) return BossScale.Identity;
        int years = year - 1;

        return new BossScale
        {
            fullnessMult = Mathf.Min(Mathf.Max(1f, cfg.maxFullnessMult), 1f + cfg.fullnessGrowthPerYear * years),
            speedMult = Mathf.Min(Mathf.Max(1f, cfg.maxSpeedMult), 1f + cfg.speedGrowthPerYear * years),
            damageBonus = cfg.damageEveryYears > 0 ? Mathf.Min(cfg.maxDamageBonus, years / cfg.damageEveryYears) : 0
        };
    }

    /// <summary>One-line description for tooltips, or empty in Year 1.</summary>
    public static string Describe(BossScale scale)
    {
        if (scale.IsIdentity) return string.Empty;
        string text = $"needs {Mathf.RoundToInt((scale.fullnessMult - 1f) * 100f)}% more feeding, moves {Mathf.RoundToInt((scale.speedMult - 1f) * 100f)}% faster";
        if (scale.damageBonus > 0) text += $", hits {scale.damageBonus} heart harder";
        return text;
    }
}
