using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Showcase-demo polish: crop growth models, farm FX, boss reactions, in-play tips and the audio system.
/// Additive and idempotent like RunSystemsBuilder: assets are only created when missing, scene objects are
/// only added when missing, and the scene is never rebuilt (COZY stays). Entry point for -executeMethod.
/// </summary>
public static class DemoPolishBuilder
{
    private const string Data = "Assets/Data";
    private const string FxDir = "Assets/Prefabs/Systems/FX";
    private const string PackCrops = "Assets/polyperfect/Poly Universal Pack/Prefabs/Farm/Crops Farm";

    [MenuItem("MoFarmSand/Add Demo Polish")]
    public static void AddDemoPolish()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EnsureFolder(FxDir);

        BuildCropStages();
        BuildFarmFx();
        BuildBossReactions();
        BuildTips();
        BuildAudio();

        EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DemoPolishBuilder] Demo polish added. Save the scene to keep the new objects.");
    }

    // =====================================================================================
    // 1. Crop growth models (sprout -> growing -> ripe), all from the Poly Universal Pack
    // =====================================================================================

    // One table is the single owner of which pack models each crop uses. Peas, turnip, spinach and garlic have no
    // exact match in the pack, so they use the closest-looking plant.
    private static readonly (string cropId, string[] stages, float scale)[] StageTable =
    {
        ("tomato",  new[] { "Tomato_Sprout_A", "Tomato_Seedling_A", "Tomato_Ripe_Group_A" }, 1f),
        ("corn",    new[] { "Corn_Sprout_A", "Corn_Young_A", "Corn_Ripe_Sweetcorn_A" }, 1f),
        ("pumpkin", new[] { "Pumpkin_Sprout_A", "Pumpkin_Flowering_A", "Pumpkin_Ripe_A" }, 1f),
        ("lettuce", new[] { "Salad_Sprout_A", "Salad_Young_A", "Salad_Mature_A" }, 1f),
        ("peas",    new[] { "BellPepper_Sprout_A", "BellPepper_Seedling_A", "BellPepper_Mature" }, 1f),
        ("turnip",  new[] { "Parsnip_Plant_Sapling_Root", "Parsnip_Plant_Young_Root", "Parsnip_Plant_Ripe_Root_A" }, 1f),
        ("spinach", new[] { "Beetroot_Plant_Sapling", "Beetroot_Plant_Young", "Beetroot_Plant_Ripe_A" }, 1f),
        ("garlic",  new[] { "Leek_Sprout_A", "Leek_Seedling_A", "Leek_Mature_A" }, 1f),
    };

    private static void BuildCropStages()
    {
        Dictionary<string, CropData> crops = new Dictionary<string, CropData>();
        foreach (string guid in AssetDatabase.FindAssets("t:CropData"))
        {
            CropData c = AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && !string.IsNullOrEmpty(c.cropId)) crops[c.cropId] = c;
        }

        foreach ((string cropId, string[] stages, float scale) row in StageTable)
        {
            if (!crops.TryGetValue(row.cropId, out CropData crop))
            {
                Debug.LogWarning($"[DemoPolishBuilder] No CropData with id '{row.cropId}'.");
                continue;
            }
            // Tomato is moved off the old custom primitives on purpose so every crop matches; the rest only fill gaps.
            bool replace = row.cropId == "tomato";
            bool empty = crop.growthStages == null || crop.growthStages.Length == 0;
            if (!replace && !empty) continue;

            GameObject[] models = new GameObject[row.stages.Length];
            bool ok = true;
            for (int i = 0; i < models.Length; i++)
            {
                models[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackCrops}/{row.stages[i]}.prefab");
                if (models[i] == null) { Debug.LogError($"[DemoPolishBuilder] Missing pack prefab {row.stages[i]}."); ok = false; }
            }
            if (!ok) continue;

            Undo.RecordObject(crop, "Assign growth stages");
            crop.growthStages = models;
            crop.growthStageScale = row.scale;
            EditorUtility.SetDirty(crop);
        }
    }

    // =====================================================================================
    // 2. Plant / ripe / harvest FX
    // =====================================================================================

    private static void BuildFarmFx()
    {
        Material puff = RunSystemsBuilder.Mat("FarmFxPuff", Color.white, "Universal Render Pipeline/Particles/Unlit");
        GameObject dirt = RunSystemsBuilder.Fx("FX_DirtPuff", puff, new Color(0.45f, 0.3f, 0.18f, 0.9f), loop: false, rate: 0f, life: 0.6f,
            size: 0.18f, speed: 1.6f, gravity: 1.2f, shape: ParticleSystemShapeType.Sphere, shapeScale: Vector3.one * 0.25f, offset: Vector3.zero, burst: 18);
        GameObject sparkle = RunSystemsBuilder.Fx("FX_RipeSparkle", puff, new Color(1f, 0.95f, 0.55f, 1f), loop: true, rate: 7f, life: 1.2f,
            size: 0.08f, speed: 0.35f, gravity: -0.08f, shape: ParticleSystemShapeType.Sphere, shapeScale: Vector3.one * 0.35f, offset: Vector3.zero);
        GameObject leaves = RunSystemsBuilder.Fx("FX_HarvestLeaves", puff, new Color(0.5f, 0.85f, 0.35f, 1f), loop: false, rate: 0f, life: 0.9f,
            size: 0.14f, speed: 2.6f, gravity: 1.5f, shape: ParticleSystemShapeType.Sphere, shapeScale: Vector3.one * 0.3f, offset: Vector3.zero, burst: 14);

        Transform managers = GetOrCreateRoot("--- MANAGERS ---");
        GameObject host = GetOrCreate("FarmFx", managers);
        FarmFx fx = Ensure<FarmFx>(host);
        SetRef(fx, "dirtPuff", dirt);
        SetRef(fx, "ripeSparkle", sparkle);
        SetRef(fx, "harvestLeaves", leaves);
    }

    // =====================================================================================
    // 3. Boss reactions (scare "!", stagger, blush + sweat tiers)
    // =====================================================================================

    private static void BuildBossReactions()
    {
        Material puff = RunSystemsBuilder.Mat("FarmFxPuff", Color.white, "Universal Render Pipeline/Particles/Unlit");
        // Rate starts at 0: BossReactions turns the drips on at the blush and stuffed tiers.
        GameObject sweat = RunSystemsBuilder.Fx("FX_SweatDrops", puff, new Color(0.6f, 0.85f, 1f, 0.95f), loop: true, rate: 0f, life: 0.7f,
            size: 0.1f, speed: 1.4f, gravity: 1.4f, shape: ParticleSystemShapeType.Sphere, shapeScale: Vector3.one * 0.5f, offset: Vector3.zero);
        GameObject mark = ScareMarkPrefab();

        GameObject boss = GameObject.Find("Caterpillar_Primitive");
        if (boss == null) { Debug.LogWarning("[DemoPolishBuilder] No Caterpillar_Primitive in the open scene; skipped boss reactions."); return; }
        BossReactions reactions = Ensure<BossReactions>(boss);
        SetRef(reactions, "scareMarkPrefab", mark);
        SetRef(reactions, "sweatPrefab", sweat);

        // Tint the same cards, mouth and wings the feed flash already uses.
        GameFeedback feedback = Object.FindFirstObjectByType<GameFeedback>();
        if (feedback != null)
        {
            SerializedProperty src = new SerializedObject(feedback).FindProperty("bossRenderers");
            SerializedObject dst = new SerializedObject(reactions);
            SerializedProperty list = dst.FindProperty("tintRenderers");
            list.arraySize = src.arraySize;
            for (int i = 0; i < src.arraySize; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = src.GetArrayElementAtIndex(i).objectReferenceValue;
            dst.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>A yellow "!" built from two primitives (an unlit material reads better than the legacy font material under URP).</summary>
    private static GameObject ScareMarkPrefab()
    {
        string path = $"{FxDir}/FX_ScareMark.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        Material mat = RunSystemsBuilder.Mat("ScareMark", new Color(1f, 0.8f, 0.1f), "Universal Render Pipeline/Unlit");
        GameObject root = new GameObject("FX_ScareMark");
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bar.name = "Bar";
        bar.transform.SetParent(root.transform, false);
        bar.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        bar.transform.localScale = new Vector3(0.16f, 0.3f, 0.16f);
        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "Dot";
        dot.transform.SetParent(root.transform, false);
        dot.transform.localPosition = new Vector3(0f, -0.22f, 0f);
        dot.transform.localScale = Vector3.one * 0.2f;
        foreach (GameObject part in new[] { bar, dot })
        {
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    // =====================================================================================
    // 4. In-play tips
    // =====================================================================================

    // Text is only written when a tip asset is first created, so edits made in the Inspector survive re-runs.
    private static readonly (string id, string title, string body)[] TipTexts =
    {
        (TipIds.FirstDay, "Welcome to the farm", "Walk with W A S D. Pick a hotbar slot with 1-8 or the mouse wheel. Hold a seed, stand at an empty plot and press E to plant it."),
        (TipIds.HoldingSeed, "Seeds go in the ground", "Walk up to an empty plot and press E to plant this seed. Plant in its own season: out-of-season crops grow slowly and come out weak."),
        (TipIds.FirstPlant, "It's growing", "Crops ripen by themselves and a timer counts down over the plot. Plant a few more while you wait."),
        (TipIds.FirstRipe, "Ready to pick!", "A ripe crop bobs and sparkles. Stand next to it and press E, or left-click it, to harvest."),
        (TipIds.FirstHarvest, "Crops are your ammo", "Keep what you harvest to feed the boss at night, or sell some to the merchant for coins. Press E at the bell when you're ready to end the day."),
        (TipIds.Merchant, "The merchant", "Press E to trade. He buys your crops for coins and sells seeds. His shelf is limited and restocks on Merchant days."),
        (TipIds.FirstNight, "Feed the caterpillar", "Don't hurt it, feed it. Hit it with crops until its fullness bar is full, before dawn. Dodge its bites and the slime it spits."),
        (TipIds.ShooterHeld, "Auto-fire crop", "Hold left-click to fire toward the cursor. One harvested crop is 10 shots."),
        (TipIds.SupportHeld, "Support crop", "Right-click to eat it for Bark Shield charges. The shield wears off after 20 seconds."),
        (TipIds.FirstDisaster, "Disaster!", "Bad weather and pests can wither your crops. Harvest early and replant afterwards."),
        (TipIds.FirstVoidFall, "Watch the edge", "Falling off the farm costs a heart. On a boss night you respawn and the fight goes on."),
    };

    private static void BuildTips()
    {
        EnsureFolder(Data + "/Tips");
        EnsureFolder(Data + "/Registries");

        TipRegistry registry = AssetDatabase.LoadAssetAtPath<TipRegistry>($"{Data}/Registries/TipRegistry.asset");
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<TipRegistry>();
            AssetDatabase.CreateAsset(registry, $"{Data}/Registries/TipRegistry.asset");
        }
        foreach ((string id, string title, string body) in TipTexts)
        {
            string path = $"{Data}/Tips/Tip_{id}.asset";
            TipData tip = AssetDatabase.LoadAssetAtPath<TipData>(path);
            if (tip == null)
            {
                tip = ScriptableObject.CreateInstance<TipData>();
                tip.tipId = id; tip.title = title; tip.body = body;
                AssetDatabase.CreateAsset(tip, path);
            }
            registry.Ensure(tip);
        }
        EditorUtility.SetDirty(registry);

        GameDatabase db = AssetDatabase.LoadAssetAtPath<GameDatabase>($"{Data}/GameDatabase.asset");
        if (db != null)
        {
            if (db.tips != registry) { db.tips = registry; EditorUtility.SetDirty(db); }
        }
        else Debug.LogError("[DemoPolishBuilder] GameDatabase.asset not found; tips are not linked.");

        Transform managers = GetOrCreateRoot("--- MANAGERS ---");
        TipsView view = Ensure<TipsView>(GetOrCreate("TipsView", managers));
        SetRef(view, "tips", registry);
        SetRef(view, "inventory", Object.FindFirstObjectByType<PlayerInventory>());
        SetRef(view, "interactor", Object.FindFirstObjectByType<PlayerInteractor>());
        SetRef(view, "disasters", Object.FindFirstObjectByType<DisasterDirector>());
    }

    // =====================================================================================
    // 5. Audio (silent until clips are assigned to the cues)
    // =====================================================================================

    private static void BuildAudio()
    {
        EnsureFolder(Data + "/Audio");

        AudioLibrary library = AssetDatabase.LoadAssetAtPath<AudioLibrary>($"{Data}/Audio/AudioLibrary.asset");
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<AudioLibrary>();
            AssetDatabase.CreateAsset(library, $"{Data}/Audio/AudioLibrary.asset");
        }

        // One empty cue per AudioLibrary field, so the team only has to drag clips onto them.
        foreach (System.Reflection.FieldInfo field in typeof(AudioLibrary).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (field.FieldType != typeof(AudioCue) || field.GetValue(library) != null) continue;
            string path = $"{Data}/Audio/Cue_{field.Name}.asset";
            AudioCue cue = AssetDatabase.LoadAssetAtPath<AudioCue>(path);
            if (cue == null)
            {
                cue = ScriptableObject.CreateInstance<AudioCue>();
                if (field.Name.EndsWith("Music")) { cue.loop = true; cue.volume = 0.8f; }
                AssetDatabase.CreateAsset(cue, path);
            }
            field.SetValue(library, cue);
            EditorUtility.SetDirty(library);
        }

        Transform managers = GetOrCreateRoot("--- MANAGERS ---");
        AudioDirector director = Ensure<AudioDirector>(GetOrCreate("AudioDirector", managers));
        SetRef(director, "library", library);
        SetRef(director, "hunger", Object.FindFirstObjectByType<BossHunger>());
        SetRef(director, "health", Object.FindFirstObjectByType<PlayerHealth>());
        SetRef(director, "wallet", Object.FindFirstObjectByType<PlayerWallet>());
    }

    // =====================================================================================
    // Helpers
    // =====================================================================================

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
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
        if (target == null) return;
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[DemoPolishBuilder] Missing field {field} on {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
