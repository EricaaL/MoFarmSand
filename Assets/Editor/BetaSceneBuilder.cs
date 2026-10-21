using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Makes the beta test scene: a copy of the Golden Slice scene whose RunDirector is in beta mode
/// (hidden save slot, free-roam map). Safe to re-run: an existing beta scene is only re-flagged, never re-copied.
/// </summary>
public static class BetaSceneBuilder
{
    private const string SourcePath = "Assets/Scenes/GoldenSliceScene.unity";
    private const string BetaPath = "Assets/Scenes/BetaTestScene.unity";

    [MenuItem("MoFarmSand/Build Beta Test Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.SaveAssets();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BetaPath) == null)
        {
            if (!AssetDatabase.CopyAsset(SourcePath, BetaPath))
            {
                Debug.LogError($"[BetaSceneBuilder] Could not copy {SourcePath} to {BetaPath}.");
                return;
            }
            AssetDatabase.Refresh();
        }

        string previous = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(BetaPath, OpenSceneMode.Single);

        RunDirector director = Object.FindFirstObjectByType<RunDirector>();
        if (director == null)
        {
            Debug.LogError("[BetaSceneBuilder] No RunDirector in the scene. Run MoFarmSand > Add Run Systems on the Golden Slice scene first.");
            return;
        }
        SerializedObject so = new SerializedObject(director);
        so.FindProperty("betaMode").boolValue = true;
        so.FindProperty("runMode").enumValueIndex = (int)RunMode.Roguelike;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, BetaPath);

        AddToBuildSettings(BetaPath);

        // Leave the editor on the scene the user was in.
        if (!string.IsNullOrEmpty(previous) && previous != BetaPath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log($"[BetaSceneBuilder] Beta test scene ready: {BetaPath} (save slot {SaveService.BetaSlot}, file {SaveService.NameOf(SaveService.BetaSlot)}).");
    }

    private static void AddToBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
