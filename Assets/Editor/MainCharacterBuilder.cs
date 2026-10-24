using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Organizes the Mixamo main character (rig, materials, clips), builds its Animator Controller and swaps the
/// primitive player prefab body for the model. Idempotent: safe to run again after tweaking.
/// </summary>
public static class MainCharacterBuilder
{
    private const string CharDir = "Assets/Art/Characters/MainCharacter";
    private const string AnimDir = "Assets/Art/Animations";
    private const string RiggedPath = CharDir + "/MainCharacter_Rigged.fbx";
    private const string UnriggedOld = "Assets/Art/Meshes/MainCharacter.fbx";
    private const string UnriggedNew = CharDir + "/Source/MainCharacter_Unrigged.fbx";
    private const string NewBodyMeshPath = "Assets/Art/Meshes/Farmer_Unwrapped.fbx";
    private const string SkinnedMeshPath = CharDir + "/MainCharacter_Skinned.asset";
    private const string ControllerPath =AnimDir + "/MainCharacter.controller";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player_Primitive.prefab";

    // Target height matches the CharacterController (1.4) in GoldenSliceBuilder.
    private const float TargetHeight = 1.4f;

    // Must match PlayerController.moveSpeed (walk) and runSpeed (hold Sprint).
    private const float WalkSpeed = 2.5f;
    private const float RunSpeed = 5f;

    // Playback speed of the Farm state, so planting and harvesting feel snappy.
    private const float FarmSpeed = 3f;

    // Throwing is an upper-body layer over locomotion, so the dog can walk and throw at once. PlayerAnimator starts the
    // "Throw" state by name on that layer; the layer's default "Empty" state lets the base layer show through.
    private const string ThrowLayerName = "Throw";
    private const string ThrowEmptyState = "Empty";
    private const string ThrowState = "Throw";
    private const string UpperBodyMaskPath = AnimDir + "/UpperBodyMask.mask";
    // Drives the Throw state's normalized time: PlayerAnimator pins it at the aim pose while the button is down, then
    // advances it to play the swing on release. (Freezing with speed 0 loses the start offset, so time is set directly.)
    private const string ThrowTimeParam = "ThrowTime";
    private const float ThrowSpeed = 1f;
    // The clip is 2.2 s: the arm winds up until ~0.7 s, releases at ~0.78 s and settles slowly after ~1.0 s. PlayerAnimator
    // starts it at ~0.66 s, so the layer lets go at ~55% (about 1.2 s) instead of holding the slow settle.
    private const float ThrowExitTime = 0.55f;
    private const float ThrowFadeOut = 0.2f;

    private const string DanceDir =AnimDir + "/Dances";
    private const string BreakdanceDir = DanceDir + "/Breakdance Pack";
    private const string DanceStateA = "DanceA"; // must match PlayerDance's state hashes
    private const string DanceStateB = "DanceB";
    private const string PlaceholderAPath = AnimDir + "/DanceSlotA.anim";
    private const string PlaceholderBPath = AnimDir + "/DanceSlotB.anim";
    private const string DanceDataDir = "Assets/Data/Dances";
    private const string DanceLibraryPath = DanceDataDir + "/DanceLibrary.asset";

    private struct ClipSpec
    {
        public string oldName, newName, clipName, dir;
        public bool loop;
        public bool grounded; // drop the clip's vertical root motion so airborne moves stay at ground level
        public ClipSpec(string o, string n, string c, bool l, string d = AnimDir, bool g = false) { oldName = o; newName = n; clipName = c; loop = l; dir = d; grounded = g; }
        public string Path => $"{dir}/{newName}.fbx";
    }

    private static readonly ClipSpec[] Clips =
    {
        new ClipSpec("Idle", "MainCharacter@Idle", "Idle", true),
        new ClipSpec("Walking", "MainCharacter@Walk", "Walk", true),
        new ClipSpec("Running", "MainCharacter@Run", "Run", true),
        new ClipSpec("Farming", "MainCharacter@Farming", "Farming", false),
        new ClipSpec("Throw", "MainCharacter@Throw", "Throw", false),
        new ClipSpec("Death", "MainCharacter@Death", "Death", false),
    };

