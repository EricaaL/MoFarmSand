using UnityEngine;

/// <summary>A seed packet. Planting consumes one and grows <see cref="grows"/>. Single owner of seed-to-crop.</summary>
[CreateAssetMenu(fileName = "Seed_New", menuName = "MoFarmSand/Seed Data")]
public class SeedData : ItemData
{
    public string seedId;
    public string displayName;
    public CropData grows;
    public Sprite icon;
    [Tooltip("Packet tint; defaults to the crop color when alpha is 0.")]
    public Color tint = new Color(1f, 1f, 1f, 0f);

    public override string Id => seedId;
    public override string DisplayName => string.IsNullOrEmpty(displayName) && grows != null ? grows.displayName + " Seeds" : displayName;
    public override Sprite Icon => icon != null ? icon : (grows != null ? grows.icon : null);
    public override Color ItemColor => tint.a > 0f ? tint : (grows != null ? grows.color : Color.white);
}
