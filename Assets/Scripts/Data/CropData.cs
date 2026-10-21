using System;
using System.Collections.Generic;
using UnityEngine;

public enum CropType
{
    Projectile,
    Support,
    /// <summary>Fired like a gun: hold to auto-fire, one harvested crop = <see cref="CropData.ammoPerCrop"/> shots.</summary>
    Shooter
}

[Serializable]
public struct SeasonAffinity
{
    public SeasonData season;
    public float multiplier;
}

/// <summary>How well a crop grows in given weather. Defaults make unconfigured crops grow at a moderate rate.</summary>
[Serializable]
public struct CropClimate
{
    public float idealTemp;
    public float tempTolerance;
    [Range(0f, 1f)] public float idealMoisture;
    public float moistureTolerance;
    public List<SeasonAffinity> seasonAffinity;

    public static CropClimate Default => new CropClimate
    {
        idealTemp = 20f, tempTolerance = 16f, idealMoisture = 0.4f, moistureTolerance = 0.7f,
        seasonAffinity = new List<SeasonAffinity>()
    };
}

[CreateAssetMenu(fileName = "Crop_New", menuName = "MoFarmSand/Crop Data")]
public class CropData : ItemData
{
    public string cropId;
    public string displayName;
    public CropType type;
    [Tooltip("Seconds from planting to harvestable.")]
    public float growTime = 6f;
    [Tooltip("Fullness added to the boss on hit (Projectile crops).")]
    public float fullnessValue = 20f;
    [Tooltip("Bark Shield charges granted when eaten (Support crops).")]
    public int shieldCharges = 1;
    public int harvestYield = 1;

    [Header("Shooter (peas, corn)")]
    [Tooltip("Shots in one harvested crop. For Shooter crops fullnessValue is per bullet.")]
    public int ammoPerCrop = 10;
    [Tooltip("Seconds between shots while the button is held.")]
    public float fireInterval = 0.15f;
    [Tooltip("Straight-line bullet speed (m/s).")]
    public float bulletSpeed = 22f;

    [Header("Scare (garlic)")]
    [Tooltip("If above 0, a hit makes the boss run away for this many seconds.")]
    public float scareSeconds = 0f;

    [Header("Visuals")]
    public GameObject projectilePrefab;
    public Sprite icon;
    public Color color = Color.white;
    [Tooltip("Growth models in order: sprout, growing, ripe (shown when harvestable). Leave empty to use the tinted sphere.")]
    public GameObject[] growthStages = new GameObject[0];
    [Tooltip("Uniform scale applied to the growth stage models on the plot.")]
    public float growthStageScale = 1f;

    [Header("Climate")]
    public CropClimate climate = CropClimate.Default;

    public override string Id => cropId;
    public override string DisplayName => displayName;
    public override Sprite Icon => icon;
    public override Color ItemColor => color;

    /// <summary>Stage model index for growth progress (0..1); the last stage is reserved for harvestable.</summary>
    public int StageIndexFor(float progress01, bool harvestable)
    {
        int count = growthStages != null ? growthStages.Length : 0;
        if (count <= 1) return 0;
        if (harvestable) return count - 1;
        int growing = count - 1;
        return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress01) * growing), 0, growing - 1);
    }

    private void OnValidate()
    {
        growthStageScale = Mathf.Max(0.01f, growthStageScale);
        growTime = Mathf.Max(0f, growTime);
        fullnessValue = Mathf.Max(0f, fullnessValue);
        shieldCharges = Mathf.Max(0, shieldCharges);
        harvestYield = Mathf.Max(1, harvestYield);
        ammoPerCrop = Mathf.Max(1, ammoPerCrop);
        fireInterval = Mathf.Max(0.02f, fireInterval);
        bulletSpeed = Mathf.Max(1f, bulletSpeed);
        scareSeconds = Mathf.Max(0f, scareSeconds);
    }
}