    // Emote-wheel dances, in slice order (first slice at the top, then clockwise). The Animator state is named after the clip.
    private static readonly ClipSpec[] Dances =
    {
        new ClipSpec("Chicken Dance", "MainCharacter@Dance_Chicken", "Dance_Chicken", true, DanceDir),
        new ClipSpec("Dancing", "MainCharacter@Dance_Groove", "Dance_Groove", true, DanceDir),
        new ClipSpec("Northern Soul Spin Combo", "MainCharacter@Dance_Spin", "Dance_Spin", true, DanceDir),
        new ClipSpec("Northern Soul Floor Combo", "MainCharacter@Dance_Floor", "Dance_Floor", true, DanceDir),
        new ClipSpec("Flying Shoulder Throw", "MainCharacter@Dance_Flip", "Dance_Flip", true, DanceDir, true),
    };

    private static readonly string[] DanceLabels = { "Chicken", "Groove", "Spin", "Floor", "Flip" };
    private static readonly Color[] DanceColors =
    {
        new Color(1f, 0.75f, 0.3f), new Color(0.75f, 0.45f, 1f), new Color(0.35f, 0.85f, 0.95f),
        new Color(1f, 0.5f, 0.7f), new Color(0.55f, 0.9f, 0.45f),
    };

    [MenuItem("MoFarmSand/Setup Main Character")]
    public static void Build()
    {
        AssetDatabase.SaveAssets();
        EnsureFolder(CharDir);
        EnsureFolder(CharDir + "/Source");
        EnsureFolder(CharDir + "/Materials");
        EnsureFolder(DanceDir);
        EnsureFolder(DanceDataDir);

        OrganizeAssets();
        Avatar avatar = SetupRiggedModel();
        foreach (ClipSpec c in Clips) SetupClip(c, avatar);
        foreach (ClipSpec c in Dances) SetupClip(c, avatar);
        SetupBreakdancePack(avatar);
        AssetDatabase.Refresh();

        DanceLibrary library = EnsureDanceAssets();
        AnimatorController controller = BuildController();
        SwapPlayerBody(controller, avatar, library);

        AssetDatabase.SaveAssets();
        Debug.Log("[MainCharacterBuilder] Main character set up.");
    }

