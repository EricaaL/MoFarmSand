using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Single owner of the particle look: every FX is a small tumbling triangle shard, to match the faceted floor.
/// Rain and the autumn leaf meshes keep their own look.
/// </summary>
public static class TriangleFxBuilder
{
    private const string MeshPath = "Assets/Art/Meshes/FX_TriangleShard.asset";
    private const string FxDir = "Assets/Prefabs/Systems/FX";

    private static readonly string[] ShardFx =
    {
        "FX_GarlicPuff", "FX_Snow", "FX_SeasonSnow", "FX_Heat", "FX_Locusts", "FX_Spores", "FX_WitherPuff",
        "FX_StrikeTelegraph", "FX_StrikeBolt", "FX_Flood", "FX_DirtPuff", "FX_RipeSparkle", "FX_HarvestLeaves", "FX_SweatDrops",
    };

    private static readonly string[] ShardPrefabsOutsideFxDir = { "Assets/Prefabs/Splat_FX.prefab", "Assets/Prefabs/Burst_FX.prefab" };

    /// <summary>A double-sided equilateral triangle, about one unit across so existing startSize values keep their scale.</summary>
    internal static Mesh ShardMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = "FX_TriangleShard" };

        const float r = 0.75f;   // 1.5x the one-unit billboard
        Vector3 a = new Vector3(0f, r, 0f);
        Vector3 b = new Vector3(-r * 0.866f, -r * 0.5f, 0f);
        Vector3 c = new Vector3(r * 0.866f, -r * 0.5f, 0f);
        mesh.Clear();
        mesh.vertices = new[] { a, b, c, a, c, b };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward };
        mesh.uv = new[] { new Vector2(0.5f, 1f), Vector2.zero, Vector2.right, new Vector2(0.5f, 1f), Vector2.right, Vector2.zero };
        mesh.triangles = new[] { 0, 2, 1, 3, 5, 4 };
        mesh.RecalculateBounds();
        if (isNew)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/Meshes")) AssetDatabase.CreateFolder("Assets/Art", "Meshes");
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    /// <summary>Turns a particle system's quads into randomly rotated, tumbling triangles.</summary>
    internal static void MakeShards(ParticleSystem ps)
    {
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.SetMeshes(new[] { ShardMesh() });
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ParticleSystem.MainModule main = ps.main;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
        rot.enabled = true; rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
        rot.y = new ParticleSystem.MinMaxCurve(-3f, 3f);
        rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
    }

    [MenuItem("MoFarmSand/Make FX Triangles")]
    public static void MakeFxTriangles()
    {
        List<string> paths = new List<string>();
        foreach (string name in ShardFx) paths.Add($"{FxDir}/{name}.prefab");
        paths.AddRange(ShardPrefabsOutsideFxDir);

        int changed = 0;
        foreach (string path in paths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[TriangleFxBuilder] {path} not found; skipped."); continue; }
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ParticleSystem ps = root.GetComponent<ParticleSystem>();
                if (ps == null) { Debug.LogWarning($"[TriangleFxBuilder] {path} has no ParticleSystem; skipped."); continue; }
                MakeShards(ps);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        // The splat is recoloured per crop at runtime (FxTint), so its material must be white, not tomato red.
        Material splat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/SplatFX.mat");
        if (splat != null && splat.color != Color.white)
        {
            splat.color = Color.white;
            if (splat.HasProperty("_BaseColor")) splat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(splat);
        }
        GameObject splatRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Splat_FX.prefab");
        try
        {
            ParticleSystem splatPs = splatRoot.GetComponent<ParticleSystem>();
            if (splatPs != null)
            {
                ParticleSystem.MainModule main = splatPs.main;
                main.startColor = GoldenSliceBuilder.SplatFallbackColor;
                PrefabUtility.SaveAsPrefabAsset(splatRoot, "Assets/Prefabs/Splat_FX.prefab");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(splatRoot); }

        AssetDatabase.SaveAssets();
        Debug.Log($"[TriangleFxBuilder] {changed} particle prefabs are now triangle shards.");
    }
}
