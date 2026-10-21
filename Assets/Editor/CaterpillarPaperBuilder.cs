using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Additive, idempotent: swaps the primitive caterpillar's sphere meshes for flat paper cutouts cut from caterpillarNEWclip.psd.
/// The sphere colliders stay (crop hits keep working); each gets a billboarded "Paper" pivot with a textured quad.
/// A segment that already has a "Paper" child is left alone, so tuned values survive re-runs.
/// </summary>
public static class CaterpillarPaperBuilder
{
    private const string BossPrefabPath = "Assets/Prefabs/Caterpillar_Primitive.prefab";
    private const string ArtDir = "Assets/Art/Characters/Caterpillar";
    private const string MatDir = "Assets/Art/Materials";
    private const float PixelsPerUnit = 420f;
    private const float SegmentSpacing = 0.85f;

    // Where the paper card sits on its sphere: pixel offset (x right, y up) from the card centre to the point that should
    // land on the sphere centre. Measured from the PSD cut (head circle sits below the antennae; tail body left of its antennae).
    private struct CardSpec
    {
        public string texture; public Vector2 anchorPx; public float scale; public float sphereScale;
        public CardSpec(string texture, Vector2 anchorPx, float scale, float sphereScale)
        { this.texture = texture; this.anchorPx = anchorPx; this.scale = scale; this.sphereScale = sphereScale; }
    }

    private static readonly CardSpec HeadCard = new CardSpec("Cat_Head", new Vector2(0f, -149f), 1f, 1.4f);
    // Mouth centre relative to the head card centre, in head-texture pixels (x right, y up).
    private static readonly Vector2 MouthOffsetPx = new Vector2(39f, -305f);

    private static readonly CardSpec[] BodyCards =
    {
        new CardSpec("Cat_SegYellowB", Vector2.zero, 0.95f, 1.2f),
        new CardSpec("Cat_SegYellow", Vector2.zero, 0.95f, 1.1f),
        new CardSpec("Cat_SegYellowB", Vector2.zero, 0.85f, 1.0f),
        new CardSpec("Cat_SegOrange", Vector2.zero, 0.85f, 0.9f),
        new CardSpec("Cat_SegOrange", Vector2.zero, 0.75f, 0.8f),
        new CardSpec("Cat_Tail", new Vector2(-154f, -98f), 0.8f, 0.8f),
    };

    [MenuItem("MoFarmSand/Paper Caterpillar")]
    public static void Apply()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        foreach (string name in new[] { "Cat_Head", "Cat_Mouth", "Cat_SegYellow", "Cat_SegYellowB", "Cat_SegOrange", "Cat_Tail" })
            ConfigureTexture($"{ArtDir}/{name}.png");

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
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[CaterpillarPaperBuilder] Paper caterpillar applied.");
    }

    private static void Build(GameObject root)
    {
        Transform head = root.transform.Find("Head");
        if (head == null) { Debug.LogError("[CaterpillarPaperBuilder] Prefab has no Head child."); return; }
        StripMesh(head.gameObject);
        if (head.Find("Paper") == null)
        {
            Transform card = BuildPaper(head, HeadCard, 0f);
            Transform mouth = BuildQuad("Mouth", card, "Cat_Mouth");
            Texture2D headTex = Tex("Cat_Head");
            Texture2D mouthTex = Tex("Cat_Mouth");
            mouth.localPosition = new Vector3(MouthOffsetPx.x / headTex.width, MouthOffsetPx.y / headTex.height, -0.02f);
            mouth.localScale = new Vector3((float)mouthTex.width / headTex.width, (float)mouthTex.height / headTex.height, 1f);
            CaterpillarFace face = head.gameObject.AddComponent<CaterpillarFace>();
            Set(face, "mouth", mouth);
        }

        Transform[] chain = new Transform[BodyCards.Length];
        for (int i = 0; i < BodyCards.Length; i++)
        {
            string segName = "Segment" + (i + 1);
            Transform seg = root.transform.Find(segName);
            if (seg == null)
            {
                GameObject go = new GameObject(segName);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -SegmentSpacing * (i + 1));
                go.transform.localScale = Vector3.one * BodyCards[i].sphereScale;
                go.AddComponent<SphereCollider>();
                seg = go.transform;
            }
            StripMesh(seg.gameObject);
            if (seg.Find("Paper") == null) BuildPaper(seg, BodyCards[i], (i + 1) * 1.1f);
            chain[i] = seg;
        }

        CaterpillarBoss boss = root.GetComponent<CaterpillarBoss>();
        SerializedObject bso = new SerializedObject(boss);
        SerializedProperty segs = bso.FindProperty("segments");
        segs.arraySize = chain.Length;
        for (int i = 0; i < chain.Length; i++) segs.GetArrayElementAtIndex(i).objectReferenceValue = chain[i];
        bso.FindProperty("segmentSpacing").floatValue = SegmentSpacing;
        bso.ApplyModifiedPropertiesWithoutUndo();

        // Everything except the butterfly is a caterpillar part, so the evolution hides the paper body too.
        BossEvolution evolution = root.GetComponent<BossEvolution>();
        if (evolution != null)
        {
            SerializedObject eso = new SerializedObject(evolution);
            SerializedProperty parts = eso.FindProperty("caterpillarParts");
            GameObject butterfly = eso.FindProperty("butterflyBody").objectReferenceValue as GameObject;
            int count = 0;
            foreach (Transform child in root.transform) if (child.gameObject != butterfly) count++;
            parts.arraySize = count;
            int idx = 0;
            foreach (Transform child in root.transform)
                if (child.gameObject != butterfly) parts.GetArrayElementAtIndex(idx++).objectReferenceValue = child.gameObject;
            eso.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>Adds Paper (billboard pivot) > Card (textured quad) under an owner sphere. Returns the card.</summary>
    private static Transform BuildPaper(Transform owner, CardSpec spec, float phase)
    {
        GameObject pivot = new GameObject("Paper");
        pivot.transform.SetParent(owner, false);
        // Cancel the sphere's scale so the card is sized in world units; PaperBillboard flips around this pivot.
        Vector3 ps = owner.localScale;
        pivot.transform.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);
        PaperBillboard billboard = pivot.AddComponent<PaperBillboard>();
        Set(billboard, "phase", phase);

        Transform card = BuildQuad("Card", pivot.transform, spec.texture);
        Texture2D tex = Tex(spec.texture);
        float k = spec.scale / PixelsPerUnit;
        card.localScale = new Vector3(tex.width * k, tex.height * k, 1f);
        card.localPosition = new Vector3(-spec.anchorPx.x * k, -spec.anchorPx.y * k, 0f);
        return card;
    }

    private static Transform BuildQuad(string name, Transform parent, string texName)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.GetComponent<Renderer>().sharedMaterial = PaperMat(texName);
        return quad.transform;
    }

    private static void StripMesh(GameObject go)
    {
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        if (r != null) Object.DestroyImmediate(r);
        MeshFilter f = go.GetComponent<MeshFilter>();
        if (f != null) Object.DestroyImmediate(f);
    }

    private static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{name}.png");

    private static void ConfigureTexture(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogError($"[CaterpillarPaperBuilder] Missing texture {path}"); return; }
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
        Texture2D tex = Tex(texName);
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_Cull", 0f);              // render both faces so the flip never shows a back
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

    private static void Set(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[CaterpillarPaperBuilder] Missing field {field} on {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string field, float value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[CaterpillarPaperBuilder] Missing field {field} on {target.GetType().Name}"); return; }
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
