using UnityEngine;

/// <summary>One in-play tip card. The id must be one of <see cref="TipIds"/>; the code raises it when its moment comes.</summary>
[CreateAssetMenu(fileName = "Tip_New", menuName = "MoFarmSand/Tip")]
public class TipData : ScriptableObject, IHasId
{
    public string tipId;
    public string title = "Tip";
    [TextArea(2, 5)] public string body;

    public string Id => tipId;
}
