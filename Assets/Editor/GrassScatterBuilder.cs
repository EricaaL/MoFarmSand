using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Scatters the CartoonGrass grass and plant prefabs over the ground of the open scene. Idempotent: it rebuilds the
/// "Grass" group under "--- ENVIRONMENT ---" from a fixed seed, so re-running gives the same layout. Grass stays out of
/// the farm field (including where extra farmland is built) and off props and trees.
/// </summary>
public static class GrassScatterBuilder
{
    private const string GrassFolder = "Assets/CartoonGrass/Prefabs";
    private static readonly string[] PrefabNames =
    {
        "Grass_01", "Grass_02", "Grass_03", "Grass_04-1", "Grass_04-2", "Grass_05-1", "Grass_05-2",
        "Grass_06-1", "Grass_06-2", "Grass_07-1", "Grass_07-2", "Plant_01", "Plant_02",
    };
    private const string EnvironmentName = "--- ENVIRONMENT ---";
    private const string GroupName = "Grass";

    private const int Seed = 2207;
    private const int Clumps = 160;
    private const int BladesPerClump = 7;
    private const float ClumpRadius = 1.6f;
    private const int Stragglers = 120;
    private const float MinScale = 1f;
    private const float MaxScale = 1.3f;

    // Field footprint plus room for extra farmland columns and rows; no grass in here.
    private static readonly Rect FieldKeepClear = new Rect(-6.5f, -4.5f, 13f, 11f);
    private const float PropClearance = 0.6f;

    // Spring wildflowers: sparser than the grass, shown only in spring (SeasonFlowers + SeasonLook.groundFlowers).
    private static readonly string[] FlowerNames =
    {
        "Flower_01", "Flower_02", "Flower_03", "Flower_04", "Flower_05", "Flower_06", "Flower_07", "Flower_08",
        "Plant_03", "Plant_04",
    };
    private const string FlowerGroupName = "SpringFlowers";
    private const string SpringSeasonPath = "Assets/Data/Seasons/Season_Spring.asset";
    private const string WeatherMapPath = "Assets/Data/Presentation/CozyWeatherMap.asset";

    [MenuItem("MoFarmSand/Scatter Grass")]
    public static void Scatter()
        => ScatterGroup(GroupName, PrefabNames, Seed, Clumps, BladesPerClump, ClumpRadius, Stragglers, MinScale, MaxScale);

    [MenuItem("MoFarmSand/Scatter Spring Flowers")]
    public static void ScatterSpringFlowers()
    {
        GameObject group = ScatterGroup(FlowerGroupName, FlowerNames, 3109, 300, 5, 1.2f, 400, 1.5f, 2.1f);
        if (group == null) return;

        // Wire the season toggle: SeasonFlowers on the CozyBridge, and the spring look asks for flowers.
        CozyBridge bridge = Object.FindFirstObjectByType<CozyBridge>(FindObjectsInactive.Include);
        if (bridge != null)
        {
            SeasonFlowers sf = bridge.GetComponent<SeasonFlowers>();
            if (sf == null) sf = Undo.AddComponent<SeasonFlowers>(bridge.gameObject);
            SerializedObject sfo = new SerializedObject(sf);
            sfo.FindProperty("flowers").objectReferenceValue = group;
            sfo.ApplyModifiedProperties();
            SerializedObject bo = new SerializedObject(bridge);
            bo.FindProperty("seasonFlowers").objectReferenceValue = sf;
            bo.ApplyModifiedProperties();
        }
        else Debug.LogWarning("GrassScatter: no CozyBridge in the open scene, so the flowers will not follow the season.");

        SeasonData spring = AssetDatabase.LoadAssetAtPath<SeasonData>(SpringSeasonPath);
        CozyWeatherMap map = AssetDatabase.LoadAssetAtPath<CozyWeatherMap>(WeatherMapPath);
        if (spring != null && map != null)
        {
            SerializedObject mo = new SerializedObject(map);
            SerializedProperty looks = mo.FindProperty("seasonLooks");
            for (int i = 0; i < looks.arraySize; i++)
            {
                SerializedProperty look = looks.GetArrayElementAtIndex(i);
                look.FindPropertyRelative("groundFlowers").boolValue =
                    look.FindPropertyRelative("season").objectReferenceValue == spring;
            }
            mo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
        }
        else Debug.LogWarning("GrassScatter: spring season or CozyWeatherMap asset not found.");
    }

