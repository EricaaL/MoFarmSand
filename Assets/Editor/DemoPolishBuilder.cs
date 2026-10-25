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
        BuildProjectiles(rebuild: false);
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
    // 1. Crop growth models (sprout -> growing -> ripe): a 3x3 plot of Poly Universal Pack plants per stage
    // =====================================================================================

    // One table is the single owner of which pack models each crop uses. Peas, turnip, spinach and garlic have no
    // exact match in the pack, so they use the closest-looking plant. heightCap keeps tall crops from hiding the dog;
    // scaleMultiplier is a hand-tuning knob applied on top of the auto-fit (1 = fit to the cell).
    // earlyStageScale multiplies every stage before the ripe one (sprout and growing), so young plants read bigger.
    private static readonly (string cropId, string[] stages, float heightCap, float scaleMultiplier, float earlyStageScale)[] StageTable =
    {
        ("tomato",  new[] { "Tomato_Sprout_A", "Tomato_Seedling_A", "Tomato_Ripe_Group_A" }, 1.3f, 1f, 1f),
        ("corn",    new[] { "Corn_Sprout_A", "Corn_Young_A", "Corn_Ripe_Sweetcorn_A" }, 1.6f, 1f, 1f),
        ("pumpkin", new[] { "Pumpkin_Sprout_A", "Pumpkin_Flowering_A", "Pumpkin_Ripe_A" }, 0.8f, 1f, 1f),
        ("lettuce", new[] { "Salad_Sprout_A", "Salad_Young_A", "Salad_Mature_A" }, 0.6f, 1f, 1f),
        ("peas",    new[] { "BellPepper_Sprout_A", "BellPepper_Seedling_A", "BellPepper_Mature" }, 1.0f, 1f, 1f),
        ("turnip",  new[] { "Parsnip_Plant_Sapling_Root", "Parsnip_Plant_Young_Root", "Parsnip_Plant_Ripe_Root_A" }, 0.8f, 1f, 1f),
        ("spinach", new[] { "Beetroot_Plant_Sapling", "Beetroot_Plant_Young", "Beetroot_Plant_Ripe_A" }, 0.8f, 1f, 1f),
        ("garlic",  new[] { "Leek_Sprout_A", "Leek_Seedling_A", "Leek_Mature_A" }, 1.0f, 1f, 1f),
    };

    private const string CropPrefabDir = "Assets/Prefabs/Crops";
    private const string FarmTilePrefab = "Assets/Prefabs/FarmTile.prefab";
    private const int Grid = 3;                 // plants across and down each plot
    private const float FitFraction = 0.9f;     // a plant's footprint fills at most this much of its cell
    private const float JitterFraction = 0.08f; // random nudge, as a fraction of a cell
    private const float EarlyStageBoost = 1f;   // growth stages 1 and 2 only, on top of earlyStageScale
    private const float PlantSizeBoost = 1.25f; // every plant, on top of the auto-fit (so plants may nudge past the cell and height cap)

    /// <summary>Regenerates every 3x3 crop prefab in place (same paths and GUIDs), for when the table or sizes change.</summary>
    [MenuItem("MoFarmSand/Rebuild 3x3 Crop Plots")]
    public static void RebuildCropPlots()
    {
        EnsureFolder(CropPrefabDir);
        BuildCropStages(rebuild: true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DemoPolishBuilder] 3x3 crop plots rebuilt.");
    }

    /// <summary>
    /// Builds, for each crop, three prefabs (sprout, growing, ripe) that each hold a 3x3 grid of the pack model, and
    /// assigns them to CropData.growthStages. Existing prefabs are kept unless <paramref name="rebuild"/> is set.
    /// </summary>
    private static void BuildCropStages(bool rebuild = false)
    {
        EnsureFolder(CropPrefabDir);
        Dictionary<string, CropData> crops = new Dictionary<string, CropData>();
        foreach (string guid in AssetDatabase.FindAssets("t:CropData"))
        {
            CropData c = AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && !string.IsNullOrEmpty(c.cropId)) crops[c.cropId] = c;
        }

        float cell = MeasureSoilSide() / Grid;

        foreach ((string cropId, string[] stages, float heightCap, float scaleMultiplier, float earlyStageScale) row in StageTable)
        {
            if (!crops.TryGetValue(row.cropId, out CropData crop))
            {
                Debug.LogWarning($"[DemoPolishBuilder] No CropData with id '{row.cropId}'.");
                continue;
            }

            GameObject[] models = new GameObject[row.stages.Length];
            bool ok = true;
            for (int i = 0; i < models.Length; i++)
            {
                models[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackCrops}/{row.stages[i]}.prefab");
                if (models[i] == null) { Debug.LogError($"[DemoPolishBuilder] Missing pack prefab {row.stages[i]}."); ok = false; }
            }
            if (!ok) continue;

            // One scale per crop, measured on the ripe model, so the plants visibly grow between stages instead of resizing.
            float fit = FitScale(models[models.Length - 1], cell, row.heightCap) * row.scaleMultiplier * PlantSizeBoost;

            GameObject[] plots = new GameObject[models.Length];
            for (int i = 0; i < models.Length; i++)
            {
                string path = $"{CropPrefabDir}/{Capitalize(row.cropId)}_Stage{i + 1}_3x3.prefab";
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                float stageFit = i < models.Length - 1 ? fit * row.earlyStageScale * EarlyStageBoost : fit;
                plots[i] = existing != null && !rebuild ? existing : BuildPlotPrefab(path, models[i], row.cropId, cell, stageFit);
            }

            bool same = crop.growthStages != null && crop.growthStages.Length == plots.Length;
            for (int i = 0; same && i < plots.Length; i++) same = crop.growthStages[i] == plots[i];
            if (same) continue; // already assigned: leave any hand-tuned growthStageScale alone

            Undo.RecordObject(crop, "Assign 3x3 growth stages");
            crop.growthStages = plots;
            crop.growthStageScale = 1f;
            EditorUtility.SetDirty(crop);
        }
    }

    /// <summary>One plot's worth of plants: an empty root with Grid x Grid nested pack-prefab instances, jittered a little.</summary>
    private static GameObject BuildPlotPrefab(string path, GameObject model, string cropId, float cell, float fit)
    {
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(path));
        int index = 0;
        for (int gz = 0; gz < Grid; gz++)
        {
            for (int gx = 0; gx < Grid; gx++, index++)
            {
                // Same seed for every stage of a crop, so each plant stays in its spot as it grows.
                System.Random rng = new System.Random(StableHash(cropId) ^ (index * 7919));
                float jx = ((float)rng.NextDouble() * 2f - 1f) * cell * JitterFraction;
                float jz = ((float)rng.NextDouble() * 2f - 1f) * cell * JitterFraction;
                float yaw = (float)rng.NextDouble() * 360f;

                GameObject plant = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                plant.name = $"Plant_{gx}_{gz}";
                plant.transform.localPosition = new Vector3((gx - (Grid - 1) * 0.5f) * cell + jx, 0f, (gz - (Grid - 1) * 0.5f) * cell + jz);
                plant.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;
                plant.transform.localScale = model.transform.localScale * fit;
            }
        }
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    /// <summary>Uniform scale (at most 1) that fits the model's footprint in a cell and under the height cap.</summary>
    private static float FitScale(GameObject model, float cell, float heightCap)
    {
        GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
        probe.transform.position = Vector3.zero;
        Bounds b = RendererBounds(probe);
        Object.DestroyImmediate(probe);
        if (b.size.sqrMagnitude < 1e-6f) return 1f;

        float scale = 1f;
        float footprint = Mathf.Max(b.size.x, b.size.z);
        if (footprint > 0f) scale = Mathf.Min(scale, cell * FitFraction / footprint);
        if (b.size.y > 0f) scale = Mathf.Min(scale, heightCap / b.size.y);
        return scale;
    }

    /// <summary>Shorter side of the soil on the farm tile prefab, so the grid matches the real plot (falls back to 2.4 m).</summary>
    private static float MeasureSoilSide()
    {
        GameObject tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FarmTilePrefab);
        if (tilePrefab == null) return 2.4f;
        GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab);
        probe.transform.position = Vector3.zero;
        probe.transform.rotation = Quaternion.identity;
        Transform soil = probe.transform.Find("Soil");
        Renderer r = soil != null ? soil.GetComponent<Renderer>() : null;
        float side = r != null ? Mathf.Min(r.bounds.size.x, r.bounds.size.z) : 2.4f;
        Object.DestroyImmediate(probe);
        return side > 0.5f ? side : 2.4f;
    }

    private static Bounds RendererBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    // string.GetHashCode isn't stable between runs, and the jitter has to be, so a build always gives the same layout.
    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
            return h;
        }
    }

    // =====================================================================================
    // 1b. Thrown crops: pack models instead of tinted spheres
    // =====================================================================================

    private const string PackRoot = "Assets/polyperfect/Poly Universal Pack/Prefabs";

    // One table owns which pack model each thrown crop uses. Peas keep their small green ball: the pack has no pea, and a
    // pea is round. size is the model's largest extent in metres; colliderFraction scales the SphereCollider from it.
    // The pack has no loose tomato (only whole staked plants), so the tomato is its nearest round red fruit, and its Garlic
    // and Onion prefabs reference meshes that are missing from this import, so garlic is a pale potato (BuildProjectiles
    // refuses a model with no mesh, so a broken pick can't produce an invisible projectile).
    private static readonly (string cropId, string model, float size, float colliderFraction, bool alignLongAxis)[] ProjectileTable =
    {
        ("tomato",  "Farm/Crops Farm/Apple_Red", 0.40f, 0.45f, false),
        ("pumpkin", "Farm/Crops Farm/Pumpkin_D", 0.55f, 0.45f, false),
        ("garlic",  "Farm/Crops Farm/Potato_C", 0.38f, 0.45f, false),
        ("corn",    "Farm/Crops Farm/Corn_Peeled", 0.50f, 0.28f, true), // a thin cob flying along its length: smaller collider
    };

    /// <summary>Regenerates every thrown-crop prefab in place (same paths and GUIDs), for when the table changes.</summary>
    [MenuItem("MoFarmSand/Rebuild Crop Projectiles")]
    public static void RebuildCropProjectiles()
    {
        EnsureFolder(CropPrefabDir);
        BuildProjectiles(rebuild: true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DemoPolishBuilder] Crop projectiles rebuilt.");
    }

    /// <summary>
    /// Builds a projectile prefab per table row: the crop's current projectile (physics, CropProjectile and splat carry over)
    /// with its sphere mesh swapped for a child holding the pack model, then points CropData.projectilePrefab at it.
    /// </summary>
    private static void BuildProjectiles(bool rebuild)
    {
        Dictionary<string, CropData> crops = new Dictionary<string, CropData>();
        foreach (string guid in AssetDatabase.FindAssets("t:CropData"))
        {
            CropData c = AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && !string.IsNullOrEmpty(c.cropId)) crops[c.cropId] = c;
        }

        foreach ((string cropId, string model, float size, float colliderFraction, bool alignLongAxis) row in ProjectileTable)
        {
            if (!crops.TryGetValue(row.cropId, out CropData crop))
            {
                Debug.LogWarning($"[DemoPolishBuilder] No CropData with id '{row.cropId}'.");
                continue;
            }
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackRoot}/{row.model}.prefab");
            if (model == null) { Debug.LogError($"[DemoPolishBuilder] Missing pack prefab {row.model}."); continue; }
            if (!HasMesh(model)) { Debug.LogError($"[DemoPolishBuilder] Pack prefab {row.model} has no mesh (its mesh is missing from this import); pick another model."); continue; }

            string path = $"{CropPrefabDir}/{Capitalize(row.cropId)}_Projectile.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || rebuild)
            {
                if (crop.projectilePrefab == null) { Debug.LogError($"[DemoPolishBuilder] {row.cropId} has no projectile prefab to start from."); continue; }
                prefab = BuildProjectilePrefab(path, crop.projectilePrefab, model, row.size, row.colliderFraction, row.alignLongAxis, crop.type == CropType.Shooter);
            }

            if (crop.projectilePrefab == prefab) continue;
            Undo.RecordObject(crop, "Assign projectile prefab");
            crop.projectilePrefab = prefab;
            EditorUtility.SetDirty(crop);
        }
    }

    private static bool HasMesh(GameObject prefab)
    {
        foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) return true;
        foreach (SkinnedMeshRenderer sr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (sr.sharedMesh != null) return true;
        return false;
    }

    private static GameObject BuildProjectilePrefab(string path, GameObject template, GameObject model, float size, float colliderFraction, bool alignLongAxis, bool bullet)
    {
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(template);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = Path.GetFileNameWithoutExtension(path);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        // The old look was a sphere on the root; on a rebuild there is none, and a previous Model child goes too.
        MeshRenderer oldRenderer = root.GetComponent<MeshRenderer>();
        if (oldRenderer != null) Object.DestroyImmediate(oldRenderer);
        MeshFilter oldFilter = root.GetComponent<MeshFilter>();
        if (oldFilter != null) Object.DestroyImmediate(oldFilter);
        Transform oldModel = root.transform.Find("Model");
        if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        visual.name = "Model";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        // Lay a long model (the corn cob) along +Z, the direction bullets are fired.
        if (alignLongAxis)
        {
            Vector3 ext = RendererBounds(visual).size;
            if (ext.y >= ext.x && ext.y >= ext.z) visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            else if (ext.x >= ext.y && ext.x >= ext.z) visual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        // Scale so the largest extent is exactly `size`, then centre the model on the root so the collider sits on it.
        Bounds b = RendererBounds(visual);
        float largest = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (largest > 1e-4f) visual.transform.localScale *= size / largest;
        b = RendererBounds(visual);
        visual.transform.position -= b.center;

        SphereCollider sphere = root.GetComponent<SphereCollider>();
        if (sphere != null) { sphere.center = Vector3.zero; sphere.radius = size * colliderFraction; }
        Rigidbody body = root.GetComponent<Rigidbody>();
        // Fast bullets must not tunnel through the caterpillar's collider.
        if (body != null && bullet) body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
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
        (TipIds.ThrownHeld, "Throw crop", "Hold left-click to aim, let go to lob it at the cursor. One crop per throw."),
        (TipIds.PlotHeld, "New farmland", "Left-click open ground to place this plot (green means it fits). Press R on an empty placed plot to pick it back up."),
        (TipIds.FirstWither, "It withered", "That crop died and the plot is empty again. Plant a new seed in it."),
        (TipIds.NightSoon, "Night is coming", "Daylight is almost gone, and the next day is the season boss. Boss days start at night, so pick what's ripe now and keep crops to feed it."),
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
        SetRef(view, "clock", Object.FindFirstObjectByType<DayClock>());
        SetRef(view, "director", Object.FindFirstObjectByType<RunDirector>());
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
