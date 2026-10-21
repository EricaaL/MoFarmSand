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
    private const string ControllerPath = AnimDir + "/MainCharacter.controller";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player_Primitive.prefab";

    // Target height matches the CharacterController (1.4) in GoldenSliceBuilder.
    private const float TargetHeight = 1.4f;

    // Must match PlayerController.moveSpeed (walk) and runSpeed (hold Sprint).
    private const float WalkSpeed = 2.5f;
    private const float RunSpeed = 5f;

    private struct ClipSpec
    {
        public string oldName, newName, clipName;
        public bool loop;
        public ClipSpec(string o, string n, string c, bool l) { oldName = o; newName = n; clipName = c; loop = l; }
    }

    private static readonly ClipSpec[] Clips =
    {
        new ClipSpec("Idle", "MainCharacter@Idle", "Idle", true),
        new ClipSpec("Walking", "MainCharacter@Walk", "Walk", true),
        new ClipSpec("Running", "MainCharacter@Run", "Run", true),
        new ClipSpec("Farming", "MainCharacter@Farming", "Farming", false),
    };

    [MenuItem("MoFarmSand/Setup Main Character")]
    public static void Build()
    {
        AssetDatabase.SaveAssets();
        EnsureFolder(CharDir);
        EnsureFolder(CharDir + "/Source");
        EnsureFolder(CharDir + "/Materials");

        OrganizeAssets();
        Avatar avatar = SetupRiggedModel();
        foreach (ClipSpec c in Clips) SetupClip(c, avatar);
        AssetDatabase.Refresh();

        AnimatorController controller = BuildController();
        SwapPlayerBody(controller, avatar);

        AssetDatabase.SaveAssets();
        Debug.Log("[MainCharacterBuilder] Main character set up.");
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

        foreach (ClipSpec c in Clips)
        {
            string from = $"{AnimDir}/{c.oldName}.fbx";
            if (File.Exists(from) && !File.Exists($"{AnimDir}/{c.newName}.fbx"))
                Report(AssetDatabase.MoveAsset(from, $"{AnimDir}/{c.newName}.fbx"));
        }
        AssetDatabase.Refresh();
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
        string path = $"{AnimDir}/{c.newName}.fbx";
        ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) { Debug.LogError("[MainCharacterBuilder] Missing clip file " + path); return; }

        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        mi.sourceAvatar = avatar;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importAnimation = true;

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
            mi.clipAnimations = new[] { clip };
        }
        mi.SaveAndReimport();
    }

    private static AnimationClip LoadClip(string fileName)
    {
        return AssetDatabase.LoadAllAssetsAtPath($"{AnimDir}/{fileName}.fbx")
            .OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview__"));
    }

    private static AnimatorController BuildController()
    {
        // Rebuilt from scratch each run so tweaks here always win.
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Farm", AnimatorControllerParameterType.Trigger);

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

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    private static void SwapPlayerBody(AnimatorController controller, Avatar avatar)
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

            Bounds b = CombinedBounds(body);
            float scale = b.size.y > 0.0001f ? TargetHeight / b.size.y : 1f;
            body.transform.localScale = Vector3.one * scale;
            b = CombinedBounds(body);
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
