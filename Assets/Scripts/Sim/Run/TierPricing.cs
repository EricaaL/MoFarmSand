using UnityEngine;

/// <summary>The one formula for "each tier you own makes the next cost more". Farmland and the stockroom both use it.</summary>
public static class TierPricing
{
    /// <summary>Largest value returned, only so absurd tiers can't overflow an int. Prices otherwise rise forever.</summary>
    public const int MaxRepresentable = 1000000000;

    public static int Price(int basePrice, float growth, int roundTo, int owned)
    {
        double raw = basePrice * System.Math.Pow(Mathf.Max(1f, growth), Mathf.Max(0, owned));
        raw = System.Math.Min(raw, MaxRepresentable);
        int step = Mathf.Max(1, roundTo);
        return Mathf.Max(step, (int)(System.Math.Round(raw / step) * step));
    }

    /// <summary>Hard ceiling for "unlimited" upgrades so an edited save can't ask for millions of plots.</summary>
    public const int SanityCap = 100;

    /// <summary>True when a tier cap exists (<paramref name="max"/> &gt; 0) and has been reached. 0 means unlimited.</summary>
    public static bool AtCap(int owned, int max) => max > 0 && owned >= max;

    /// <summary>Clamps an owned count to the cap; with no cap (0) it uses <see cref="SanityCap"/>.</summary>
    public static int ClampOwned(int owned, int max) => Mathf.Clamp(owned, 0, max > 0 ? max : SanityCap);
}
