using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Builds the Golden Slice data assets, prefabs and scene. Idempotent: existing assets are kept.</summary>
public static class GoldenSliceBuilder
{
    private const string DataDir = "Assets/Data";
    private const string MatDir = "Assets/Art/Materials";
    private const string PrefabDir = "Assets/Prefabs";
    private const string ScenePath = "Assets/Scenes/GoldenSliceScene.unity";

    [MenuItem("MoFarmSand/Build Golden Slice")]
    public static void Build() { BuildAll(false); }

    [MenuItem("MoFarmSand/Rebuild Golden Slice Scene (overwrite scene)")]
    public static void RebuildScene() { BuildAll(true); }

    private static void BuildAll(bool overwriteScene)
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        foreach (string d in new[] { DataDir, MatDir, PrefabDir, "Assets/Scenes" }) EnsureFolder(d);

        // ---- Materials ----
        Material soil = Mat("Soil", new Color(0.35f, 0.22f, 0.12f));
        Material grass = Mat("Grass", new Color(0.55f, 0.82f, 0.45f));
        Material dogBody = Mat("DogBody", new Color(0.85f, 0.65f, 0.4f));
        Material dogSnout = Mat("DogSnout", new Color(0.95f, 0.85f, 0.7f));
        Material tomatoMat = Mat("Tomato", new Color(0.9f, 0.2f, 0.15f));
        Material turnipMat = Mat("Turnip", new Color(0.8f, 0.6f, 0.85f));
        Material caterpillarMat = Mat("Caterpillar", new Color(0.5f, 0.85f, 0.3f));
        Material bellMat = Mat("Bell", new Color(1f, 0.85f, 0.2f));
        Material postMat = Mat("Post", new Color(0.45f, 0.3f, 0.2f));
        Material lineMat = Mat("TrajectoryLine", new Color(1f, 0.95f, 0.5f), "Universal Render Pipeline/Unlit");
        Material markerMat = Mat("LandingMarker", new Color(1f, 0.5f, 0.2f), "Universal Render Pipeline/Unlit");
        Material splatMat = Mat("SplatFX", Color.white, "Universal Render Pipeline/Particles/Unlit");   // white so FxTint can recolour the splat per crop
        Material burstMat = Mat("BurstFX", new Color(1f, 0.85f, 0.3f), "Universal Render Pipeline/Particles/Unlit");

        // ---- Particle prefabs ----
        GameObject splatFx = ParticlePrefab("Splat_FX", splatMat, SplatFallbackColor, 18, 0.18f, 4f);
        GameObject burstFx = ParticlePrefab("Burst_FX", burstMat, burstMat.color, 80, 0.4f, 9f);

