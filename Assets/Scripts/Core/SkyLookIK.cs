using UnityEngine;

/// <summary>
/// Presentation only: turns the humanoid's head (and a little of the chest) up at the sky through look-at IK.
/// Sits next to the Animator, because OnAnimatorIK only reaches components on that GameObject. The base layer needs
/// "IK Pass" on (set by MainCharacterBuilder).
/// </summary>
[RequireComponent(typeof(Animator))]
public class SkyLookIK : MonoBehaviour
{
    [Range(0f, 1f)] public float weight;
    [SerializeField] private float bodyWeight = 0.35f;
    [SerializeField] private float headWeight = 1f;
    [SerializeField] private float clampWeight = 0.85f;
    [Tooltip("The point looked at, relative to the dog: a little ahead and very high up.")]
    [SerializeField] private Vector3 skyOffset = new Vector3(0f, 25f, 4f);

    private Animator animator;

    private void Awake() { animator = GetComponent<Animator>(); }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;
        animator.SetLookAtWeight(weight, bodyWeight, headWeight, 0f, clampWeight);
        animator.SetLookAtPosition(transform.position + transform.rotation * skyOffset);
    }
}
