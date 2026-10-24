using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds a ring of Fence.fbx segments along the border of the main ground. Idempotent: it rebuilds the "Fence Ring"
/// group under "--- ENVIRONMENT ---", so re-running gives the same layout. A segment is skipped where terrain or a hill
/// covers the border, so the fence appears to run into it.
/// </summary>
public static class FenceRingBuilder
{
    private const string FencePath = "Assets/Art/Meshes/Fence.fbx";
    private const string EnvironmentName = "--- ENVIRONMENT ---";
    private const string GroundPath = "Terrain/Ground";
    private const string GroupName = "Fence Ring";

    // Matches the hand-placed fences in the scene: scale 1.57, resting on the ground.
    private const float FenceScale = 1.57f;
    private const float FenceLift = 0.37f;
    private const float BorderInset = 0.25f;     // keeps the posts on the plane instead of on its very edge
    private const float BuriedHeight = 0.4f;     // a surface this far above the ground hides the fence

    // The fence mesh in root space (see Fence.fbx): about 0.92 m long along Z, 0.5 m tall, centred on the pivot.
    private static readonly Vector3 ColliderCenter = new Vector3(0.02f, 0f, -0.03f);
    private static readonly Vector3 ColliderSize = new Vector3(0.1f, 0.5f, 0.92f);
    private const float SegmentLength = 0.92f * FenceScale;

    [MenuItem("MoFarmSand/Build Ground Fence")]
    public static void Build()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FencePath);
        if (prefab == null) { Debug.LogError("FenceRing: missing " + FencePath); return; }

        GameObject env = GameObject.Find(EnvironmentName);
        Transform ground = env != null ? env.transform.Find(GroundPath) : null;
        Renderer groundRenderer = ground != null ? ground.GetComponent<Renderer>() : null;
        if (groundRenderer == null) { Debug.LogError($"FenceRing: no Ground renderer at {EnvironmentName}/{GroundPath}"); return; }
        Bounds gb = groundRenderer.bounds;

        Transform old = env.transform.Find(GroupName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Build Ground Fence");
        group.transform.SetParent(env.transform, false);

        Physics.SyncTransforms();

        float minX = gb.min.x + BorderInset, maxX = gb.max.x - BorderInset;
        float minZ = gb.min.z + BorderInset, maxZ = gb.max.z - BorderInset;
        float top = gb.max.y;
        int count = 0;

        // Sides run along X (yaw 90) or along Z (yaw 0); spacing is stretched a little so the segments meet at the corners.
        count += Side(prefab, group.transform, new Vector3(minX, top, minZ), new Vector3(maxX, top, minZ), 90f);
        count += Side(prefab, group.transform, new Vector3(minX, top, maxZ), new Vector3(maxX, top, maxZ), 90f);
        count += Side(prefab, group.transform, new Vector3(minX, top, minZ), new Vector3(minX, top, maxZ), 0f);
        count += Side(prefab, group.transform, new Vector3(maxX, top, minZ), new Vector3(maxX, top, maxZ), 0f);

        EditorSceneManager.MarkSceneDirty(group.scene);
        Debug.Log($"FenceRing: placed {count} fence segments under {EnvironmentName}/{GroupName}.");
    }

    private static int Side(GameObject prefab, Transform parent, Vector3 from, Vector3 to, float yaw)
    {
        float length = Vector3.Distance(from, to);
        int segments = Mathf.Max(1, Mathf.RoundToInt(length / SegmentLength));
        float step = length / segments;
        float stretch = step / SegmentLength;
        Vector3 dir = (to - from).normalized;
        int placed = 0;

        for (int i = 0; i < segments; i++)
        {
            Vector3 p = from + dir * (step * (i + 0.5f));

            // Buried under a hill or mound: leave a gap so the fence seems to run into it.
            Vector3 origin = new Vector3(p.x, from.y + 30f, p.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)
                && hit.point.y > from.y + BuriedHeight) continue;

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(new Vector3(p.x, from.y + FenceLift, p.z), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = new Vector3(FenceScale, FenceScale, FenceScale * stretch);

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = ColliderCenter;
            box.size = ColliderSize;
            placed++;
        }
        return placed;
    }
}