        // ---- Projectile prefab ----
        GameObject tomatoProj = MakePrefab("Tomato_Projectile", () =>
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.localScale = Vector3.one * 0.4f;
            go.GetComponent<Renderer>().sharedMaterial = tomatoMat;
            go.AddComponent<Rigidbody>();
            go.AddComponent<CropProjectile>();
            SetRef(go.GetComponent<CropProjectile>(), "splatPrefab", splatFx);
            return go;
        });

        // ---- Data assets ----
        CropData tomato = Asset<CropData>($"{DataDir}/Crop_Tomato.asset", c =>
        {
            c.cropId = "tomato"; c.displayName = "Tomato"; c.type = CropType.Projectile;
            c.growTime = 11f; c.fullnessValue = 20f; c.color = tomatoMat.color; c.projectilePrefab = tomatoProj;
        });
        CropData turnip = Asset<CropData>($"{DataDir}/Crop_Turnip.asset", c =>
        {
            c.cropId = "turnip"; c.displayName = "Turnip"; c.type = CropType.Support;
            c.growTime = 13f; c.shieldCharges = 1; c.fullnessValue = 0f; c.color = turnipMat.color;
        });
        BossData bossData = Asset<BossData>($"{DataDir}/Boss_Caterpillar.asset", b =>
        {
            b.displayName = "Caterpillar"; b.maxFullness = 100f; b.chaseSpeed = 2.5f; b.damage = 1;
        });

        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");

        // ---- Player prefab ----
        GameObject playerPrefab = MakePrefab("Player_Primitive", () =>
        {
            GameObject root = new GameObject("Player") { tag = "Player" };
            CharacterController cc = root.AddComponent<CharacterController>();
            cc.height = 1.4f; cc.radius = 0.5f; cc.center = new Vector3(0f, 0.7f, 0f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body"; body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f); body.transform.localScale = Vector3.one * 1.2f;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = dogBody;

            GameObject snout = GameObject.CreatePrimitive(PrimitiveType.Cube);
            snout.name = "Snout"; snout.transform.SetParent(root.transform);
            snout.transform.localPosition = new Vector3(0f, 0.55f, 0.65f); snout.transform.localScale = new Vector3(0.35f, 0.3f, 0.45f);
            Object.DestroyImmediate(snout.GetComponent<Collider>());
            snout.GetComponent<Renderer>().sharedMaterial = dogSnout;

            GameObject origin = new GameObject("ThrowOrigin");
            origin.transform.SetParent(root.transform); origin.transform.localPosition = new Vector3(0f, 1f, 0.9f);

            GameObject trajGo = new GameObject("Trajectory");
            trajGo.transform.SetParent(root.transform);
            LineRenderer lr = trajGo.AddComponent<LineRenderer>();
            lr.sharedMaterial = lineMat; lr.widthMultiplier = 0.12f;
            TrajectoryRenderer tr = trajGo.AddComponent<TrajectoryRenderer>();

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "LandingMarker"; marker.transform.SetParent(trajGo.transform);
            marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f); marker.transform.localScale = Vector3.one * 0.9f;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = markerMat;
            SetRef(tr, "landingMarker", marker.transform);

            PlayerController pc = root.AddComponent<PlayerController>();
            PlayerInventory inv = root.AddComponent<PlayerInventory>();
            root.AddComponent<PlayerHealth>();
            PlayerCombat combat = root.AddComponent<PlayerCombat>();
            PlayerInteractor interactor = root.AddComponent<PlayerInteractor>();

            SetRef(pc, "inputActions", actions);
            SetRef(combat, "inputActions", actions);
            SetRef(interactor, "inputActions", actions);
            SetRef(combat, "trajectory", tr);
            SetRef(combat, "throwOrigin", origin.transform);

            SerializedObject so = new SerializedObject(inv);
            SerializedProperty slots = so.FindProperty("slots");
            slots.arraySize = PlayerInventory.SlotCount;
            slots.GetArrayElementAtIndex(0).FindPropertyRelative("crop").objectReferenceValue = tomato;
            slots.GetArrayElementAtIndex(1).FindPropertyRelative("crop").objectReferenceValue = turnip;
            SerializedProperty starters = so.FindProperty("starterCrops");
            starters.arraySize = 2;
            starters.GetArrayElementAtIndex(0).objectReferenceValue = tomato;
            starters.GetArrayElementAtIndex(1).objectReferenceValue = turnip;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        });

        // ---- Boss prefab ----
        GameObject bossPrefab = MakePrefab("Caterpillar_Primitive", () =>
        {
            GameObject root = new GameObject("Caterpillar");
            BossHunger hunger = root.AddComponent<BossHunger>();
            CaterpillarBoss boss = root.AddComponent<CaterpillarBoss>();

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head"; head.transform.SetParent(root.transform);
            head.transform.localPosition = Vector3.zero; head.transform.localScale = Vector3.one * 1.4f;
            head.GetComponent<Renderer>().sharedMaterial = caterpillarMat;

            Transform[] segs = new Transform[4];
            for (int i = 0; i < segs.Length; i++)
            {
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "Segment" + (i + 1); s.transform.SetParent(root.transform);
                s.transform.localPosition = new Vector3(0f, 0f, -0.9f * (i + 1));
                s.transform.localScale = Vector3.one * (1.2f - i * 0.1f);
                s.GetComponent<Renderer>().sharedMaterial = caterpillarMat;
                segs[i] = s.transform;
            }

            SetRef(hunger, "data", bossData);
            SetRef(hunger, "burstPrefab", burstFx);
            SetRef(boss, "data", bossData);
            SerializedObject so = new SerializedObject(boss);
            SerializedProperty sp = so.FindProperty("segments");
            sp.arraySize = segs.Length;
            for (int i = 0; i < segs.Length; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = segs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        });

        // ---- Farm tile prefab ----
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject tilePrefab = MakePrefab("FarmTile", () =>
        {
            GameObject root = new GameObject("FarmTile");
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(1.8f, 0.2f, 1.8f);

            GameObject soilGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            soilGo.name = "Soil"; soilGo.transform.SetParent(root.transform);
            soilGo.transform.localScale = new Vector3(1.8f, 0.2f, 1.8f);
            Object.DestroyImmediate(soilGo.GetComponent<Collider>());
            soilGo.GetComponent<Renderer>().sharedMaterial = soil;

            GameObject cropGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cropGo.name = "Crop"; cropGo.transform.SetParent(root.transform);
            cropGo.transform.localPosition = new Vector3(0f, 0.4f, 0f); cropGo.transform.localScale = Vector3.one * 0.7f;
            Object.DestroyImmediate(cropGo.GetComponent<Collider>());
            cropGo.GetComponent<Renderer>().sharedMaterial = tomatoMat;

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform);
            labelGo.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            labelGo.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            TextMesh tm = labelGo.AddComponent<TextMesh>();
            tm.font = font; tm.fontSize = 48; tm.characterSize = 0.1f; tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white; tm.text = "";
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            FarmTile tile = root.AddComponent<FarmTile>();
            SetRef(tile, "defaultCrop", tomato);
            SetRef(tile, "cropVisual", cropGo.transform);
            SetRef(tile, "cropRenderer", cropGo.GetComponent<Renderer>());
            SetRef(tile, "soilRenderer", soilGo.GetComponent<Renderer>());
            SetRef(tile, "label", tm);
            return root;
        });

        // ---- Bell prefab ----
        GameObject bellPrefab = MakePrefab("Bell_Trigger", () =>
        {
            GameObject root = new GameObject("Bell");
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(1.5f, 2.5f, 1.5f); box.center = new Vector3(0f, 1.25f, 0f);

            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post"; post.transform.SetParent(root.transform);
            post.transform.localPosition = new Vector3(0f, 1f, 0f); post.transform.localScale = new Vector3(0.25f, 1f, 0.25f);
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.GetComponent<Renderer>().sharedMaterial = postMat;

            GameObject bell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bell.name = "BellBody"; bell.transform.SetParent(root.transform);
            bell.transform.localPosition = new Vector3(0f, 2.1f, 0f); bell.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            Object.DestroyImmediate(bell.GetComponent<Collider>());
            bell.GetComponent<Renderer>().sharedMaterial = bellMat;

            root.AddComponent<BellTrigger>();
            return root;
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ---- Scene ----
        if (File.Exists(ScenePath) && !overwriteScene)
        {
            Debug.Log("[GoldenSliceBuilder] Assets ready. GoldenSliceScene.unity already exists; use 'Rebuild Golden Slice Scene' to overwrite it.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Transform env = Group("--- ENVIRONMENT ---");
        Transform lightingGroup = Group("--- LIGHTING ---");
        Transform chars = Group("--- CHARACTERS ---");
        Transform managers = Group("--- MANAGERS ---");

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground"; ground.transform.SetParent(env); ground.transform.localScale = Vector3.one * 6f;
        ground.GetComponent<Renderer>().sharedMaterial = grass;

        Transform tiles = new GameObject("FarmTiles").transform;
        tiles.SetParent(env);
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                GameObject t = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab);
                t.transform.SetParent(tiles);
                t.transform.position = new Vector3((col - 1) * 2.2f, 0.1f, 3f + row * 2.2f);
                t.name = $"FarmTile_{row}_{col}";
            }

        GameObject bellGo = (GameObject)PrefabUtility.InstantiatePrefab(bellPrefab);
        bellGo.transform.SetParent(env); bellGo.transform.position = new Vector3(-6f, 0f, 0f);

        GameObject sunGo = new GameObject("Directional Light");
        sunGo.transform.SetParent(lightingGroup);
        Light sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Soft;
        sunGo.AddComponent<UniversalAdditionalLightData>();
        sunGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        GameObject camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.82f, 0.95f);
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UniversalAdditionalCameraData>();
        camGo.AddComponent<AnimalCrossingCamera>();
        camGo.transform.SetParent(lightingGroup);

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.transform.SetParent(chars); player.transform.position = new Vector3(0f, 0.1f, -2f);
        GameObject boss = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab);
        boss.transform.SetParent(chars); boss.transform.position = new Vector3(0f, 0.7f, 20f);
        boss.transform.rotation = Quaternion.LookRotation(Vector3.back);

        SetRef(camGo.GetComponent<AnimalCrossingCamera>(), "target", player.transform);

        GameObject lightingMgr = new GameObject("DayNightLighting");
        lightingMgr.transform.SetParent(managers);
        DayNightLighting lighting = lightingMgr.AddComponent<DayNightLighting>();
        SetRef(lighting, "sun", sun);

        GameObject gmGo = new GameObject("GameManager");
        gmGo.transform.SetParent(managers);
        GameManager gm = gmGo.AddComponent<GameManager>();
        SetRef(gm, "boss", boss.GetComponent<CaterpillarBoss>());
        SetRef(gm, "bossHunger", boss.GetComponent<BossHunger>());
        SetRef(gm, "playerHealth", player.GetComponent<PlayerHealth>());
        SetRef(gm, "inventory", player.GetComponent<PlayerInventory>());
        SetRef(gm, "lighting", lighting);

        GameObject hudGo = new GameObject("DebugHUD");
        hudGo.transform.SetParent(managers);
        DebugHUD hud = hudGo.AddComponent<DebugHUD>();
        SetRef(hud, "health", player.GetComponent<PlayerHealth>());
        SetRef(hud, "inventory", player.GetComponent<PlayerInventory>());
        SetRef(hud, "interactor", player.GetComponent<PlayerInteractor>());
        SetRef(hud, "hunger", boss.GetComponent<BossHunger>());

        EditorSceneManager.SaveScene(scene, ScenePath);
        // Keep any other scenes (e.g. the beta test scene) in the build list; the Golden Slice stays first.
        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if (existing.path != ScenePath) buildScenes.Add(existing);
        EditorBuildSettings.scenes = buildScenes.ToArray();
        Debug.Log("[GoldenSliceBuilder] Golden Slice built: " + ScenePath);
    }

    // ---------- helpers ----------

    private static Transform Group(string name) { return new GameObject(name).transform; }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void SetRef(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[GoldenSliceBuilder] Missing field {field} on {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material Mat(string name, Color color, string shaderName = "Universal Render Pipeline/Lit")
    {
        string path = $"{MatDir}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find(shaderName)) { color = color };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static T Asset<T>(string path, System.Action<T> init) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        T so = ScriptableObject.CreateInstance<T>();
        init(so);
        AssetDatabase.CreateAsset(so, path);
        return so;
    }

    private static GameObject MakePrefab(string name, System.Func<GameObject> build)
    {
        string path = $"{PrefabDir}/{name}.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        GameObject go = build();
        go.name = name;
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return saved;
    }

    /// <summary>Splat colour when no crop is known (CropProjectile tints it with the crop's colour otherwise).</summary>
    internal static readonly Color SplatFallbackColor = new Color(0.9f, 0.2f, 0.15f);

    private static GameObject ParticlePrefab(string name, Material mat, Color startColor, int count, float size, float speed)
    {
        return MakePrefab(name, () =>
        {
            GameObject go = new GameObject(name);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.5f; main.loop = false; main.startLifetime = 0.7f;
            main.startSpeed = speed; main.startSize = size; main.gravityModifier = 1f;
            main.startColor = startColor; main.playOnAwake = true;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.2f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            TriangleFxBuilder.MakeShards(ps);
            return go;
        });
    }
}
