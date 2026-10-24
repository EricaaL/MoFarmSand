using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds the trailer ending to the open scene: the spot where the dog lies down in front of the farm tiles, the
/// CM_Epilogue camera and a <see cref="TrailerEpilogue"/> next to the <see cref="TrailerDirector"/>, which plays it after
/// its last shot. Additive and idempotent; re-running only re-links references, it does not move a spot or camera you placed.
/// </summary>
public static class TrailerEpilogueBuilder
{
    private const string SpotName = "EpilogueMark";
    private const string CameraName = "CM_Epilogue";

    [MenuItem("MoFarmSand/Add Trailer Epilogue")]
    public static void AddTrailerEpilogue()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        TrailerDirector trailer = Object.FindFirstObjectByType<TrailerDirector>(FindObjectsInactive.Include);
        RunDirector director = Object.FindFirstObjectByType<RunDirector>(FindObjectsInactive.Include);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (trailer == null || director == null || player == null || director.Db == null)
        {
            Debug.LogError("[TrailerEpilogueBuilder] Needs a TrailerDirector, a RunDirector with a GameDatabase and a Player in the open scene.");
            return;
        }

        Transform cinematics = GetOrCreateRoot("--- CINEMATICS ---");
        GetPlacement(player.transform.position, out Vector3 spotPos, out Quaternion spotRot);

        bool newSpot = cinematics.Find(SpotName) == null;
        GameObject spot = GetOrCreate(SpotName, cinematics);
        if (newSpot) spot.transform.SetPositionAndRotation(spotPos, spotRot);

        bool newCam = cinematics.Find(CameraName) == null;
        GameObject camGo = GetOrCreate(CameraName, cinematics);
        CinemachineCamera vcam = Ensure<CinemachineCamera>(camGo);
        vcam.Priority.Enabled = true;
        vcam.Priority.Value = 0;   // TrailerEpilogue raises it when it plays
        if (newCam)
        {
            // Behind the dog, low, looking out over it at the farm tiles and the barn beyond.
            Vector3 toField = spot.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, toField);
            Vector3 camPos = spot.transform.position - toField * 3.2f + side * -1f + Vector3.up * 0.8f;
            GameObject barn = GameObject.Find("BarnHouse");
            Vector3 target = barn != null ? barn.transform.position + Vector3.up * 3f : spot.transform.position + toField * 12f;
            camGo.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(target - camPos));
        }

        TrailerEpilogue epilogue = Ensure<TrailerEpilogue>(trailer.gameObject);
        GameDatabase db = director.Db;
        SerializedObject so = new SerializedObject(epilogue);
        so.FindProperty("lieSpot").objectReferenceValue = spot.transform;
        so.FindProperty("epilogueCamera").objectReferenceValue = vcam;
        so.FindProperty("cozyBridge").objectReferenceValue = Object.FindFirstObjectByType<CozyBridge>(FindObjectsInactive.Include);
        so.FindProperty("lighting").objectReferenceValue = Object.FindFirstObjectByType<DayNightLighting>(FindObjectsInactive.Include);
        so.FindProperty("db").objectReferenceValue = db;

        // Summer, then spring, autumn and winter, and back to summer.
        string[] order = { "summer", "spring", "autumn", "winter" };
        SerializedProperty list = so.FindProperty("timelapse");
        list.arraySize = order.Length;
        for (int i = 0; i < order.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = FindSeason(db, order[i]);
        so.FindProperty("wakeSeason").objectReferenceValue = FindSeason(db, "summer");
        so.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject directorSo = new SerializedObject(trailer);
        directorSo.FindProperty("epilogue").objectReferenceValue = epilogue;
        directorSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(trailer.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[TrailerEpilogueBuilder] Trailer epilogue added. Move EpilogueMark and CM_Epilogue to taste; press E in Play mode to preview.");
    }

    private static SeasonData FindSeason(GameDatabase db, string key)
    {
        SeasonData season = db.calendar.seasons.FirstOrDefault(s => s != null && s.name.ToLowerInvariant().Contains(key));
        if (season == null) Debug.LogWarning("[TrailerEpilogueBuilder] No season named like '" + key + "' in the calendar.");
        return season;
    }

    /// <summary>On the player's side of the tiles, just outside them, facing the field.</summary>
    private static void GetPlacement(Vector3 playerPos, out Vector3 pos, out Quaternion rot)
    {
        FarmTile[] tiles = Object.FindObjectsByType<FarmTile>(FindObjectsSortMode.None);
        Vector3 center = Vector3.zero;
        foreach (FarmTile t in tiles) center += t.transform.position;
        center = tiles.Length > 0 ? center / tiles.Length : playerPos + Vector3.forward * 4f;

        float reach = 0f;
        foreach (FarmTile t in tiles)
        {
            Vector3 d = t.transform.position - center; d.y = 0f;
            reach = Mathf.Max(reach, d.magnitude);
        }

        Vector3 away = playerPos - center; away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
        pos = center + away * (reach + 2f);
        pos.y = playerPos.y;
        rot = Quaternion.LookRotation(-away);
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
}
