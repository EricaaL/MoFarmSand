using System;

/// <summary>Pure rule for how deep the snow lies: it builds across the season and creeps up during each day.</summary>
public static class SnowAccumulation
{
    /// <summary>
    /// Snow cover 0..1. <paramref name="seasonProgress01"/> is where today sits in the season;
    /// <paramref name="dayShare"/> is how much of the season's build-up one day covers, which the
    /// day's own progress (0 = morning, 1 = dusk) fills in so snow visibly grows while you play.
    /// </summary>
    public static float Amount(float start01, float end01, float seasonProgress01, float dayProgress01, float dayShare)
    {
        float p = Clamp01(seasonProgress01) + Clamp01(dayProgress01) * Math.Max(0f, dayShare);
        float t = Clamp01(p);
        return Clamp01(start01 + (end01 - start01) * t);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
}
