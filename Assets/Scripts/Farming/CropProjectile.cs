using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CropProjectile : MonoBehaviour
{
    public static int ActiveCount { get; private set; }

    /// <summary>Raised when a thrown crop lands (not shooter bullets): where, and whether it hit the boss. Drives the splat sound.</summary>
    public static event System.Action<Vector3, bool> Landed;

    [SerializeField] private GameObject splatPrefab;
    [SerializeField] private float lifetime = 6f;

    private CropData crop;
    private float quality = 1f;
    private bool hit;

    /// <param name="strength">Crop quality (1 = normal). Weak, out-of-season crops feed the boss less.</param>
    /// <param name="useGravity">False for gun-style shooter bullets, which fly straight.</param>
    public void Init(CropData data, Vector3 velocity, float strength = 1f, bool useGravity = true)
    {
        crop = data;
        quality = Mathf.Max(0f, strength);
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = useGravity;
        if (!useGravity) lifetime = Mathf.Min(lifetime, 1.5f);
        rb.linearVelocity = velocity;
        // Lobbed crops tumble through the air; gun-style bullets keep pointing along their flight.
        if (useGravity) rb.angularVelocity = Random.onUnitSphere * 6f;
    }

    private void OnEnable() { ActiveCount++; }
    private void OnDisable() { ActiveCount--; }

    private void Start() { Destroy(gameObject, lifetime); }

    private void OnCollisionEnter(Collision collision)
    {
        if (hit) return;
        if (collision.collider.GetComponentInParent<PlayerController>() != null) return;
        hit = true;

        BossHunger boss = collision.collider.GetComponentInParent<BossHunger>();
        if (boss != null && crop != null)
        {
            boss.Feed(crop.fullnessValue * quality);
            if (crop.scareSeconds > 0f) boss.Scare(crop.scareSeconds);
        }

        if (crop == null || crop.type != CropType.Shooter) Landed?.Invoke(transform.position, boss != null); // bullets would machine-gun the squish

        if (splatPrefab != null)
        {
            GameObject fx = Instantiate(splatPrefab, transform.position, Quaternion.identity);
            if (crop != null) FxTint.Apply(fx, crop.color);
            Destroy(fx, 2f);
        }
        Destroy(gameObject);
    }
}
