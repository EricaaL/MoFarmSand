using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Additive, idempotent: swaps the primitive butterfly (capsule + sphere wings) for flat paper cutouts cut from butterfly.psd.
/// Everything sits under a billboarded "Paper" pivot; each wing hangs off a WingPivot that ButterflyBoss flaps.
/// A butterfly that already has a "Paper" child is left alone, so tuned values survive re-runs.
/// </summary>
public static class ButterflyPaperBuilder
{
    private const string BossPrefabPath = "Assets/Prefabs/Caterpillar_Primitive.prefab";
    private const string ArtDir = "Assets/Art/Characters/Butterfly";
    private const string MatDir = "Assets/Art/Materials";
    private const float PixelsPerUnit = 520f;

    // PSD canvas pixels (y down). The body's horizontal centre is the origin; the wing hinges sit where each wing meets the body.
    private const float OriginX = 842f;
    private const float OriginY = 775f;
    // Wing cards slide this far toward the body so each wing root tucks under the body card.
    private const float WingTuckPx = 30f;
    private static readonly Vector2 HingeL = new Vector2(770f, 700f);
    private static readonly Vector2 HingeR = new Vector2(915f, 700f);

    private struct Layer
    {
        public string texture; public Vector4 bbox; public float z;
        public Layer(string texture, float x0, float y0, float x1, float y1, float z)
        { this.texture = texture; bbox = new Vector4(x0, y0, x1, y1); this.z = z; }
        public Vector2 CentrePx => new Vector2((bbox.x + bbox.z) * 0.5f, (bbox.y + bbox.w) * 0.5f);
    }

    private static readonly Layer LeftWing = new Layer("Bt_LeftWing", 20, 195, 841, 1529, 0f);
    private static readonly Layer LeftInside = new Layer("Bt_LeftWingInside", 155, 256, 778, 1147, -0.005f);
    private static readonly Layer RightWing = new Layer("Bt_RightWing", 878, 199, 1699, 1533, 0f);
    private static readonly Layer RightInside = new Layer("Bt_RightWingInside", 941, 260, 1564, 1151, -0.005f);
    private static readonly Layer Body = new Layer("Bt_Body", 718, 256, 967, 1322, -0.06f);
    private static readonly Layer Eyes = new Layer("Bt_Eyes", 746, 311, 954, 1060, -0.07f);
    private static readonly Layer Antennae = new Layer("Bt_Antennae", 601, 18, 1099, 304, -0.08f);

    private static readonly string[] Textures =
        { "Bt_LeftWing", "Bt_LeftWingInside", "Bt_RightWing", "Bt_RightWingInside", "Bt_Body", "Bt_Eyes", "Bt_Antennae" };

