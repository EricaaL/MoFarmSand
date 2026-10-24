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

    [MenuItem("MoFarmSand/Scatter Grass")]
    public static void Scatter()
    {
        GameObject[] prefabs = LoadPrefabs();
        if (prefabs.Length == 0) { Debug.LogError("GrassScatter: no prefabs found in " + GrassFolder); return; }

        GameObject ground = GameObject.Find(EnvironmentName + "/Ground");
        Renderer groundRenderer = ground != null ? ground.GetComponent<Renderer>() : null;
        if (groundRenderer == null) { Debug.LogError("GrassScatter: no Ground renderer under " + EnvironmentName); return; }
        Bounds gb = groundRenderer.bounds;

        GameObject env = GameObject.Find(EnvironmentName);
        Transform old = env != null ? env.transform.Find(GroupName) : null;
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Scatter Grass");
        if (env != null) group.transform.SetParent(env.transform, false);

        // Colliders must be current for the overlap checks to see props moved since the last physics step.
        Physics.SyncTransforms();

        System.Random rng = new System.Random(Seed);
        float margin = 1.5f;
        int placed = 0;

        for (int c = 0; c < Clumps; c++)
        {
            Vector3 center = RandomSpot(rng, gb, margin);
            for (int b = 0; b < BladesPerClump; b++)
            {
                double a = rng.NextDouble() * Mathf.PI * 2.0;
                float r = ClumpRadius * (float)(rng.NextDouble() * rng.NextDouble()); // dense in the middle
                Vector3 p = center + new Vector3((float)System.Math.Cos(a) * r, 0f, (float)System.Math.Sin(a) * r);
                if (Place(rng, prefabs, group.transform, p, gb)) placed++;
            }
        }
        for (int i = 0; i < Stragglers; i++)
            if (Place(rng, prefabs, group.transform, RandomSpot(rng, gb, margin), gb)) placed++;

        EditorSceneManager.MarkSceneDirty(group.scene);
        Debug.Log($"GrassScatter: placed {placed} grass pieces under {EnvironmentName}/{GroupName}.");
    }

    private static bool Place(System.Random rng, GameObject[] prefabs, Transform parent, Vector3 p, Bounds ground)
    {
        if (p.x < ground.min.x || p.x > ground.max.x || p.z < ground.min.z || p.z > ground.max.z) return false;
        if (FieldKeepClear.Contains(new Vector2(p.x, p.z))) return false;

        // Drop onto the ground surface; skip anything that lands on a prop, tree or building instead.
        Vector3 from = new Vector3(p.x, ground.max.y + 20f, p.z);
        if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 60f)) return false;
        if (hit.collider.gameObject.name != "Ground") return false;
        if (Physics.CheckSphere(hit.point + Vector3.up * (PropClearance + 0.2f), PropClearance, ~0, QueryTriggerInteraction.Ignore)) return false;

        GameObject prefab = prefabs[rng.Next(prefabs.Length)];
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        float scale = MinScale + (float)rng.NextDouble() * (MaxScale - MinScale);
        go.transform.SetPositionAndRotation(hit.point, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
        go.transform.localScale = Vector3.one * scale;
        foreach (Collider col in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col); // walk straight through
        return true;
    }

    private static Vector3 RandomSpot(System.Random rng, Bounds ground, float margin)
    {
        float x = Mathf.Lerp(ground.min.x + margin, ground.max.x - margin, (float)rng.NextDouble());
        float z = Mathf.Lerp(ground.min.z + margin, ground.max.z - margin, (float)rng.NextDouble());
        return new Vector3(x, 0f, z);
    }

    private static GameObject[] LoadPrefabs()
    {
        List<GameObject> list = new List<GameObject>();
        foreach (string name in PrefabNames)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>($"{GrassFolder}/{name}.prefab");
            if (go != null) list.Add(go);
            else Debug.LogWarning($"GrassScatter: missing prefab {GrassFolder}/{name}.prefab");
        }
        return list.ToArray();
    }
}