    private static GameObject ScatterGroup(string groupName, string[] names, int seed, int clumps, int perClump,
        float clumpRadius, int stragglers, float minScale, float maxScale)
    {
        GameObject[] prefabs = LoadPrefabs(names);
        if (prefabs.Length == 0) { Debug.LogError("GrassScatter: no prefabs found in " + GrassFolder); return null; }

        GameObject env = GameObject.Find(EnvironmentName);

        // The ground is a set of tiles ("Ground", "Ground (1)", ...), now under Terrain; take their combined bounds.
        bool found = false;
        Bounds gb = new Bounds();
        if (env != null)
        {
            foreach (Renderer r in env.GetComponentsInChildren<Renderer>())
            {
                if (!IsGround(r.gameObject)) continue;
                if (!found) { gb = r.bounds; found = true; }
                else gb.Encapsulate(r.bounds);
            }
        }
        if (!found) { Debug.LogError("GrassScatter: no Ground renderer under " + EnvironmentName); return null; }

        Transform old = env != null ? env.transform.Find(groupName) : null;
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject group = new GameObject(groupName);
        Undo.RegisterCreatedObjectUndo(group, "Scatter " + groupName);
        if (env != null) group.transform.SetParent(env.transform, false);

        // Colliders must be current for the overlap checks to see props moved since the last physics step.
        Physics.SyncTransforms();

        System.Random rng = new System.Random(seed);
        float margin = 1.5f;
        int placed = 0;

        for (int c = 0; c < clumps; c++)
        {
            Vector3 center = RandomSpot(rng, gb, margin);
            for (int b = 0; b < perClump; b++)
            {
                double a = rng.NextDouble() * Mathf.PI * 2.0;
                float r = clumpRadius * (float)(rng.NextDouble() * rng.NextDouble()); // dense in the middle
                Vector3 p = center + new Vector3((float)System.Math.Cos(a) * r, 0f, (float)System.Math.Sin(a) * r);
                if (Place(rng, prefabs, group.transform, p, gb, minScale, maxScale)) placed++;
            }
        }
        for (int i = 0; i < stragglers; i++)
            if (Place(rng, prefabs, group.transform, RandomSpot(rng, gb, margin), gb, minScale, maxScale)) placed++;

        EditorSceneManager.MarkSceneDirty(group.scene);
        Debug.Log($"GrassScatter: placed {placed} pieces under {EnvironmentName}/{groupName}.");
        return group;
    }

    private static bool Place(System.Random rng, GameObject[] prefabs, Transform parent, Vector3 p, Bounds ground,
        float minScale, float maxScale)
    {
        if (p.x < ground.min.x || p.x > ground.max.x || p.z < ground.min.z || p.z > ground.max.z) return false;
        if (FieldKeepClear.Contains(new Vector2(p.x, p.z))) return false;

        // Drop onto the ground surface; skip anything that lands on a prop, tree or building instead.
        Vector3 from = new Vector3(p.x, ground.max.y + 20f, p.z);
        if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 60f)) return false;
        if (!IsGround(hit.collider.gameObject)) return false;
        if (Physics.CheckSphere(hit.point + Vector3.up * (PropClearance + 0.2f), PropClearance, ~0, QueryTriggerInteraction.Ignore)) return false;

        GameObject prefab = prefabs[rng.Next(prefabs.Length)];
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        float scale = minScale + (float)rng.NextDouble() * (maxScale - minScale);
        go.transform.SetPositionAndRotation(hit.point, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
        go.transform.localScale = Vector3.one * scale;
        foreach (Collider col in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col); // walk straight through
        return true;
    }

    private static bool IsGround(GameObject go) => go.name == "Ground" || go.name.StartsWith("Ground (");

    private static Vector3 RandomSpot(System.Random rng, Bounds ground, float margin)
    {
        float x = Mathf.Lerp(ground.min.x + margin, ground.max.x - margin, (float)rng.NextDouble());
        float z = Mathf.Lerp(ground.min.z + margin, ground.max.z - margin, (float)rng.NextDouble());
        return new Vector3(x, 0f, z);
    }

    private static GameObject[] LoadPrefabs(string[] names)
    {
        List<GameObject> list = new List<GameObject>();
        foreach (string name in names)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>($"{GrassFolder}/{name}.prefab");
            if (go != null) list.Add(go);
            else Debug.LogWarning($"GrassScatter: missing prefab {GrassFolder}/{name}.prefab");
        }
        return list.ToArray();
    }
}
