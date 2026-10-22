using UnityEngine;

/// <summary>A single placeable farmland plot in the hotbar. Never saved: how many you hold is derived from farmlands owned minus plots placed.</summary>
[CreateAssetMenu(fileName = "FarmlandPlot", menuName = "MoFarmSand/Farmland Plot Item")]
public class FarmlandPlotItem : ItemData
{
    public string itemId = "farmland_plot";
    public string displayName = "Farmland Plot";
    public Sprite icon;
    public Color color = new Color(0.35f, 0.22f, 0.12f);

    public override string Id => itemId;
    public override string DisplayName => displayName;
    public override Sprite Icon => icon;
    public override Color ItemColor => color;
}
