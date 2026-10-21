using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Additive, idempotent setup for boss stage 2 (butterfly). Never rebuilds the scene, so COZY stays.</summary>
public static class ButterflyStageBuilder
{
    private const string CaterpillarDataPath = "Assets/Data/Boss_Caterpillar.asset";
    private const string ButterflyDataPath = "Assets/Data/Boss_Butterfly.asset";
    private const string BodyMatPath = "Assets/Art/Materials/Mat_Butterfly.mat";
    private const string WingMatPath = "Assets/Art/Materials/Mat_ButterflyWing.mat";
    private const string BossPrefabPath = "Assets/Prefabs/Caterpillar_Primitive.prefab";

    [MenuItem("MoFarmSand/Add Butterfly Stage")]
    public static void AddButterflyStage()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        BossData caterpillarData = AssetDatabase.LoadAssetAtPath<BossData>(CaterpillarDataPath);
        if (caterpillarData == null)
        {
            Debug.LogError("[ButterflyStageBuilder] Boss_Caterpillar.asset not found. Run the Golden Slice builder first.");
            return;
        }

        BossData butterflyData = AssetDatabase.LoadAssetAtPath<BossData>(ButterflyDataPath);
        if (butterflyData == null)
        {
            butterflyData = ScriptableObject.CreateInstance<BossData>();
            AssetDatabase.CreateAsset(butterflyData, ButterflyDataPath);
        }
        butterflyData.displayName = "Butterfly";
        butterflyData.maxFullness = 60f;
        butterflyData.chaseSpeed = 4.5f;
        butterflyData.damage = 1;
        butterflyData.nextStage = null;
        EditorUtility.SetDirty(butterflyData);

        caterpillarData.nextStage = butterflyData;
        EditorUtility.SetDirty(caterpillarData);

        Material bodyMat = Mat(BodyMatPath, new Color(0.75f, 0.65f, 0.95f));
        Material wingMat = Mat(WingMatPath, new Color(1f, 0.6f, 0.8f));

        GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
        try
        {
            BuildButterfly(root, bodyMat, wingMat);
            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        RefreshFeedbackRenderers();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[ButterflyStageBuilder] Butterfly stage added.");
    }

    private static void BuildButterfly(GameObject root, Material bodyMat, Material wingMat)
    {
        Transform existing = root.transform.Find("Butterfly");
        GameObject butterflyGo;
        Transform[] pivots;
        if (existing != null)
        {
            butterflyGo = existing.gameObject;
            pivots = new[] { existing.Find("WingPivotL"), existing.Find("WingPivotR") };
        }
        else
        {
            butterflyGo = new GameObject("Butterfly");
            butterflyGo.transform.SetParent(root.transform, false);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(butterflyGo.transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;

            pivots = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                GameObject pivot = new GameObject(i == 0 ? "WingPivotL" : "WingPivotR");
                pivot.transform.SetParent(butterflyGo.transform, false);
                pivot.transform.localPosition = new Vector3(side * 0.25f, 0.1f, 0f);
                pivots[i] = pivot.transform;

                GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                wing.name = "Wing";
                wing.transform.SetParent(pivot.transform, false);
                wing.transform.localPosition = new Vector3(side * 0.7f, 0f, 0f);
                wing.transform.localScale = new Vector3(1.4f, 0.08f, 1.1f);
                Object.DestroyImmediate(wing.GetComponent<Collider>());
                wing.GetComponent<Renderer>().sharedMaterial = wingMat;
            }

            SphereCollider hit = butterflyGo.AddComponent<SphereCollider>();
            hit.radius = 1.2f;
            butterflyGo.SetActive(false);
        }

        ButterflyBoss butterfly = Ensure<ButterflyBoss>(root);
        butterfly.enabled = false;
        SerializedObject bso = new SerializedObject(butterfly);
        bso.FindProperty("body").objectReferenceValue = butterflyGo.transform;
        SerializedProperty wp = bso.FindProperty("wingPivots");
        wp.arraySize = pivots.Length;
        for (int i = 0; i < pivots.Length; i++) wp.GetArrayElementAtIndex(i).objectReferenceValue = pivots[i];
        bso.ApplyModifiedPropertiesWithoutUndo();

        BossEvolution evolution = Ensure<BossEvolution>(root);
        SerializedObject eso = new SerializedObject(evolution);
        eso.FindProperty("caterpillar").objectReferenceValue = root.GetComponent<CaterpillarBoss>();
        eso.FindProperty("butterfly").objectReferenceValue = butterfly;
        eso.FindProperty("butterflyBody").objectReferenceValue = butterflyGo;
        SerializedProperty parts = eso.FindProperty("caterpillarParts");
        int count = 0;
        foreach (Transform child in root.transform) if (child.gameObject != butterflyGo) count++;
        parts.arraySize = count;
        int idx = 0;
        foreach (Transform child in root.transform)
            if (child.gameObject != butterflyGo) parts.GetArrayElementAtIndex(idx++).objectReferenceValue = child.gameObject;
        eso.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void RefreshFeedbackRenderers()
    {
        GameFeedback feedback = Object.FindFirstObjectByType<GameFeedback>();
        BossHunger hunger = Object.FindFirstObjectByType<BossHunger>(FindObjectsInactive.Include);
        if (feedback == null || hunger == null) return;

        Renderer[] rs = hunger.GetComponentsInChildren<Renderer>(true);
        SerializedObject so = new SerializedObject(feedback);
        SerializedProperty rp = so.FindProperty("bossRenderers");
        rp.arraySize = rs.Length;
        for (int i = 0; i < rs.Length; i++) rp.GetArrayElementAtIndex(i).objectReferenceValue = rs[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(feedback.gameObject.scene);
    }

    private static Material Mat(string path, Color color)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