    /// <summary>
    /// Imports every FBX in the Breakdance Pack as a Humanoid clip on the dog's avatar, so each one previews on the dog
    /// in the Inspector. The clips are not on the emote wheel; add a spec to <see cref="Dances"/> to use one.
    /// </summary>
    private static void SetupBreakdancePack(Avatar avatar)
    {
        if (!AssetDatabase.IsValidFolder(BreakdanceDir)) return;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { BreakdanceDir }))
        {
            string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            SetupClip(new ClipSpec(name, name, name, false, BreakdanceDir), avatar);
        }
    }

    private static void OrganizeAssets()
    {
        // The un-rigged Mixamo upload mesh has no skeleton; keep it for reference, out of the way.
        if (File.Exists(UnriggedOld) && !File.Exists(UnriggedNew))
            Report(AssetDatabase.MoveAsset(UnriggedOld, UnriggedNew));

        // A Mixamo "with skin" clip FBX carries the skinned mesh + skeleton; copy one before renaming the clips.
        if (!File.Exists(RiggedPath))
        {
            string idle = $"{AnimDir}/Idle.fbx";
            string renamed = $"{AnimDir}/MainCharacter@Idle.fbx";
            string source = File.Exists(idle) ? idle : renamed;
            if (!AssetDatabase.CopyAsset(source, RiggedPath))
                Debug.LogError("[MainCharacterBuilder] Could not create the rigged model from " + source);
        }

        foreach (ClipSpec c in Clips) MoveClip(c);
        foreach (ClipSpec c in Dances) MoveClip(c); // loose Mixamo downloads land in AnimDir; file them under Dances/
        AssetDatabase.Refresh();
    }

    private static void MoveClip(ClipSpec c)
    {
        if (File.Exists(c.Path)) return;
        // Mixamo downloads land loose in AnimDir or, for dances, straight in the clip's own folder.
        foreach (string from in new[] { $"{AnimDir}/{c.oldName}.fbx", $"{c.dir}/{c.oldName}.fbx" })
            if (File.Exists(from)) { Report(AssetDatabase.MoveAsset(from, c.Path)); return; }
    }

    private static Avatar SetupRiggedModel()
    {
        ModelImporter mi = (ModelImporter)AssetImporter.GetAtPath(RiggedPath);
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = false;
        mi.SaveAndReimport();

        // Extract materials so they can be edited (the FBX's embedded ones are read-only).
        foreach (Material m in AssetDatabase.LoadAllAssetsAtPath(RiggedPath).OfType<Material>().ToList())
        {
            string target = $"{CharDir}/Materials/{m.name}.mat";
            if (!File.Exists(target)) Report(AssetDatabase.ExtractAsset(m, target));
        }
        AssetDatabase.WriteImportSettingsIfDirty(RiggedPath);
        AssetDatabase.ImportAsset(RiggedPath, ImportAssetOptions.ForceUpdate);

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(RiggedPath).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            Debug.LogError("[MainCharacterBuilder] Humanoid avatar is not valid. Open MainCharacter_Rigged.fbx > Rig > Configure and check the bone mapping.");
        return avatar;
    }

    private static void SetupClip(ClipSpec c, Avatar avatar)
    {
        string path = c.Path;
        ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) { Debug.LogError("[MainCharacterBuilder] Missing clip file " + path); return; }

        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        mi.sourceAvatar = avatar;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importAnimation = true;

        // Mixamo clips are exported "with skin": we only want their animation, so skip everything else in the file.
        mi.importBlendShapes = false;
        mi.importNormals = ModelImporterNormals.None;
        mi.importTangents = ModelImporterTangents.None;
        mi.importVisibility = false;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importConstraints = false;
        mi.importAnimatedCustomProperties = false;
        mi.isReadable = false;
        mi.meshCompression = ModelImporterMeshCompression.High;
        // Smaller clips: Optimal picks the best curve compression per clip; constant scale curves are pure waste.
        mi.animationCompression = ModelImporterAnimationCompression.Optimal;
        mi.removeConstantScaleCurves = true;

        ModelImporterClipAnimation[] defaults = mi.defaultClipAnimations;
        if (defaults.Length > 0)
        {
            ModelImporterClipAnimation clip = defaults[0];
            clip.name = c.clipName;
            clip.loopTime = c.loop;
            clip.loopPose = c.loop;
            // Play in place: the CharacterController does the moving, not root motion.
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            if (c.grounded)
            {
                // The Flip clip launches the hips ~1.2 body heights up; unbaked height is discarded (no root motion).
                clip.lockRootHeightY = false;
                clip.keepOriginalPositionY = false;
                clip.heightFromFeet = true;
            }
            mi.clipAnimations = new[] { clip };
        }
        mi.SaveAndReimport();
    }

    private static AnimationClip LoadClip(string fileName, string dir = AnimDir)
    {
        return AssetDatabase.LoadAllAssetsAtPath($"{dir}/{fileName}.fbx")
            .OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview__"));
    }

    /// <summary>
    /// The library is created once and seeded with the <see cref="Dances"/> specs while it has no clips. After that the
    /// slots belong to the Inspector, so re-runs never overwrite what was dragged in.
    /// </summary>
    private static DanceLibrary EnsureDanceAssets()
    {
        DanceLibrary library = AssetDatabase.LoadAssetAtPath<DanceLibrary>(DanceLibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<DanceLibrary>();
            AssetDatabase.CreateAsset(library, DanceLibraryPath);
        }

        if (!library.slots.Any(s => s.clip != null))
        {
            library.slots.Clear();
            for (int i = 0; i < Dances.Length && i < DanceLibrary.MaxSlots; i++)
            {
                library.slots.Add(new DanceSlot
                {
                    displayName = DanceLabels[i],
                    clip = LoadClip(Dances[i].newName, Dances[i].dir),
                    color = DanceColors[i],
                });
            }
        }
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        return library;
    }

    /// <summary>An empty clip that stands in for "the chosen dance" in a dance state; each state needs its own asset to override.</summary>
    private static AnimationClip EnsurePlaceholder(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(clip, path);
        }
        return clip;
    }

    private static AnimatorController BuildController()
    {
        // Rebuilt from scratch each run so tweaks here always win.
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Farm", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(new AnimatorControllerParameter { name = ThrowTimeParam, type = AnimatorControllerParameterType.Float, defaultFloat = 0f });

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(LoadClip("MainCharacter@Idle"), 0f);
        tree.AddChild(LoadClip("MainCharacter@Walk"), WalkSpeed);
        tree.AddChild(LoadClip("MainCharacter@Run"), RunSpeed);

        AnimatorState locomotion = sm.states.First(s => s.state.name == "Locomotion").state;
        sm.defaultState = locomotion;

        AnimatorState farm = sm.AddState("Farm");
        farm.motion = LoadClip("MainCharacter@Farming");
        farm.speed = FarmSpeed;

        AnimatorStateTransition toFarm = locomotion.AddTransition(farm);
        toFarm.hasExitTime = false;
        toFarm.duration = 0.1f;
        toFarm.AddCondition(AnimatorConditionMode.If, 0f, "Farm");

        AnimatorStateTransition done = farm.AddTransition(locomotion);
        done.hasExitTime = true;
        done.exitTime = 0.9f;
        done.duration = 0.15f;

        // Moving cancels the farming animation so the player never feels stuck.
        AnimatorStateTransition cancel = farm.AddTransition(locomotion);
        cancel.hasExitTime = false;
        cancel.duration = 0.1f;
        cancel.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");

        // Dances: two generic states entered from PlayerDance via CrossFade. PlayerDance swaps the chosen clip over the
        // placeholder at runtime (AnimatorOverrideController); moving or planting leaves them.
        foreach (string stateName in new[] { DanceStateA, DanceStateB })
        {
            AnimatorState dance = sm.AddState(stateName);
            dance.motion = EnsurePlaceholder(stateName == DanceStateA ? PlaceholderAPath : PlaceholderBPath);

            AnimatorStateTransition stop = dance.AddTransition(locomotion);
            stop.hasExitTime = false;
            stop.duration = 0.1f;
            stop.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");

            AnimatorStateTransition plant = dance.AddTransition(farm);
            plant.hasExitTime = false;
            plant.duration = 0.1f;
            plant.AddCondition(AnimatorConditionMode.If, 0f, "Farm");
        }

        // Death: entered by PlayerAnimator with a CrossFade and never left (the scene reloads after a loss).
        AnimatorState dying = sm.AddState("Death");
        dying.motion = LoadClip("MainCharacter@Death");

        AddThrowLayer(ctrl);

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    /// <summary>
    /// A second, upper-body-only layer for throwing. The mask keeps the legs on the base layer (so the walk keeps cycling)
    /// while the arms, head and torso follow the throw clip. It idles in an empty state, which lets the base layer through.
    /// </summary>
    private static void AddThrowLayer(AnimatorController ctrl)
    {
        ctrl.AddLayer(ThrowLayerName);
        AnimatorControllerLayer[] layers = ctrl.layers; // a copy: edit it, then assign it back
        int index = layers.Length - 1;
        layers[index].avatarMask = EnsureUpperBodyMask();
        layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[index].defaultWeight = 1f;
        ctrl.layers = layers;

        AnimatorStateMachine sm = ctrl.layers[index].stateMachine;
        AnimatorState empty = sm.AddState(ThrowEmptyState);
        sm.defaultState = empty;

        AnimatorState throwing = sm.AddState(ThrowState);
        throwing.motion = LoadClip("MainCharacter@Throw");
        throwing.speed = ThrowSpeed;
        throwing.timeParameterActive = true;
        throwing.timeParameter = ThrowTimeParam;

        // Leave when the ThrowTime parameter passes the exit point (not on the state's own clock, which keeps running while the
        // aim pose is pinned): holding sits below it forever, releasing advances ThrowTime past it.
        AnimatorStateTransition done = throwing.AddTransition(empty);
        done.hasExitTime = false;
        done.duration = ThrowFadeOut;
        done.AddCondition(AnimatorConditionMode.Greater, ThrowExitTime, ThrowTimeParam);
    }

    /// <summary>Humanoid mask: torso, head, arms and hands on; root, legs and foot IK off. Created once, so Inspector edits survive re-runs.</summary>
    private static AvatarMask EnsureUpperBodyMask()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
        if (mask != null) return mask;

        mask = new AvatarMask();
        foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
        {
            if (part == AvatarMaskBodyPart.LastBodyPart) continue;
            bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers
                || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
            mask.SetHumanoidBodyPartActive(part, upper);
        }
        AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
        return mask;
    }

    private static void SwapPlayerBody(AnimatorController controller, Avatar avatar, DanceLibrary library)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedPath);
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            // Remove the primitive sphere/cube (and any previous run's model); other children stay untouched.
            foreach (string name in new[] { "Body", "Snout" })
            {
                Transform old = root.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            body.name = "Body"; // PlayerController squashes the child named "Body"
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            ApplyNewBodyMesh(body);

            Bounds b = BodyBounds(body);
            float scale = b.size.y > 0.0001f ? TargetHeight / b.size.y : 1f;
            body.transform.localScale = Vector3.one * scale;
            b = BodyBounds(body);
            body.transform.localPosition = new Vector3(0f, root.transform.position.y - b.min.y, 0f);

            Animator anim = body.GetComponent<Animator>();
            if (anim == null) anim = body.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.avatar = avatar;
            anim.applyRootMotion = false;

            PlayerAnimator pa = root.GetComponent<PlayerAnimator>();
            if (pa == null) pa = root.AddComponent<PlayerAnimator>();
            SerializedObject so = new SerializedObject(pa);
            so.FindProperty("animator").objectReferenceValue = anim;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (root.GetComponent<PlayerDeath>() == null) root.AddComponent<PlayerDeath>();

            PlayerDance pd = root.GetComponent<PlayerDance>();
            if (pd == null) pd = root.AddComponent<PlayerDance>();
            SerializedObject pds = new SerializedObject(pd);
            pds.FindProperty("library").objectReferenceValue = library;
            pds.FindProperty("animator").objectReferenceValue = anim;
            pds.FindProperty("slotA").objectReferenceValue = EnsurePlaceholder(PlaceholderAPath);
            pds.FindProperty("slotB").objectReferenceValue = EnsurePlaceholder(PlaceholderBPath);
            pds.FindProperty("controller").objectReferenceValue = root.GetComponent<PlayerController>();
            pds.ApplyModifiedPropertiesWithoutUndo();

            PlayerController pc = root.GetComponent<PlayerController>();
            if (pc != null)
            {
                SerializedObject pcs = new SerializedObject(pc);
                pcs.FindProperty("moveSpeed").floatValue = WalkSpeed;
                pcs.FindProperty("runSpeed").floatValue = RunSpeed;
                pcs.FindProperty("bodyVisual").objectReferenceValue = body.transform;
                pcs.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Swaps the rigged dog's mesh for the new unwrapped one and gives it the FarmerMAT material. The new mesh has no
    /// skeleton, so it borrows the rigged mesh's bones: every vertex takes the bone weights of the nearest rigged vertex.
    /// </summary>
    private static void ApplyNewBodyMesh(GameObject body)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(NewBodyMeshPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CharacterArtBuilder.FarmerMaterialPath);
        SkinnedMeshRenderer smr = body.GetComponentInChildren<SkinnedMeshRenderer>();
        MeshFilter newFilter = source != null ? source.GetComponentInChildren<MeshFilter>() : null;
        if (smr == null || newFilter == null) { Debug.LogWarning("[MainCharacterBuilder] New dog mesh not found; keeping the old body."); return; }

        Mesh skinned = BuildSkinnedBody(smr.sharedMesh, newFilter.sharedMesh);
        smr.sharedMesh = skinned;
        if (material != null) smr.sharedMaterials = new[] { material };
    }

    private static Mesh BuildSkinnedBody(Mesh rigged, Mesh fresh)
    {
        Vector3[] rv = rigged.vertices;
        BoneWeight[] rw = rigged.boneWeights;
        Vector3[] nv = fresh.vertices;

        // Line the new mesh up with the rigged one (the new pivot is the centre, the rigged one stands on the ground).
        Vector3 shift = rigged.bounds.center - fresh.bounds.center;
        BoneWeight[] weights = new BoneWeight[nv.Length];
        Vector3[] moved = new Vector3[nv.Length];
        float worst = 0f;
        for (int i = 0; i < nv.Length; i++)
        {
            moved[i] = nv[i] + shift;
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int j = 0; j < rv.Length; j++)
            {
                float d = (rv[j] - moved[i]).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = j; }
            }
            weights[i] = rw[best];
            worst = Mathf.Max(worst, bestSqr);
        }
        Debug.Log($"[MainCharacterBuilder] Skin weights copied onto {nv.Length} vertices; furthest match {Mathf.Sqrt(worst) * 100f:F2} cm (mesh height {fresh.bounds.size.y * 100f:F1} cm).");

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SkinnedMeshPath);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear();
        mesh.name = "MainCharacter_Skinned";
        mesh.indexFormat = fresh.indexFormat;
        mesh.vertices = moved;
        mesh.normals = fresh.normals;
        mesh.tangents = fresh.tangents;
        mesh.uv = fresh.uv;
        mesh.subMeshCount = fresh.subMeshCount;
        for (int s = 0; s < fresh.subMeshCount; s++) mesh.SetTriangles(fresh.GetTriangles(s), s);
        mesh.bindposes = rigged.bindposes;
        mesh.boneWeights = weights;
        mesh.RecalculateBounds();
        if (mesh.tangents.Length == 0) mesh.RecalculateTangents();

        if (isNew) AssetDatabase.CreateAsset(mesh, SkinnedMeshPath);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    /// <summary>
    /// World bounds of the posed skinned mesh. A SkinnedMeshRenderer's own bounds are measured from its root bone and can be
    /// stale or off, which would sink or float the dog; baking the vertices measures what is really drawn.
    /// </summary>
    private static Bounds BodyBounds(GameObject go)
    {
        SkinnedMeshRenderer smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null) return CombinedBounds(go);

        Mesh baked = new Mesh();
        smr.BakeMesh(baked, true);
        Vector3[] verts = baked.vertices;
        Object.DestroyImmediate(baked);
        if (verts.Length == 0) return CombinedBounds(go);

        Bounds b = new Bounds(smr.transform.TransformPoint(verts[0]), Vector3.zero);
        for (int i = 1; i < verts.Length; i++) b.Encapsulate(smr.transform.TransformPoint(verts[i]));
        return b;
    }

    private static Bounds CombinedBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.one);
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void Report(string error)
    {
        if (!string.IsNullOrEmpty(error)) Debug.LogError("[MainCharacterBuilder] " + error);
    }
}
