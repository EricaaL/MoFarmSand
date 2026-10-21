using UnityEngine;

/// <summary>Presentation only: drives the main character's Animator from movement and planting.</summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerAnimator : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int FarmHash = Animator.StringToHash("Farm");

    [SerializeField] private Animator animator;
    [SerializeField] private float speedDamp = 0.08f;

    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable() { FarmTile.Planted += OnPlanted; }
    private void OnDisable() { FarmTile.Planted -= OnPlanted; }

    private void Update()
    {
        if (animator == null) return;
        Vector3 v = controller.velocity; v.y = 0f;
        animator.SetFloat(SpeedHash, v.magnitude, speedDamp, Time.deltaTime);
    }

    private void OnPlanted(FarmTile tile, GameObject who)
    {
        if (animator == null || who != gameObject) return;
        Vector3 toTile = tile.transform.position - transform.position; toTile.y = 0f;
        if (toTile.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toTile);
        animator.SetTrigger(FarmHash);
    }
}
