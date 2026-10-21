using System.Collections.Generic;
using UnityEngine;

/// <summary>Pure rules for buying extra farmland and where the new plots go.</summary>
public static class FarmlandLogic
{
    // ------------------------------------------------------------------ buying

    /// <summary>Price of the next farmland when you already own <paramref name="owned"/>. Grows with every one you buy.</summary>
    public static int PriceFor(int owned, FarmlandConfig cfg)
    {
        return cfg == null ? 0 : TierPricing.Price(cfg.basePrice, cfg.priceGrowth, cfg.roundTo, owned);
    }

    public static bool IsMaxed(int owned, FarmlandConfig cfg) => cfg == null || TierPricing.AtCap(owned, cfg.maxFarmlands);

    /// <summary>Spends vault coins on the next farmland. Returns false if maxed or you can't afford it.</summary>
    public static bool TryBuy(MetaProgress meta, FarmlandConfig cfg)
    {
        if (meta == null || IsMaxed(meta.farmlands, cfg)) return false;
        int price = PriceFor(meta.farmlands, cfg);
        if (meta.vaultCoins < price) return false;
        meta.vaultCoins -= price;
        meta.farmlands++;
        return true;
    }

    // ------------------------------------------------------------------ layout

    /// <summary>
    /// Plot positions for <paramref name="extraFarmlands"/> new columns, derived from the existing field so it works
    /// with any grid. Columns go to the right first, then the left, out to <paramref name="maxColumnsPerSide"/> each way;
    /// after that, new bands of rows start behind the field and fill centre-out, each repeating the field's rows.
    /// A column that would sit within <paramref name="clearRadius"/> of a <paramref name="keepClear"/> point (merchant, bell)
    /// is skipped, so every farmland you buy still adds a full column.
    /// </summary>
    public static List<Vector3> PositionsFor(int extraFarmlands, IReadOnlyList<Vector3> baseTiles, int maxColumnsPerSide = 4,
        IReadOnlyList<Vector3> keepClear = null, float clearRadius = 2.8f)
    {
        List<Vector3> result = new List<Vector3>();
        if (extraFarmlands <= 0 || baseTiles == null || baseTiles.Count == 0) return result;

        List<float> xs = Distinct(baseTiles, v => v.x);
        List<float> zs = Distinct(baseTiles, v => v.z);
        float y = baseTiles[0].y;

        float spacing = xs.Count > 1 ? (xs[xs.Count - 1] - xs[0]) / (xs.Count - 1) : 2.2f;
        float zStep = zs.Count > 1 ? (zs[zs.Count - 1] - zs[0]) / (zs.Count - 1) : spacing;
        float zSpan = zs[zs.Count - 1] - zs[0];
        float centerX = (xs[0] + xs[xs.Count - 1]) * 0.5f;
        float halfColumns = (xs.Count - 1) * 0.5f;

        int perSide = Mathf.Max(1, maxColumnsPerSide);
        int beside = perSide * 2;                 // extra columns that fit next to the starting field
        int bandColumns = xs.Count + beside;      // columns in every later band (same width as the field + side columns)

        int placed = 0;
        for (int candidate = 0; placed < extraFarmlands && candidate < 4000; candidate++)
        {
            float x, zShift;
            if (candidate < beside)
            {
                float side = candidate % 2 == 0 ? 1f : -1f;
                int ring = candidate / 2 + 1;
                x = centerX + side * (halfColumns + ring) * spacing;
                zShift = 0f;
            }
            else
            {
                int later = candidate - beside;
                int band = 1 + later / bandColumns;
                x = centerX + CenteredOffset(later % bandColumns, bandColumns) * spacing;
                zShift = band * (zSpan + zStep);
            }

            List<Vector3> column = new List<Vector3>(zs.Count);
            foreach (float z in zs) column.Add(new Vector3(x, y, z + zShift));
            if (Blocked(column, keepClear, clearRadius)) continue;

            result.AddRange(column);
            placed++;
        }
        return result;
    }

    private static bool Blocked(List<Vector3> column, IReadOnlyList<Vector3> keepClear, float radius)
    {
        if (keepClear == null) return false;
        foreach (Vector3 plot in column)
            foreach (Vector3 point in keepClear)
            {
                Vector3 d = plot - point; d.y = 0f;
                if (d.magnitude < radius) return true;
            }
        return false;
    }

    /// <summary>The m-th column offset (in column widths) counting out from the centre, right first. Works for odd and even widths.</summary>
    private static float CenteredOffset(int m, int columns)
    {
        if (columns % 2 == 1)
        {
            if (m == 0) return 0f;
            int k = (m + 1) / 2;
            return m % 2 == 1 ? k : -k;
        }
        int ring = m / 2;
        return m % 2 == 0 ? ring + 0.5f : -(ring + 0.5f);
    }

    /// <summary>Number of plots a farmland adds, for display ("+3 plots").</summary>
    public static int PlotsPerFarmland(IReadOnlyList<Vector3> baseTiles)
        => baseTiles == null || baseTiles.Count == 0 ? 0 : Distinct(baseTiles, v => v.z).Count;

    private static List<float> Distinct(IReadOnlyList<Vector3> points, System.Func<Vector3, float> axis)
    {
        List<float> values = new List<float>();
        foreach (Vector3 p in points)
        {
            float v = axis(p);
            if (!values.Exists(e => Mathf.Abs(e - v) < 0.05f)) values.Add(v);
        }
        values.Sort();
        return values;
    }
}
