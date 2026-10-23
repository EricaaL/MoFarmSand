using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes a tileable triangle-patterned texture from <see cref="GroundPatternConfig"/>, makes a URP Lit material for it and puts
/// it on the Ground plane. Idempotent: the config asset is only created once, so tuned colours survive re-runs.
/// </summary>
public static class GroundTextureBuilder
{
    private const string ConfigPath = "Assets/Data/GroundPatternConfig.asset";
    private const string TexturePath = "Assets/Art/Environment/Ground_Triangles.png";
    private const string MaterialPath = "Assets/Art/Materials/Ground_Triangles.mat";
    private const string EnvironmentName = "--- ENVIRONMENT ---";

    private static bool rebuildQueued;

    [InitializeOnLoadMethod]
    private static void WatchConfig()
    {
        GroundPatternConfig.Edited -= QueueRebuild;
        GroundPatternConfig.Edited += QueueRebuild;
    }

    // OnValidate fires on every keystroke/drag, so collapse them into one rebuild after the editor goes idle.
    private static void QueueRebuild()
    {
        if (rebuildQueued || Application.isPlaying) return;
        rebuildQueued = true;
        EditorApplication.delayCall += () =>
        {
            rebuildQueued = false;
            if (AssetDatabase.LoadAssetAtPath<GroundPatternConfig>(ConfigPath) != null) Build();
        };
    }

    [MenuItem("MoFarmSand/Build Triangle Ground Texture")]
    public static void Build()
    {
        AssetDatabase.SaveAssets();

        GroundPatternConfig config = AssetDatabase.LoadAssetAtPath<GroundPatternConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GroundPatternConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        if (config.palette == null || config.palette.Length == 0) { Debug.LogError("GroundTexture: palette is empty."); return; }

        Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
        File.WriteAllBytes(TexturePath, Render(config).EncodeToPNG());
        AssetDatabase.ImportAsset(TexturePath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", Vector2.one * config.tiling);
        mat.SetFloat("_Smoothness", 0f);
        EditorUtility.SetDirty(mat);

        GameObject env = GameObject.Find(EnvironmentName);
        Transform ground = env != null ? env.transform.Find("Ground") : null;
        Renderer r = ground != null ? ground.GetComponent<Renderer>() : null;
        if (r != null)
        {
            Undo.RecordObject(r, "Triangle Ground Material");
            r.sharedMaterial = mat;
        }
        else Debug.LogWarning("GroundTexture: no Ground renderer in the open scene; texture and material were still built.");

        AssetDatabase.SaveAssets();
        Debug.Log($"GroundTexture: wrote {TexturePath} ({config.cells}x{config.cells} cells, {config.cells * config.cells * 2} triangles).");
    }

    private static Texture2D Render(GroundPatternConfig c)
    {
        int size = c.textureSize;
        var rng = new System.Random(c.seed);

        // One colour per triangle, picked up front so the pixel loop stays a pure lookup.
        var colours = new Color[c.cells, c.cells, 2];
        for (int y = 0; y < c.cells; y++)
            for (int x = 0; x < c.cells; x++)
                for (int t = 0; t < 2; t++)
                {
                    Color col = c.palette[rng.Next(c.palette.Length)];
                    float shift = ((float)rng.NextDouble() * 2f - 1f) * c.brightnessJitter;
                    colours[x, y, t] = new Color(Mathf.Clamp01(col.r + shift), Mathf.Clamp01(col.g + shift), Mathf.Clamp01(col.b + shift), 1f);
                }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float cellPx = size / (float)c.cells;
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                int cx = Mathf.Min((int)(px / cellPx), c.cells - 1);
                int cy = Mathf.Min((int)(py / cellPx), c.cells - 1);
                float u = px / cellPx - cx, v = py / cellPx - cy;
                // Flip the diagonal on alternate cells so the triangles zig-zag instead of forming one long stripe.
                if (((cx + cy) & 1) == 1) u = 1f - u;
                pixels[py * size + px] = colours[cx, cy, u + v < 1f ? 0 : 1];
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