    /// <summary>Drops the existing paper first, so new artwork or tuned anchors replace the old cards.</summary>
    [MenuItem("MoFarmSand/Rebuild Paper Butterfly")]
    public static void Rebuild()
    {
        AssetDatabase.Refresh();
        GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
        try
        {
            Transform paper = root.transform.Find("Butterfly/Paper");
            if (paper != null) Object.DestroyImmediate(paper.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Apply();
    }

    [MenuItem("MoFarmSand/Paper Butterfly")]
    public static void Apply()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        foreach (string name in Textures) ConfigureTexture($"{ArtDir}/{name}.png");

        GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
        try
        {
            Build(root);
            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ButterflyStageBuilder.RefreshFeedbackRenderers();
        CopyTintRenderers();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[ButterflyPaperBuilder] Paper butterfly applied.");
    }

    private static void Build(GameObject root)
    {
        Transform butterfly = root.transform.Find("Butterfly");
        if (butterfly == null) { Debug.LogError("[ButterflyPaperBuilder] Prefab has no Butterfly child. Run Add Butterfly Stage first."); return; }
        if (butterfly.Find("Paper") != null) return;

        // The primitive body and wings go; the wing pivots are kept (or made) and moved under the paper.
        Transform oldBody = butterfly.Find("Body");
        if (oldBody != null) Object.DestroyImmediate(oldBody.gameObject);

        GameObject paperGo = new GameObject("Paper");
        paperGo.transform.SetParent(butterfly, false);
        PaperBillboard billboard = paperGo.AddComponent<PaperBillboard>();
        Set(billboard, "flip", false);
        Set(billboard, "bobHeight", 0.04f);
        Set(billboard, "tiltDegrees", 3f);

        Transform pivotL = WingPivot(butterfly, paperGo.transform, "WingPivotL", HingeL, LeftWing, LeftInside, WingTuckPx);
        Transform pivotR = WingPivot(butterfly, paperGo.transform, "WingPivotR", HingeR, RightWing, RightInside, -WingTuckPx);

        // Body, eyes, antennae sit in front of the wings so a flapping wing never cuts through them.
        Card(paperGo.transform, Body, Vector2.zero);
        Card(paperGo.transform, Eyes, Vector2.zero);
        Card(paperGo.transform, Antennae, Vector2.zero);

        SphereCollider hit = butterfly.GetComponent<SphereCollider>();
        if (hit != null) { hit.center = Vector3.zero; hit.radius = 1.4f; }

        ButterflyBoss boss = root.GetComponent<ButterflyBoss>();
        SerializedObject bso = new SerializedObject(boss);
        SerializedProperty wp = bso.FindProperty("wingPivots");
        wp.arraySize = 2;
        wp.GetArrayElementAtIndex(0).objectReferenceValue = pivotL;
        wp.GetArrayElementAtIndex(1).objectReferenceValue = pivotR;
        bso.FindProperty("hoverHeight").floatValue = 1.8f;
        bso.FindProperty("flapDegrees").floatValue = 35f;
        bso.FindProperty("flapBackOnly").boolValue = true;
        bso.FindProperty("flapAxis").vector3Value = Vector3.up;
        bso.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>A wing pivot at the hinge holding the wing card and its inner-pattern card, both placed relative to the hinge.</summary>
    private static Transform WingPivot(Transform butterfly, Transform paper, string name, Vector2 hingePx, Layer wing, Layer inside, float tuckPx)
    {
        Transform pivot = butterfly.Find(name);
        if (pivot == null) pivot = new GameObject(name).transform;
        foreach (Transform child in pivot) Object.DestroyImmediate(child.gameObject);
        pivot.SetParent(paper, false);
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        pivot.localPosition = ToUnits(hingePx - new Vector2(OriginX, OriginY));
        Card(pivot, wing, hingePx, tuckPx);
        Card(pivot, inside, hingePx, tuckPx);
        return pivot;
    }

    /// <summary>Adds a textured quad for a PSD layer, positioned so its bbox centre lands at (layer - origin) relative to originPx.</summary>
    private static void Card(Transform parent, Layer layer, Vector2 hingePx, float tuckPx = 0f)
    {
        Vector2 origin = hingePx == Vector2.zero ? new Vector2(OriginX, OriginY) : hingePx;
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = layer.texture;
        quad.transform.SetParent(parent, false);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.GetComponent<Renderer>().sharedMaterial = PaperMat(layer.texture);
        Texture2D tex = Tex(layer.texture);
        quad.transform.localScale = new Vector3(tex.width / PixelsPerUnit, tex.height / PixelsPerUnit, 1f);
        Vector3 p = ToUnits(layer.CentrePx + new Vector2(tuckPx, 0f) - origin);
        quad.transform.localPosition = new Vector3(p.x, p.y, layer.z);
    }

    // PSD pixels are y-down, Unity is y-up.
    private static Vector3 ToUnits(Vector2 px) => new Vector3(px.x / PixelsPerUnit, -px.y / PixelsPerUnit, 0f);

    private static void CopyTintRenderers()
    {
        GameFeedback feedback = Object.FindFirstObjectByType<GameFeedback>();
        BossReactions reactions = Object.FindFirstObjectByType<BossReactions>(FindObjectsInactive.Include);
        if (feedback == null || reactions == null) return;
        SerializedProperty src = new SerializedObject(feedback).FindProperty("bossRenderers");
        SerializedObject dst = new SerializedObject(reactions);
        SerializedProperty list = dst.FindProperty("tintRenderers");
        list.arraySize = src.arraySize;
        for (int i = 0; i < src.arraySize; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = src.GetArrayElementAtIndex(i).objectReferenceValue;
        dst.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(reactions.gameObject.scene);
    }

    private static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{name}.png");

    private static void ConfigureTexture(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogError($"[ButterflyPaperBuilder] Missing texture {path}"); return; }
        ti.textureType = TextureImporterType.Default;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.mipmapEnabled = true;
        ti.maxTextureSize = 1024;
        ti.SaveAndReimport();
    }

    private static Material PaperMat(string texName)
    {
        string path = $"{MatDir}/Mat_{texName}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_BaseMap", Tex(texName));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_Cull", 0f);              // both faces, so the wing flap never shows a back
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_SpecularHighlights", 0f);
        m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void Set(Object target, string field, float value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[ButterflyPaperBuilder] Missing field {field} on {target.GetType().Name}"); return; }
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string field, bool value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[ButterflyPaperBuilder] Missing field {field} on {target.GetType().Name}"); return; }
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
