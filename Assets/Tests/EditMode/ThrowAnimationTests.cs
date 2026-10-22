using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>The throw animation is an upper-body layer over locomotion, so the dog can walk and throw at once.</summary>
public class ThrowAnimationTests
{
    private const string ControllerPath = "Assets/Art/Animations/MainCharacter.controller";
    private const string MaskPath = "Assets/Art/Animations/UpperBodyMask.mask";
    private const string ClipPath = "Assets/Art/Animations/MainCharacter@Throw.fbx";

    private static AnimatorControllerLayer ThrowLayer()
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert.IsNotNull(ctrl, "controller is missing (run MoFarmSand > Setup Main Character)");
        AnimatorControllerLayer layer = ctrl.layers.FirstOrDefault(l => l.name == "Throw");
        Assert.IsNotNull(layer, "the controller has no 'Throw' layer (run MoFarmSand > Setup Main Character)");
        return layer;
    }

    [Test]
    public void ThrowLayerOverridesTheUpperBodyAndIdlesInAnEmptyState()
    {
        AnimatorControllerLayer layer = ThrowLayer();
        Assert.AreEqual(AnimatorLayerBlendingMode.Override, layer.blendingMode);
        Assert.AreEqual(1f, layer.defaultWeight);
        Assert.IsNotNull(layer.avatarMask, "without a mask the throw would also drive the legs");

        AnimatorState idle = layer.stateMachine.defaultState;
        Assert.IsNotNull(idle);
        Assert.AreEqual("Empty", idle.name);
        Assert.IsNull(idle.motion, "the idle state must be empty so the walk shows through");

        AnimatorState throwing = layer.stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Throw");
        Assert.IsNotNull(throwing, "no 'Throw' state (PlayerAnimator starts it by name)");
        Assert.IsNotNull(throwing.motion);
        Assert.AreEqual("Throw", throwing.motion.name);
        Assert.IsTrue(throwing.transitions.Any(t => t.destinationState == idle), "the throw never returns to Empty");
    }

    [Test]
    public void TheThrowOnlyLeavesWhenThrowTimePassesTheExitPointSoHoldingNeverDropsTheArm()
    {
        AnimatorState throwing = ThrowLayer().stateMachine.states.Select(s => s.state).First(s => s.name == "Throw");
        AnimatorStateTransition leave = throwing.transitions.First(t => t.destinationState.name == "Empty");
        Assert.IsFalse(leave.hasExitTime, "the state's own clock keeps running while the pose is pinned, so it must not decide the exit");
        AnimatorCondition c = leave.conditions.Single();
        Assert.AreEqual("ThrowTime", c.parameter);
        Assert.AreEqual(AnimatorConditionMode.Greater, c.mode);
        Assert.Greater(c.threshold, 0.346f, "the aim pose (frame 18 of 52) must sit below the exit point");
    }

    [Test]
    public void ThrowTimeIsDrivenByAParameterSoTheAimPoseCanBePinned()
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorControllerParameter p = ctrl.parameters.FirstOrDefault(x => x.name == "ThrowTime");
        Assert.IsNotNull(p, "no ThrowTime parameter (run MoFarmSand > Setup Main Character)");
        Assert.AreEqual(AnimatorControllerParameterType.Float, p.type);

        AnimatorState throwing = ThrowLayer().stateMachine.states.Select(s => s.state).First(s => s.name == "Throw");
        Assert.IsTrue(throwing.timeParameterActive);
        Assert.AreEqual("ThrowTime", throwing.timeParameter);
    }

    [Test]
    public void UpperBodyMaskKeepsTheLegsOnTheWalkCycle()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        Assert.IsNotNull(mask, "UpperBodyMask.mask is missing");
        foreach (AvatarMaskBodyPart on in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm })
            Assert.IsTrue(mask.GetHumanoidBodyPartActive(on), on + " should follow the throw");
        foreach (AvatarMaskBodyPart off in new[] { AvatarMaskBodyPart.Root, AvatarMaskBodyPart.LeftLeg, AvatarMaskBodyPart.RightLeg, AvatarMaskBodyPart.LeftFootIK, AvatarMaskBodyPart.RightFootIK })
            Assert.IsFalse(mask.GetHumanoidBodyPartActive(off), off + " should stay on the base layer");
    }

    [Test]
    public void ThrowClipIsAHumanoidClipOnTheDogsAvatar()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ClipPath) as ModelImporter;
        Assert.IsNotNull(importer, "MainCharacter@Throw.fbx is missing (run MoFarmSand > Setup Main Character)");
        Assert.AreEqual(ModelImporterAnimationType.Human, importer.animationType, "a Generic clip can't retarget onto the dog");
        Assert.IsNotNull(importer.sourceAvatar);

        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        Assert.IsNotNull(clip);
        Assert.AreEqual("Throw", clip.name);
        Assert.IsTrue(clip.isHumanMotion);
        Assert.IsFalse(clip.isLooping);
    }
}
