using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds the extra farmland plots you've bought. Runs at the start of each day (the scene resets between days).</summary>
public static class FarmlandSpawner
{
    /// <summary>
    /// Spawns <paramref name="extraFarmlands"/> new columns of plots copied from <paramref name="template"/>, laid out
    /// from the existing <paramref name="existing"/> tiles. Returns the new tiles.
    /// </summary>
    public static List<FarmTile> Spawn(int extraFarmlands, FarmTile template, IReadOnlyList<FarmTile> existing, Transform parent, FarmConditions conditions, int maxColumnsPerSide = 4, IReadOnlyList<Vector3> keepClear = null)
    {
        List<FarmTile> spawned = new List<FarmTile>();
        if (extraFarmlands <= 0 || template == null || existing == null || existing.Count == 0) return spawned;

        List<Vector3> basePositions = new List<Vector3>(existing.Count);
        foreach (FarmTile t in existing) basePositions.Add(t.transform.position);

        List<Vector3> positions = FarmlandLogic.PositionsFor(extraFarmlands, basePositions, maxColumnsPerSide, keepClear);
        for (int i = 0; i < positions.Count; i++)
        {
            FarmTile tile = Object.Instantiate(template, positions[i], template.transform.rotation, parent);
            tile.name = $"FarmTile_Extra_{i + 1}";
            tile.gameObject.SetActive(true);
            tile.SetFreePlanting(false);   // roguelike runs plant seeds, same as the original plots
            tile.SetConditions(conditions);
            tile.SetWitherFx(template.WitherFx);
            spawned.Add(tile);
        }
        return spawned;
    }
}
