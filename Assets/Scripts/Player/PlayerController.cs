using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float moveSpeed = 2.5f; // walk: hold the Sprint key (Shift)
    [SerializeField] private float runSpeed = 5f;    // default speed
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private InputAction moveAction;
    private InputAction sprintAction;
    private float verticalVelocity;
    private Vector3 knockback;

    [SerializeField] private Transform bodyVisual;

    public bool CanMove { get; set; } = true;

    /// <summary>Multiplicative speed modifiers keyed by source (e.g. disasters). Empty = full speed.</summary>
    public ModifierStack SpeedModifiers { get; } = new ModifierStack();

    private float squashTime = 1f;
    private Vector3 bodyBaseScale = Vector3.one;

    /// <summary>Quick squash-and-stretch on the body, called when throwing.</summary>
    public void PlayThrowSquash() { squashTime = 0f; }

    private Vector3 facingDir;
    private float facingUntil;
    private Vector3 aimPoint;
    private float aimUntil;
    private const float AimDeadZone = 0.3f; // a cursor this close to the dog gives no usable direction

    /// <summary>
    /// Keeps the dog looking at <paramref name="worldPoint"/> (the cursor) while the caller keeps calling it each frame (aiming a
    /// throw or holding to fire). It lapses by itself 0.1 s after the last call. Beats the walk direction and the post-throw hold.
    /// </summary>
    public void AimAt(Vector3 worldPoint)
    {
        aimPoint = worldPoint;
        aimUntil = Time.time + 0.1f;
    }

    /// <summary>
    /// Faces <paramref name="direction"/> now and keeps facing it for <paramref name="seconds"/>, instead of turning toward the walk
    /// direction. Lets the dog walk while its upper body throws without the throw swinging round to the side.
    /// </summary>
    public void FaceDirection(Vector3 direction, float seconds)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;
        facingDir = direction.normalized;
        facingUntil = Time.time + Mathf.Max(0f, seconds);
        transform.rotation = Quaternion.LookRotation(facingDir, Vector3.up);
    }

    private void Awake()
    {
        if (bodyVisual == null) bodyVisual = transform.Find("Body");
        if (bodyVisual != null) bodyBaseScale = bodyVisual.localScale;
        controller = GetComponent<CharacterController>();
        if (inputActions == null) inputActions = InputSystem.actions;
        moveAction = inputActions != null ? inputActions.FindAction("Player/Move") : null;
        sprintAction = inputActions != null ? inputActions.FindAction("Player/Sprint") : null;
    }

    private void OnEnable() { moveAction?.Enable(); sprintAction?.Enable(); }

    public void ApplyKnockback(Vector3 impulse)
    {
        impulse.y = 0f;
        knockback += impulse;
    }

    private void Update()
    {
        Vector2 input = (CanMove && moveAction != null) ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector3 move = new Vector3(input.x, 0f, input.y);
        if (move.sqrMagnitude > 1f) move.Normalize();

        if (Time.time < aimUntil)
        {
            // Aiming: look at the cursor while the legs carry on walking. Too close to it and we keep the current facing.
            Vector3 toAim = aimPoint - transform.position; toAim.y = 0f;
            if (toAim.sqrMagnitude > AimDeadZone * AimDeadZone)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toAim, Vector3.up), turnSpeed * Time.deltaTime);
        }
        else if (Time.time < facingUntil)
        {
            // Mid-throw: keep facing the throw while the legs carry on walking.
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facingDir, Vector3.up), turnSpeed * Time.deltaTime);
        }
        else if (move.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        float speed = (sprintAction != null && sprintAction.IsPressed()) ? moveSpeed : runSpeed;
        Vector3 velocity = move * (speed * SpeedModifiers.Product) + knockback + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
        knockback = Vector3.Lerp(knockback, Vector3.zero, 8f * Time.deltaTime);

        if (bodyVisual != null && squashTime < 1f)
        {
            squashTime += Time.deltaTime * 5f;
            float s = 1f + 0.25f * Mathf.Sin(Mathf.Clamp01(squashTime) * Mathf.PI);
            bodyVisual.localScale = new Vector3(bodyBaseScale.x * (2f - s), bodyBaseScale.y * s, bodyBaseScale.z * (2f - s));
        }
        else if (bodyVisual != null && bodyVisual.localScale != bodyBaseScale)
        {
            bodyVisual.localScale = bodyBaseScale;
        }
    }
}
