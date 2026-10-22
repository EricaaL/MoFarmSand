using UnityEngine;

/// <summary>Builds a farmland plot the player placed. Runs at the start of each day for saved plots, and on the spot when you place one.</summary>
public static class FarmlandSpawner
{
    /// <summary>Spawns one placed plot at <paramref name="spot"/>, copied from <paramref name="template"/>. Returns null if there is no template.</summary>
    public static FarmTile SpawnAt(Vector3 spot, FarmTile template, Transform parent, FarmConditions conditions)
    {
        if (template == null) return null;
        FarmTile tile = Object.Instantiate(template, spot, template.transform.rotation, parent);
        tile.name = "FarmTile_Placed";
        tile.gameObject.SetActive(true);
        tile.SetFreePlanting(false);   // roguelike runs plant seeds, same as the original plots
        tile.SetConditions(conditions);
        tile.SetWitherFx(template.WitherFx);
        tile.SetPlaced(true);
        return tile;
    }
}
