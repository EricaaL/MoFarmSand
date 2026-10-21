using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Additive scene setup for camera-ready polish. Idempotent: finds objects by name, never wipes the scene (COZY stays).</summary>
public static class Batch2Builder
{
    private const string VolumePath = "Assets/Settings/Volume_Night.asset";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player_Primitive.prefab";

    [MenuItem("MoFarmSand/Add Batch 2 to Open Scene")]
    public static void AddBatch2()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        EnsureShieldOnPlayerPrefab();
        VolumeProfile profile = EnsureNightProfile();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        BossHunger hunger = Object.FindFirstObjectByType<BossHunger>(FindObjectsInactive.Include);
        Camera cam = Camera.main;
        if (player == null || hunger == null || cam == null)
        {
            Debug.LogError("[Batch2Builder] Needs Player, Caterpillar and Main Camera in the open scene.");
            return;
        }

        Transform lightingGroup = Find("--- LIGHTING ---");
        Transform cinematics = GetOrCreateRoot("--- CINEMATICS ---");
        Transform managers = Find("--- MANAGERS ---");

        // ---- Night volume + post-processing ----
        UniversalAdditionalCameraData camData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;

        GameObject volGo = GetOrCreate("Volume_Night", lightingGroup);
        Volume volume = Ensure<Volume>(volGo);
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        volume.weight = 0f;
        volume.priority = 10f;

        DayNightLighting lighting = Object.FindFirstObjectByType<DayNightLighting>();
        if (lighting != null) SetRef(lighting, "nightVolume", volume);

        // ---- Cinemachine gameplay camera ----
        CinemachineBrain brain = Ensure<CinemachineBrain>(cam.gameObject);
        AnimalCrossingCamera oldCam = cam.GetComponent<AnimalCrossingCamera>();
        if (oldCam != null) oldCam.enabled = false;

        GameObject gameplayGo = GetOrCreate("CM_Gameplay", cinematics);
        CinemachineCamera gameplay = Ensure<CinemachineCamera>(gameplayGo);
        gameplay.Priority.Enabled = true;
        gameplay.Priority.Value = 10;
        gameplay.Target.TrackingTarget = player.transform;
        CinemachineFollow follow = Ensure<CinemachineFollow>(gameplayGo);
        follow.FollowOffset = new Vector3(0f, 9f, -10f);
        follow.TrackerSettings.PositionDamping = new Vector3(0.3f, 0.3f, 0.3f);
        Ensure<CinemachineImpulseListener>(gameplayGo);
        gameplayGo.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
        gameplayGo.transform.position = player.transform.position + follow.FollowOffset;

        // ---- Trailer shot cameras ----
        CinemachineCamera orbit = ShotCamera("CM_FarmOrbit", cinematics, player.transform, player.transform);
        CinemachineOrbitalFollow orbitFollow = Ensure<CinemachineOrbitalFollow>(orbit.gameObject);
        orbitFollow.Radius = 9f;
        orbitFollow.VerticalAxis.Value = 25f;
        Ensure<CinemachineRotationComposer>(orbit.gameObject);

        CinemachineCamera intro = ShotCamera("CM_BossIntro", cinematics, hunger.transform, hunger.transform);
        intro.transform.position = hunger.transform.position + new Vector3(4f, 0.5f, -7f);
        Ensure<CinemachineRotationComposer>(intro.gameObject);

        CinemachineCamera burst = ShotCamera("CM_BurstCloseup", cinematics, hunger.transform, hunger.transform);
        CinemachineFollow burstFollow = Ensure<CinemachineFollow>(burst.gameObject);
        burstFollow.FollowOffset = new Vector3(4f, 2f, -5f);
        burstFollow.TrackerSettings.PositionDamping = Vector3.zero;
        Ensure<CinemachineRotationComposer>(burst.gameObject);

        // ---- Director ----
        GameObject dirGo = GetOrCreate("TrailerDirector", managers);
        TrailerDirector director = Ensure<TrailerDirector>(dirGo);
        SerializedObject dso = new SerializedObject(director);
        SerializedProperty seq = dso.FindProperty("sequence");
        seq.arraySize = 2;
        SetShot(seq.GetArrayElementAtIndex(0), orbit, 5f);
        SetShot(seq.GetArrayElementAtIndex(1), intro, 4f);
        dso.FindProperty("burstCamera").objectReferenceValue = burst;
        dso.ApplyModifiedPropertiesWithoutUndo();

        // ---- Feedback ----
        GameObject fbGo = GetOrCreate("GameFeedback", managers);
        GameFeedback feedback = Ensure<GameFeedback>(fbGo);
        SerializedObject fso = new SerializedObject(feedback);
        fso.FindProperty("hunger").objectReferenceValue = hunger;
        fso.FindProperty("playerHealth").objectReferenceValue = player.GetComponent<PlayerHealth>();
        fso.FindProperty("trailer").objectReferenceValue = director;
        Renderer[] rs = hunger.GetComponentsInChildren<Renderer>(true);
        SerializedProperty rp = fso.FindProperty("bossRenderers");
        rp.arraySize = rs.Length;
        for (int i = 0; i < rs.Length; i++) rp.GetArrayElementAtIndex(i).objectReferenceValue = rs[i];
        fso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(brain.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Batch2Builder] Batch 2 added to the open scene.");
    }

    // ---------- helpers ----------

    private static CinemachineCamera ShotCamera(string name, Transform parent, Transform follow, Transform lookAt)
    {
        GameObject go = GetOrCreate(name, parent);
        CinemachineCamera c = Ensure<CinemachineCamera>(go);
        c.Priority.Enabled = true;
        c.Priority.Value = 0;
        c.Target.TrackingTarget = follow;
        c.Target.LookAtTarget = lookAt;
        c.Target.CustomLookAtTarget = lookAt != follow;
        return c;
    }

    private static void SetShot(SerializedProperty shot, CinemachineCamera cam, float seconds)
    {
        shot.FindPropertyRelative("camera").objectReferenceValue = cam;
        shot.FindPropertyRelative("seconds").floatValue = seconds;
    }

    private static void EnsureShieldOnPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root.GetComponent<ShieldVisual>() == null) root.AddComponent<ShieldVisual>();
        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static VolumeProfile EnsureNightProfile()
    {
        VolumeProfile existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
        if (existing != null) return existing;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, VolumePath);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.4f);
        vignette.smoothness.Override(0.5f);
        vignette.color.Override(new Color(0.15f, 0.05f, 0.3f));

        ColorAdjustments adj = profile.Add<ColorAdjustments>(true);
        adj.postExposure.Override(-0.3f);
        adj.saturation.Override(-20f);
        adj.colorFilter.Override(new Color(0.7f, 0.65f, 1f));

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.6f);
        bloom.threshold.Override(0.9f);

        AssetDatabase.AddObjectToAsset(vignette, profile);
        AssetDatabase.AddObjectToAsset(adj, profile);
        AssetDatabase.AddObjectToAsset(bloom, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static Transform Find(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.transform : GetOrCreateRoot(name);
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
        if (p == null) { Debug.LogError($"[Batch2Builder] Missing field {field} on {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
