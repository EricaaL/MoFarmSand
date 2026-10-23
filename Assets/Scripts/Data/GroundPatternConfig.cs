using UnityEngine;

/// <summary>Look of the triangle-patterned ground texture. <c>MoFarmSand ▸ Build Triangle Ground Texture</c> bakes it into a PNG.</summary>
[CreateAssetMenu(fileName = "GroundPatternConfig", menuName = "MoFarmSand/Ground Pattern Config")]
public class GroundPatternConfig : ScriptableObject
{
    [Tooltip("Triangle colours. Each triangle picks one at random (seeded).")]
    public Color[] palette =
    {
        new Color(0.55f, 0.82f, 0.45f),
        new Color(0.62f, 0.86f, 0.50f),
        new Color(0.48f, 0.76f, 0.42f),
        new Color(0.70f, 0.88f, 0.55f),
        new Color(0.58f, 0.80f, 0.52f),
    };
    [Tooltip("Squares per side; each square is split into two triangles.")]
    [Range(2, 64)] public int cells = 8;
    [Tooltip("Texture width and height in pixels.")]
    public int textureSize = 1024;
    [Tooltip("Random brightness shift applied on top of the palette colour (0 = flat palette).")]
    [Range(0f, 0.2f)] public float brightnessJitter = 0.04f;
    [Tooltip("Same seed gives the same pattern every rebuild.")]
    public int seed = 237;
    [Tooltip("How many times the texture repeats across the ground plane.")]
    [Min(1f)] public float tiling = 6f;

    /// <summary>Raised in the Editor when a value is edited in the Inspector; the editor builder listens and re-bakes the texture.</summary>
    public static event System.Action Edited;

    private void OnValidate()
    {
        Edited?.Invoke();
    }
}
