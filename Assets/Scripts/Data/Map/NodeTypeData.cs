using UnityEngine;

/// <summary>Declarative description of a map node / farm day. RunDirector reads these flags and never switches on a type.</summary>
[CreateAssetMenu(fileName = "Node_New", menuName = "MoFarmSand/Map/Node Type")]
public class NodeTypeData : ScriptableObject, IHasId
{
    public string nodeTypeId;
    public string displayName;
    [Tooltip("Short text drawn inside the node on the map.")]
    public string glyph = "?";
    public Color color = Color.white;
    [Tooltip("Round node art for the route map. Empty = a coloured circle with the glyph.")]
    public Texture2D icon;
    [Tooltip("Name shown beside the icon in the route map legend. Empty = displayName.")]
    public string legendLabel;
    [TextArea] public string description;

    [Header("Day shape")]
    public bool farmingAllowed = true;
    [Tooltip("Night fight for this day. Empty = the bell just ends the day.")]
    public BossData encounter;
    [Tooltip("Skip the daytime entirely: the day starts at night and the boss fight begins at once (needs an encounter).")]
    public bool startsAtNight;
    public bool merchantActive;
    [Range(0f, 1f)] public float disasterChance = 0.15f;
    public bool healOnStart;
    [Tooltip("Crop growth multiplier for the day (e.g. 1.5 at a rest stop).")]
    public float growthBonus = 1f;
    [Tooltip("Random event shown when the day starts. Empty = none.")]
    public EventRegistry eventPool;
    public Vector2Int coinReward = new Vector2Int(5, 10);
    [Tooltip("Finishing this node ends the run (final boss).")]
    public bool endsRun;

    [Header("Losing the fight")]
    [Tooltip("Losing this night does not end the year: you carry on (with at least 1 heart) but next season's crops grow slower.")]
    public bool survivableLoss;
    [Tooltip("Crop growth speed for the whole next season after losing this fight (0.65 = about 54% longer).")]
    [Range(0.2f, 1f)] public float lossGrowthMultiplier = 0.65f;

    [Header("Map placement")]
    public int minLayer;
    [Tooltip("Max times this type may appear along any single path (0 = unlimited).")]
    public int maxPerPath;
    [Tooltip("Never directly after a node of the same type.")]
    public bool noConsecutive;

    public string Id => nodeTypeId;
}
