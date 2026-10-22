using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Wires the winter tree swap into the open scene: pairs every tree under "--- ENVIRONMENT ---/Trees" with its snow twin
/// (Pine_Tree_OneSided_m_01_LOD -> Pine_Tree_OneSided_m_Snow_01_LOD) and turns the winter season look on. Idempotent.
/// </summary>
public static class SeasonTreesBuilder
{
    private const string SnowFolder = "Assets/Low Poly Trees Pack/Tree Assets/Prefabs/Trees_Snow/LOD/No_Bottoms/Pine_Trees/OneSided";
    private const string TreesPath = "--- ENVIRONMENT ---/Trees";
    private const string WeatherMapPath = "Assets/Data/Presentation/CozyWeatherMap.asset";

    [MenuItem("MoFarmSand/Wire Winter Trees")]
    public static void Wire()
    {
        GameObject trees = GameObject.Find(TreesPath);
        CozyBridge bridge = Object.FindFirstObjectByType<CozyBridge>();
        if (trees == null || bridge == null) { Debug.LogError("SeasonTrees: need '" + TreesPath + "' and a CozyBridge in the open scene."); return; }

        SeasonTrees component = bridge.GetComponent<SeasonTrees>();
        if (component == null) component = Undo.AddComponent<SeasonTrees>(bridge.gameObject);

        List<(GameObject tree, GameObject snow)> pairs = new List<(GameObject, GameObject)>();
        List<string> missing = new List<string>();
        // The "Trees" group plus any pine dropped loose at the scene root.
        List<Transform> candidates = new List<Transform>();
        foreach (Transform child in trees.transform) candidates.Add(child);
        foreach (GameObject root in bridge.gameObject.scene.GetRootGameObjects())
            if (Regex.IsMatch(root.name, @"^Pine_Tree_OneSided_[a-z]+_\d+_LOD")) candidates.Add(root.transform);

        foreach (Transform child in candidates)
        {
            // Names like "Pine_Tree_OneSided_m_09_LOD (1)": drop the copy suffix, then add _Snow before the number.
            string baseName = Regex.Replace(child.name, @"\s*\(\d+\)$", "");
            string snowName = Regex.Replace(baseName, @"_(\d+)_LOD$", "_Snow_$1_LOD");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{SnowFolder}/{snowName}.prefab");
            if (prefab == null) { missing.Add(child.name); continue; }
            pairs.Add((child.gameObject, prefab));
        }

        Undo.RecordObject(component, "Wire Winter Trees");
        SerializedObject so = new SerializedObject(component);
        SerializedProperty swaps = so.FindProperty("swaps");
        swaps.arraySize = pairs.Count;
        for (int i = 0; i < pairs.Count; i++)
        {
            SerializedProperty e = swaps.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("tree").objectReferenceValue = pairs[i].tree;
            e.FindPropertyRelative("snowPrefab").objectReferenceValue = pairs[i].snow;
        }
        so.ApplyModifiedProperties();

        SerializedObject bridgeSo = new SerializedObject(bridge);
        bridgeSo.FindProperty("seasonTrees").objectReferenceValue = component;
        bridgeSo.ApplyModifiedProperties();

        EnableWinterLook();
        EditorSceneManager.MarkSceneDirty(bridge.gameObject.scene);
        Debug.Log($"SeasonTrees: wired {pairs.Count} tree swaps" + (missing.Count > 0 ? $", no snow twin for: {string.Join(", ", missing)}" : "."));
    }

    private static void EnableWinterLook()
    {
        CozyWeatherMap map = AssetDatabase.LoadAssetAtPath<CozyWeatherMap>(WeatherMapPath);
        if (map == null) { Debug.LogWarning("SeasonTrees: " + WeatherMapPath + " not found; tick snowyTrees on the winter look by hand."); return; }
        for (int i = 0; i < map.seasonLooks.Count; i++)
        {
            CozyWeatherMap.SeasonLook look = map.seasonLooks[i];
            if (look.season == null || look.season.seasonId != "winter") continue;
            look.snowyTrees = true;
            map.seasonLooks[i] = look;
        }
        EditorUtility.SetDirty(map);
        AssetDatabase.SaveAssets();
    }
}
