using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Turns the imported toon chocobo (Assets/Art/Characters/Chocobo/Chocobo.fbx) into a ready-to-place prefab: the looping
/// "Toctoc" clip, URP Lit materials from its textures, an Animator Controller and Assets/Prefabs/Characters/Chocobo.prefab.
/// The FBX also carries a stump and a ground disc; the prefab keeps only the bird. Additive and idempotent.
/// </summary>
public static class ChocoboBuilder
{
    private const string Folder = "Assets/Art/Characters/Chocobo";
    private const string FbxPath = Folder + "/Chocobo.fbx";
    private const string MaterialPath = Folder + "/Chocobo.mat";
    private const string ControllerPath = "Assets/Art/Animations/Chocobo.controller";
    private const string PrefabPath = "Assets/Prefabs/Characters/Chocobo.prefab";
    private const string ClipName = "Toctoc";
    /// <summary>The file is about 0.2 units tall; this brings the bird to roughly a metre and a half.</summary>
    private const float PrefabScale = 8f;

    [MenuItem("MoFarmSand/Add Chocobo")]
    public static void AddChocobo()
    {
        AssetDatabase.SaveAssets();
        SetUpClip();
        Material material = MakeMaterial();
        AnimatorController controller = MakeController();
        MakePrefab(material, controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Chocobo ready: " + PrefabPath);
    }

    private static void SetUpClip()
    {
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation c in clips)
        {
            c.name = ClipName;
            c.loopTime = true;
        }
        importer.clipAnimations = clips;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;   // the Arnold shader graphs it brings are not ours
        importer.SaveAndReimport();
    }

    private static Material MakeMaterial()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Chocobo" };
            AssetDatabase.CreateAsset(m, MaterialPath);
        }
        Texture2D body = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/Chocobo.png");
        Texture2D glow = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/ChocoboEmission.png");
        m.SetTexture("_BaseMap", body);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0f);
        if (glow != null)
        {
            m.SetTexture("_EmissionMap", glow);
            m.SetColor("_EmissionColor", Color.white);
            m.EnableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static AnimatorController MakeController()
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == ClipName);
        if (state == null) state = machine.AddState(ClipName);
        state.motion = clip;
        machine.defaultState = state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void MakePrefab(Material material, AnimatorController controller)
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // Keep the bird (the skinned body and its rig); drop the stump, ground disc and tufts that came in the same file.
        foreach (Transform child in instance.transform.Cast<Transform>().ToArray())
            if (child.GetComponentInChildren<SkinnedMeshRenderer>() == null && child.name.IndexOf("Rig", System.StringComparison.OrdinalIgnoreCase) < 0)
                Object.DestroyImmediate(child.gameObject);

        foreach (SkinnedMeshRenderer r in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            r.sharedMaterials = r.sharedMaterials.Select(_ => material).ToArray();

        Animator animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        instance.name = "Chocobo";
        instance.transform.localScale = Vector3.one * PrefabScale;

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
    }
}
