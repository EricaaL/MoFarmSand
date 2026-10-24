using UnityEngine;

/// <summary>
/// Gives a <see cref="TextMesh"/> a depth-tested material (MoFarmSand/DepthText) the font atlas,
/// so geometry in front of the text hides it instead of the text drawing over everything.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(TextMesh))]
public class DepthTextMesh : MonoBehaviour
{
    private void OnEnable() => Apply();

    private void Apply()
    {
        TextMesh tm = GetComponent<TextMesh>();
        MeshRenderer r = GetComponent<MeshRenderer>();
        if (tm == null || tm.font == null || r == null) return;
        Texture atlas = tm.font.material != null ? tm.font.material.mainTexture : null;
        if (atlas == null) return;
        var block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetTexture("_MainTex", atlas);
        r.SetPropertyBlock(block);
    }

    // Dynamic font atlases are rebuilt as glyphs are added; refresh before drawing.
    private void LateUpdate() => Apply();
}
