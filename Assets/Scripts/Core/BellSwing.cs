using UnityEngine;

/// <summary>
/// Presentation only: swings the bell parts like a damped pendulum when <see cref="Ring"/> is called.
/// The parts are re-parented under a pivot at the top of their bounds, so they hang from the bracket.
/// </summary>
public class BellSwing : MonoBehaviour
{
    [SerializeField] private Transform[] swingParts;
    [SerializeField] private float swingAngle = 25f;
    [SerializeField] private float swingFrequency = 2.2f;
    [SerializeField] private float damping = 3.5f;
    [SerializeField] private float punchScale = 0.06f;
    [SerializeField] private float punchSeconds = 0.18f;

    private Transform pivot;
    private Vector3 axis = Vector3.right;
    private float elapsed;
    private bool swinging;

    private void Awake()
    {
        if (swingParts == null || swingParts.Length == 0) return;

        Bounds bounds = default;
        bool any = false;
        foreach (Transform part in swingParts)
        {
            if (part == null) continue;
            foreach (Renderer r in part.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
        }
        if (!any) return;

        pivot = new GameObject("SwingPivot").transform;
        pivot.SetParent(transform, false);
        pivot.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        foreach (Transform part in swingParts)
            if (part != null) part.SetParent(pivot, true);
    }

    /// <summary>Starts a swing that tips the bell away from <paramref name="from"/>.</summary>
    public void Ring(Vector3 from)
    {
        if (pivot == null) return;
        Vector3 away = transform.position - from;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = transform.forward;
        away.Normalize();
        // Rotating about this axis tips the hanging bell toward `away` first.
        axis = Vector3.Cross(Vector3.up, away);
        elapsed = 0f;
        swinging = true;
    }

    private void Update()
    {
        if (!swinging) return;
        elapsed += Time.deltaTime;

        float envelope = Mathf.Exp(-damping * elapsed);
        float angle = swingAngle * envelope * Mathf.Sin(2f * Mathf.PI * swingFrequency * elapsed);
        pivot.rotation = Quaternion.AngleAxis(angle, axis) * transform.rotation;

        float punch = elapsed < punchSeconds ? Mathf.Sin(elapsed / punchSeconds * Mathf.PI) * punchScale : 0f;
        pivot.localScale = new Vector3(1f - punch, 1f + punch, 1f - punch);

        if (envelope * swingAngle < 0.05f && elapsed > punchSeconds)
        {
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;
            swinging = false;
        }
    }
}
