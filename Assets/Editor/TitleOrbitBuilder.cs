using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds the title-screen panorama to the open scene: a pivot at the middle of the starting field and a CM_TitleOrbit camera
/// that circles it (see <see cref="TitleOrbit"/>), then hands it to the RunDirector. Additive and idempotent.
/// </summary>
public static class TitleOrbitBuilder
{
    [MenuItem("MoFarmSand/Add Title Orbit")]
    public static void AddTitleOrbit()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        RunDirector director = Object.FindFirstObjectByType<RunDirector>(FindObjectsInactive.Include);
        if (director == null)
        {
            Debug.LogError("[TitleOrbitBuilder] Needs a RunDirector in the open scene.");
            return;
        }

        Transform cinematics = GetOrCreateRoot("--- CINEMATICS ---");

        GameObject pivotGo = GetOrCreate("TitleOrbitPivot", cinematics);
        pivotGo.transform.position = FieldCenter();

        GameObject camGo = GetOrCreate("CM_TitleOrbit", cinematics);
        CinemachineCamera vcam = Ensure<CinemachineCamera>(camGo);
        vcam.Priority.Enabled = true;
        vcam.Priority.Value = 0;   // TitleOrbit raises it while a title screen is up
        TitleOrbit orbit = Ensure<TitleOrbit>(camGo);
        SetRef(orbit, "pivot", pivotGo.transform);
        SerializedObject orbitSo = new SerializedObject(orbit);
        orbitSo.FindProperty("height").floatValue = 13f;   // above the barn roof and the treetops
        orbitSo.FindProperty("radius").floatValue = 16f;
        orbitSo.ApplyModifiedPropertiesWithoutUndo();
        SetRef(director, "titleOrbit", orbit);

        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[TitleOrbitBuilder] Title orbit camera added.");
    }

    /// <summary>Middle of the farm tiles (or the origin if the scene has none).</summary>
    private static Vector3 FieldCenter()
    {
        FarmTile[] tiles = Object.FindObjectsByType<FarmTile>(FindObjectsSortMode.None);
        if (tiles.Length == 0) return Vector3.zero;
        Vector3 sum = Vector3.zero;
        foreach (FarmTile t in tiles) sum += t.transform.position;
        return sum / tiles.Length;
    }

    private static Transform GetOrCreateRoot(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }
        return go.transform;
    }

    private static GameObject GetOrCreate(string name, Transform parent)
    {
        Transform existing = parent != null ? parent.Find(name) : null;
        if (existing != null) return existing.gameObject;
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    private static void SetRef(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[TitleOrbitBuilder] Missing field {field} on {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
