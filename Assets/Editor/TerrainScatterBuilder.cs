using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Scatters the Low Poly Trees Pack terrain chunks and hills along the inner edge of the square ground so the border
/// reads as a rolling rim. Idempotent: it rebuilds the "Edge Terrain" group under "--- ENVIRONMENT ---" from a fixed
/// seed, so re-running gives the same layout. Pieces stay out of the farm field and off props, trees and each other.
/// </summary>
public static class TerrainScatterBuilder
{
    private const string PackFolder = "Assets/Low Poly Trees Pack/Bonus Assets/Prefabs";
    private const string TerrainFolder = PackFolder + "/Terrain";
    private const string HillsFolder = PackFolder + "/Hills";
    private const string EnvironmentName = "--- ENVIRONMENT ---";
    private const string GroupName = "Edge Terrain";

    private const int Seed = 4113;
    private const float Step = 9f;
    private const float EdgeInset = 1f;           // nearest a piece centre gets to the ground's edge
    private static readonly Vector2 HillDepth = new Vector2(5f, 10f);     // distance in from the edge
    private static readonly Vector2 TerrainDepth = new Vector2(9f, 15f);
    private static readonly Vector2 HillFootprint = new Vector2(18f, 28f);   // target size on the ground (m)
    private static readonly Vector2 TerrainFootprint = new Vector2(10f, 18f);
    private const float ScaleJitter = 0.15f;
    private const float Sink = 0.05f;
    private const float PropClearance = 0.5f;
    private const float OverlapFactor = 0.55f;    // centres closer than (r1 + r2) * this are rejected

    // Field footprint plus room for extra farmland, the merchant and the bell; nothing is placed in here.
    private static readonly Rect KeepClear = new Rect(-10f, -4.5f, 20f, 19.5f);

    private struct Piece
    {
        public GameObject prefab;
        public bool hill;
        public Vector3 size;    // mesh bounds size
        public float minY;      // mesh bounds bottom, so the base can sit on the ground
    }

    [MenuItem("MoFarmSand/Scatter Edge Terrain")]
    public static void Scatter()
    {
        List<Piece> hills = Load(HillsFolder, true);
        List<Piece> terrain = Load(TerrainFolder, false);
        if (hills.Count == 0 && terrain.Count == 0) { Debug.LogError("TerrainScatter: no prefabs found under " + PackFolder); return; }

        GameObject env = GameObject.Find(EnvironmentName);
        Transform ground = env != null ? env.transform.Find("Ground") : null;
        Renderer groundRenderer = ground != null ? ground.GetComponent<Renderer>() : null;
        if (groundRenderer == null) { Debug.LogError("TerrainScatter: no Ground renderer under " + EnvironmentName); return; }
        Bounds gb = groundRenderer.bounds;

        Transform old = env.transform.Find(GroupName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Scatter Edge Terrain");
        group.transform.SetParent(env.transform, false);

        Physics.SyncTransforms();

        System.Random rng = new System.Random(Seed);
        List<Vector4> placed = new List<Vector4>(); // x, z, radius
        int count = 0;

        // Walk the four sides; every step tries one hill (outer) and one terrain chunk (inner).
        for (int side = 0; side < 4; side++)
        {
            float length = (side < 2 ? gb.size.x : gb.size.z) - 2f * EdgeInset;
            for (float t = 0f; t <= length; t += Step)
            {
                float along = t + ((float)rng.NextDouble() - 0.5f) * Step * 0.6f;
                if (hills.Count > 0 && TryPlace(rng, hills, group.transform, gb, side, along, HillDepth, HillFootprint, placed)) count++;
                if (terrain.Count > 0 && TryPlace(rng, terrain, group.transform, gb, side, along, TerrainDepth, TerrainFootprint, placed)) count++;
            }
        }

        EditorSceneManager.MarkSceneDirty(group.scene);
        Debug.Log($"TerrainScatter: placed {count} pieces under {EnvironmentName}/{GroupName}.");
    }

    private static bool TryPlace(System.Random rng, List<Piece> pieces, Transform parent, Bounds ground, int side,
        float along, Vector2 depthRange, Vector2 footprintRange, List<Vector4> placed)
    {
        Piece piece = pieces[rng.Next(pieces.Count)];
        float depth = Mathf.Lerp(depthRange.x, depthRange.y, (float)rng.NextDouble());
        float x, z;
        switch (side)
        {
            case 0: x = ground.min.x + EdgeInset + along; z = ground.min.z + depth; break;   // south
            case 1: x = ground.min.x + EdgeInset + along; z = ground.max.z - depth; break;   // north
            case 2: x = ground.min.x + depth; z = ground.min.z + EdgeInset + along; break;   // west
            default: x = ground.max.x - depth; z = ground.min.z + EdgeInset + along; break;  // east
        }
        if (x < ground.min.x + EdgeInset || x > ground.max.x - EdgeInset || z < ground.min.z + EdgeInset || z > ground.max.z - EdgeInset) return false;

        float footprint = Mathf.Lerp(footprintRange.x, footprintRange.y, (float)rng.NextDouble());
        float scale = footprint / Mathf.Max(0.01f, Mathf.Max(piece.size.x, piece.size.z));
        scale *= 1f + ((float)rng.NextDouble() * 2f - 1f) * ScaleJitter;
        float radius = Mathf.Max(piece.size.x, piece.size.z) * scale * 0.5f;

        // Keep the farm clear (including the piece's own reach), and don't crowd neighbours.
        Rect clear = new Rect(KeepClear.xMin - radius * 0.4f, KeepClear.yMin - radius * 0.4f, KeepClear.width + radius * 0.8f, KeepClear.height + radius * 0.8f);
        if (clear.Contains(new Vector2(x, z))) return false;
        foreach (Vector4 o in placed)
        {
            float dx = o.x - x, dz = o.y - z;
            float min = (o.z + radius) * OverlapFactor;
            if (dx * dx + dz * dz < min * min) return false;
        }

        // Drop onto the ground; skip anything that lands on a prop, tree or building instead.
        Vector3 from = new Vector3(x, ground.max.y + 20f, z);
        if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 60f)) return false;
        if (hit.collider.gameObject.name != "Ground") return false;
        float clearRadius = Mathf.Clamp(radius * 0.3f, PropClearance, 3f);
        if (Physics.CheckSphere(hit.point + Vector3.up * (clearRadius + 0.2f), clearRadius, ~0, QueryTriggerInteraction.Ignore)) return false;

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(piece.prefab, parent);
        float y = hit.point.y - piece.minY * scale - Sink;
        go.transform.SetPositionAndRotation(new Vector3(hit.point.x, y, hit.point.z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
        go.transform.localScale = Vector3.one * scale;
        placed.Add(new Vector4(x, z, radius, 0f));
        Physics.SyncTransforms(); // later raycasts and clearance checks see this piece
        return true;
    }

    private static List<Piece> Load(string folder, bool hill)
    {
        List<Piece> list = new List<Piece>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            bool any = false;
            Bounds b = new Bounds();
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Bounds w = new Bounds(mf.transform.TransformPoint(mb.center), Vector3.Scale(mb.size, mf.transform.lossyScale));
                if (!any) { b = w; any = true; } else b.Encapsulate(w);
            }
            if (!any) { Debug.LogWarning("TerrainScatter: no mesh in " + path); continue; }
            list.Add(new Piece { prefab = prefab, hill = hill, size = b.size, minY = b.min.y });
        }
        return list;
    }
}
