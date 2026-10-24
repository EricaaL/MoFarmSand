using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns the dog's and the merchant's PBR texture sets into URP Lit materials, rebuilds the player body with the new dog
/// mesh (<see cref="MainCharacterBuilder"/>) and saves the merchant as a prefab. Idempotent: materials are created once, so
/// tuned values survive re-runs; only the texture slots are refreshed.
/// </summary>
public static class CharacterArtBuilder
{
    private const string TexDir = "Assets/Art/Textures";
    public const string FarmerMaterialPath = "Assets/Art/Characters/MainCharacter/Materials/FarmerMAT.mat";
    private const string MerchantMaterialPath = "Assets/Art/Characters/Merchant/Materials/MerchantMAT.mat";
    private const string MerchantMeshPath = "Assets/Art/Meshes/Merchant_Unwrapped.fbx";
    private const string MerchantPrefabPath = "Assets/Prefabs/Characters/Merchant.prefab";

    // The dog is 1.4 m (CharacterController); the merchant stands a little taller behind his counter.
    private const float MerchantHeight = 1.6f;
    private const float HeightScale = 0.005f;

    [MenuItem("MoFarmSand/Setup Character Art")]
    public static void Build()
    {
        AssetDatabase.SaveAssets();
        BuildMaterials();
        MainCharacterBuilder.Build();
        BuildMerchantPrefab();
        AssetDatabase.SaveAssets();
        Debug.Log("[CharacterArtBuilder] Dog and merchant art set up.");
    }

    private static void BuildMaterials()
    {
        EnsureLitMaterial(FarmerMaterialPath,
            Tex("FarmerMAT_Base_color.png"), Tex("FarmerMAT_Normal_OpenGL.png"),
            BakeMetalSmooth("FarmerMAT_MetallicSmoothness.png", "FarmerMAT_Base_metalness.png", "FarmerMAT_Specular_roughness.png"),
            Tex("FarmerMAT_Mixed_AO.png"), Tex("FarmerMAT_Height.png"));
        EnsureLitMaterial(MerchantMaterialPath,
            Tex("MerchantMAT_BaseColor.png"), Tex("MerchantMAT_Normal.png"),
            BakeMetalSmooth("MerchantMAT_MetallicSmoothness.png", null, "MerchantMAT_Roughness.png"),
            null, Tex("MerchantMAT_Height.png"));
    }

    private static string TexPath(string file) => $"{TexDir}/{file}";

    /// <summary>Loads a texture after fixing its import: normal maps as normals, every data map linear.</summary>
    private static Texture2D Tex(string file)
    {
        string path = TexPath(file);
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogError("[CharacterArtBuilder] Missing texture " + path); return null; }

        bool isColor = file.Contains("Base_color") || file.Contains("BaseColor");
        bool isNormal = file.Contains("Normal");
        bool dirty = false;
        if (isNormal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
        if (!isColor && !isNormal && ti.sRGBTexture) { ti.sRGBTexture = false; dirty = true; }
        if (dirty) ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>
    /// URP Lit reads metallic from R and smoothness from A of one map, but the sets ship roughness separately: bake
    /// R = metalness (0 when there is none) and A = 1 - roughness. Rebuilt only when a source is newer than the result.
    /// </summary>
    private static Texture2D BakeMetalSmooth(string outFile, string metalFile, string roughFile)
    {
        string outPath = TexPath(outFile);
        string roughPath = TexPath(roughFile);
        string metalPath = metalFile == null ? null : TexPath(metalFile);

        bool stale = !File.Exists(outPath)
            || File.GetLastWriteTimeUtc(roughPath) > File.GetLastWriteTimeUtc(outPath)
            || (metalPath != null && File.GetLastWriteTimeUtc(metalPath) > File.GetLastWriteTimeUtc(outPath));
        if (stale)
        {
            Texture2D rough = ReadRaw(roughPath);
            Texture2D metal = metalPath == null ? null : ReadRaw(metalPath);
            int w = rough.width, h = rough.height;
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float m = metal == null ? 0f : metal.GetPixelBilinear(u, v).r;
                    float r = rough.GetPixel(x, y).r;
                    px[y * w + x] = new Color(m, 0f, 0f, 1f - r);
                }
            }
            Texture2D baked = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            baked.SetPixels(px);
            File.WriteAllBytes(outPath, baked.EncodeToPNG());
            Object.DestroyImmediate(baked);
            Object.DestroyImmediate(rough);
            if (metal != null) Object.DestroyImmediate(metal);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
        }

        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(outPath);
        if (ti.sRGBTexture || ti.alphaIsTransparency)
        {
            ti.sRGBTexture = false;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
    }

    private static Texture2D ReadRaw(string path)
    {
        Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        t.LoadImage(File.ReadAllBytes(path));
        return t;
    }

    private static void EnsureLitMaterial(string path, Texture2D baseMap, Texture2D normal, Texture2D metalSmooth,
        Texture2D occlusion, Texture2D height)
    {
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 1f);        // the map scales it
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_Parallax", HeightScale);
            mat.SetFloat("_OcclusionStrength", 1f);
            mat.SetFloat("_BumpScale", 1f);
        }

        mat.SetTexture("_BaseMap", baseMap);
        mat.SetTexture("_BumpMap", normal);
        mat.SetTexture("_MetallicGlossMap", metalSmooth);
        mat.SetTexture("_OcclusionMap", occlusion);
        mat.SetTexture("_ParallaxMap", height);
        SetKeyword(mat, "_NORMALMAP", normal != null);
        SetKeyword(mat, "_METALLICSPECGLOSSMAP", metalSmooth != null);
        SetKeyword(mat, "_OCCLUSIONMAP", occlusion != null);
        SetKeyword(mat, "_PARALLAXMAP", height != null);
        EditorUtility.SetDirty(mat);
    }

    private static void SetKeyword(Material mat, string keyword, bool on)
    {
        if (on) mat.EnableKeyword(keyword); else mat.DisableKeyword(keyword);
    }

    private static void BuildMerchantPrefab()
    {
        GameObject mesh = AssetDatabase.LoadAssetAtPath<GameObject>(MerchantMeshPath);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MerchantMaterialPath);
        if (mesh == null || mat == null) { Debug.LogError("[CharacterArtBuilder] Merchant mesh or material missing."); return; }

        EnsureFolder(Path.GetDirectoryName(MerchantPrefabPath).Replace('\\', '/'));
        GameObject root = new GameObject("Merchant");
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(mesh, root.transform);
            model.name = "Model";
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }

            Bounds b = CombinedBounds(model);
            float scale = b.size.y > 0.0001f ? MerchantHeight / b.size.y : 1f;
            model.transform.localScale = Vector3.one * scale;
            b = CombinedBounds(model);
            model.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            // Renderer bounds lag one change behind in an unsaved hierarchy, so measure again and correct the leftover.
            b = CombinedBounds(model);
            model.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);

            PrefabUtility.SaveAsPrefabAsset(root, MerchantPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Bounds CombinedBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.one);
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
