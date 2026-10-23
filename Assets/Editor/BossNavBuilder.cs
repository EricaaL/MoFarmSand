using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Additive, idempotent: bakes the NavMesh the caterpillar boss reads its route from.
/// It uses the real physics colliders (barn, trees, props) so the route matches what blocks CaterpillarBoss.Move.
/// The player and boss are switched off while baking so they don't punch holes in the mesh.
/// </summary>
public static class BossNavBuilder
{
    private const string SurfaceName = "BossNavMesh";
    private const string ManagersName = "--- MANAGERS ---";
    private const string DataFolder = "Assets/Scenes/GoldenSliceScene";

    [MenuItem("MoFarmSand/Bake Boss NavMesh")]
    public static void Bake()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        GameObject managers = GameObject.Find(ManagersName);
        Transform surfaceTf = managers != null ? managers.transform.Find(SurfaceName) : null;
        GameObject go;
        if (surfaceTf != null) go = surfaceTf.gameObject;
        else
        {
            go = new GameObject(SurfaceName);
            Undo.RegisterCreatedObjectUndo(go, "Create BossNavMesh");
            if (managers != null) go.transform.SetParent(managers.transform, false);
        }

        NavMeshSurface surface = go.GetComponent<NavMeshSurface>();
        if (surface == null) surface = Undo.AddComponent<NavMeshSurface>(go);
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;

        List<GameObject> paused = new List<GameObject>();
        Pause("Player_Primitive", paused);
        Pause("Caterpillar_Primitive", paused);
        try
        {
            if (surface.navMeshData != null)
            {
                string old = AssetDatabase.GetAssetPath(surface.navMeshData);
                surface.RemoveData();
                if (!string.IsNullOrEmpty(old)) AssetDatabase.DeleteAsset(old);
            }
            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets/Scenes", "GoldenSliceScene");
                AssetDatabase.CreateAsset(surface.navMeshData, $"{DataFolder}/NavMesh-{SurfaceName}.asset");
            }
            else Debug.LogError("[BossNavBuilder] NavMesh build produced no data.");
        }
        finally
        {
            foreach (GameObject p in paused) if (p != null) p.SetActive(true);
        }

        EditorUtility.SetDirty(surface);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[BossNavBuilder] Boss NavMesh baked.");
    }

    private static void Pause(string objectName, List<GameObject> paused)
    {
        GameObject g = GameObject.Find(objectName);
        if (g == null || !g.activeSelf) return;
        g.SetActive(false);
        paused.Add(g);
    }
}
