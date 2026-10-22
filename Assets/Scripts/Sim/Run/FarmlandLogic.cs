using System.Collections.Generic;
using UnityEngine;

/// <summary>Pure rules for buying extra farmland and for tracking the plots the player places.</summary>
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

    // ------------------------------------------------------------------ placing

    /// <summary>Plots still waiting in the hotbar: owned (farmlands x plots each) minus the ones already placed.</summary>
    public static int UnplacedPlots(int farmlands, int plotsPerFarmland, int placed)
        => Mathf.Max(0, farmlands * Mathf.Max(0, plotsPerFarmland) - Mathf.Max(0, placed));

    /// <summary>Snaps a world point onto the starting field's grid (same spacing), keeping the field's height.</summary>
    public static Vector3 SnapToField(Vector3 point, IReadOnlyList<Vector3> baseTiles)
    {
        if (baseTiles == null || baseTiles.Count == 0) return point;
        List<float> xs = Distinct(baseTiles, v => v.x);
        List<float> zs = Distinct(baseTiles, v => v.z);
        float spacing = xs.Count > 1 ? (xs[xs.Count - 1] - xs[0]) / (xs.Count - 1) : 2.2f;
        float zStep = zs.Count > 1 ? (zs[zs.Count - 1] - zs[0]) / (zs.Count - 1) : spacing;
        float x = xs[0] + Mathf.Round((point.x - xs[0]) / spacing) * spacing;
        float z = zs[0] + Mathf.Round((point.z - zs[0]) / zStep) * zStep;
        return new Vector3(x, baseTiles[0].y, z);
    }

    /// <summary>True when no occupied point is closer than <paramref name="minSpacing"/> (flat distance).</summary>
    public static bool IsClear(Vector3 spot, IReadOnlyList<Vector3> occupied, float minSpacing)
    {
        if (occupied == null) return true;
        foreach (Vector3 o in occupied)
        {
            Vector3 d = o - spot; d.y = 0f;
            if (d.magnitude < minSpacing) return false;
        }
        return true;
    }

    /// <summary>Records a placed plot. Refuses when every plot you own is already placed.</summary>
    public static bool TryPlace(MetaProgress meta, Vector3 spot, int ownedPlots)
    {
        if (meta == null || meta.placedPlots.Count >= ownedPlots) return false;
        meta.placedPlots.Add(spot);
        return true;
    }

    /// <summary>Forgets the placed plot at <paramref name="spot"/> (within 0.1 m), so it returns to the hotbar.</summary>
    public static bool TryPickUp(MetaProgress meta, Vector3 spot)
    {
        if (meta == null) return false;
        for (int i = 0; i < meta.placedPlots.Count; i++)
        {
            Vector3 d = meta.placedPlots[i] - spot; d.y = 0f;
            if (d.magnitude > 0.1f) continue;
            meta.placedPlots.RemoveAt(i);
            return true;
        }
        return false;
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
