using System.Collections.Generic;
using UnityEngine;

/// <summary>Presentation layer for the field: a dirt puff on planting, a sparkle while a crop is ripe, and a leaf burst on harvest.</summary>
public class FarmFx : MonoBehaviour
{
    [SerializeField] private GameObject dirtPuff;
    [SerializeField] private GameObject ripeSparkle;
    [SerializeField] private GameObject harvestLeaves;
    [SerializeField] private float sparkleHeight = 0.35f;

    private readonly Dictionary<FarmTile, GameObject> sparkles = new Dictionary<FarmTile, GameObject>();

    private void OnEnable()
    {
        FarmTile.Planted += HandlePlanted;
        FarmTile.Ripened += HandleRipened;
        FarmTile.Harvested += HandleHarvested;
        FarmTile.Withered += HandleWithered;
    }

    private void OnDisable()
    {
        FarmTile.Planted -= HandlePlanted;
        FarmTile.Ripened -= HandleRipened;
        FarmTile.Harvested -= HandleHarvested;
        FarmTile.Withered -= HandleWithered;
        sparkles.Clear();
    }

    private void HandlePlanted(FarmTile tile, GameObject who)
    {
        Burst(dirtPuff, tile.Position + Vector3.up * 0.15f, null);
    }

    private void HandleRipened(FarmTile tile)
    {
        if (ripeSparkle == null || tile == null) return;
        StopSparkle(tile);
        GameObject fx = Instantiate(ripeSparkle, tile.Position + Vector3.up * sparkleHeight, Quaternion.identity, tile.transform);
        sparkles[tile] = fx;
    }

    private void HandleHarvested(FarmTile tile, CropData crop)
    {
        StopSparkle(tile);
        Burst(harvestLeaves, tile.Position + Vector3.up * 0.4f, crop != null ? (Color?)crop.color : null);
    }

    private void HandleWithered(FarmTile tile) { StopSparkle(tile); }

    private void StopSparkle(FarmTile tile)
    {
        if (tile == null || !sparkles.TryGetValue(tile, out GameObject fx)) return;
        sparkles.Remove(tile);
        if (fx != null) Destroy(fx);
    }

    private static void Burst(GameObject prefab, Vector3 position, Color? tint)
    {
        if (prefab == null) return;
        GameObject fx = Instantiate(prefab, position, Quaternion.identity);
        if (tint.HasValue)
        {
            ParticleSystem ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ParticleSystem.MainModule main = ps.main;
                main.startColor = tint.Value;
            }
        }
        Destroy(fx, 2.5f);
    }
}
