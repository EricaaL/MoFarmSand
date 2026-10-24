using UnityEngine;

/// <summary>Presentation only: drives the main character's Animator from movement, planting and throwing.</summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerAnimator : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int FarmHash = Animator.StringToHash("Farm");
    private static readonly int ThrowHash = Animator.StringToHash("Throw");
    private static readonly int EmptyHash = Animator.StringToHash("Empty");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int ThrowTimeHash = Animator.StringToHash("ThrowTime");

    private const float FallbackThrowClipSeconds = 2.17f;

    [SerializeField] private Animator animator;
    [SerializeField] private float speedDamp = 0.08f;
    [Tooltip("Where the aim pose is held while the throw button is down: seconds into the throw clip. 0.75 s is frame 18 of the 24 fps clip, the cocked arm just before the swing (the release is at ~0.78 s).")]
    [SerializeField] private float throwHoldSeconds = 0.75f;
    [SerializeField] private float holdFadeSeconds = 0.12f;
    [SerializeField] private float cancelFadeSeconds = 0.15f;
    [Tooltip("For a throw with no aim before it (dragged off the hotbar): seconds into the clip to start from, so the arm is already swinging when the crop leaves the hand.")]
    [SerializeField] private float throwStartSeconds = 0.66f;
    [SerializeField] private float throwFadeSeconds = 0.05f;
    [SerializeField] private float deathFadeSeconds = 0.15f;

    private readonly ThrowPoseTracker pose = new ThrowPoseTracker();
    private CharacterController controller;
    private PlayerCombat combat;
    private int throwLayer = -1;
    private float throwClipSeconds = FallbackThrowClipSeconds;
    private float throwTime;      // normalized time of the Throw state, set here (the state reads it from the ThrowTime parameter)
    private bool releasing;       // advancing throwTime to play the swing
    private bool firedLobbed;
    private PlayerDeath death;
    private bool dead;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        death = GetComponent<PlayerDeath>();
        combat = GetComponent<PlayerCombat>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        // The upper-body throw layer sits over locomotion (built by MainCharacterBuilder); -1 if the controller has none.
        if (animator != null)
        {
            throwLayer = animator.GetLayerIndex("Throw");
            if (throwLayer >= 0) animator.SetLayerWeight(throwLayer, 1f);
            if (animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    if (clip != null && clip.name == "Throw") { throwClipSeconds = clip.length; break; }
        }
    }

    private void OnEnable()
    {
        FarmTile.Planted += OnPlanted;
        PlayerCombat.Fired += OnFired;
        if (death != null) death.Died += OnDied;
    }

    private void OnDisable()
    {
        FarmTile.Planted -= OnPlanted;
        PlayerCombat.Fired -= OnFired;
        if (death != null) death.Died -= OnDied;
        pose.Reset();
        releasing = false;
    }

    /// <summary>Drops the dog into the death clip and keeps every other animation out of the way.</summary>
    private void OnDied()
    {
        dead = true;
        releasing = false;
        if (animator == null) return;
        if (throwLayer >= 0)
        {
            animator.CrossFadeInFixedTime(EmptyHash, 0f, throwLayer);
            animator.SetLayerWeight(throwLayer, 0f);
        }
        animator.SetFloat(SpeedHash, 0f);
        animator.CrossFadeInFixedTime(DeathHash, deathFadeSeconds, 0);
    }

    private void Update()
    {
        if (animator == null || dead) return;
        Vector3 v = controller.velocity; v.y = 0f;
        animator.SetFloat(SpeedHash, v.magnitude, speedDamp, Time.deltaTime);

        if (throwLayer >= 0)
        {
            ApplyThrowPose(pose.Step(combat != null && combat.IsAiming, firedLobbed));
            if (releasing)
            {
                // Play the swing: run the Throw state's time on from the aim pose. The state leaves for Empty by itself.
                throwTime += Time.deltaTime / Mathf.Max(0.1f, throwClipSeconds);
                animator.SetFloat(ThrowTimeHash, throwTime);
                if (throwTime >= 1f) releasing = false;
            }
        }
        firedLobbed = false;
    }

    private void OnPlanted(FarmTile tile, GameObject who)
    {
        if (animator == null || who != gameObject) return;
        Vector3 toTile = tile.transform.position - transform.position; toTile.y = 0f;
        if (toTile.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toTile);
        animator.SetTrigger(FarmHash);
    }

    /// <summary>Only lobbed crops use the throw pose. Hold-to-fire shooters (peas, corn) fire too fast for an arm swing each shot.</summary>
    private void OnFired(CropData crop)
    {
        if (crop != null && crop.type == CropType.Projectile) firedLobbed = true;
    }

    /// <summary>
    /// Holding the throw button pins the Throw state at the aim pose on the upper-body layer, so the legs keep walking
    /// underneath; releasing runs it on through the swing, and abandoning the aim fades the arm back down.
    /// </summary>
    private void ApplyThrowPose(ThrowPoseAction action)
    {
        switch (action)
        {
            case ThrowPoseAction.Hold:
                releasing = false;
                SetThrowTime(throwHoldSeconds);
                animator.CrossFadeInFixedTime(ThrowHash, holdFadeSeconds, throwLayer);
                break;
            case ThrowPoseAction.Release:
                releasing = true;
                break;
            case ThrowPoseAction.Cancel:
                releasing = false;
                animator.CrossFadeInFixedTime(EmptyHash, cancelFadeSeconds, throwLayer);
                break;
            case ThrowPoseAction.Swing:
                releasing = true;
                SetThrowTime(throwStartSeconds);
                animator.CrossFadeInFixedTime(ThrowHash, throwFadeSeconds, throwLayer);
                break;
        }
    }

    private void SetThrowTime(float seconds)
    {
        throwTime = seconds / Mathf.Max(0.1f, throwClipSeconds);
        animator.SetFloat(ThrowTimeHash, throwTime);
    }
}
