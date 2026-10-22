using UnityEngine;

/// <summary>Boss stage 2: a hovering, weaving chaser. Reads its stats from the BossData the BossHunger currently holds.</summary>
public class ButterflyBoss : MonoBehaviour
{
    [SerializeField] private Transform body;
    [SerializeField] private Transform[] wingPivots;
    [SerializeField] private float hoverHeight = 1.2f;
    [SerializeField] private float bobAmount = 0.25f;
    [SerializeField] private float weaveAmount = 0.8f;
    [SerializeField] private float flapDegrees = 35f;
    [SerializeField] private float contactRange = 1.4f;
    [SerializeField] private float contactCooldown = 1.2f;
    [SerializeField] private float knockbackForce = 14f;

    private Transform player;
    private PlayerHealth playerHealth;
    private PlayerController playerController;
    private BossHunger hunger;
    private bool awake;
    private float nextHitTime;
    private Vector3 groundPos;

    public bool IsAwake => awake;

    private void Awake() { hunger = GetComponentInParent<BossHunger>(); }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerHealth = p.GetComponent<PlayerHealth>();
            playerController = p.GetComponent<PlayerController>();
        }
        if (GameManager.Instance != null) GameManager.Instance.OnPhaseChanged += HandlePhase;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnPhaseChanged -= HandlePhase;
    }

    private void OnEnable() { groundPos = transform.position; }

    public void Wake() { awake = true; }
    public void Sleep() { awake = false; }

    private void HandlePhase(GamePhase phase) { if (phase == GamePhase.Lost) Sleep(); }

    private void Update()
    {
        AnimateWings();
        if (!awake || player == null || hunger == null || hunger.Data == null) return;

        // Stats come through BossHunger so the year's scaling applies to this stage too.
        float chaseSpeed = hunger.ChaseSpeed;
        int hitDamage = hunger.Damage;
        Vector3 to = player.position - groundPos;
        to.y = 0f;
        bool scared = hunger.IsScared; // garlic: flee instead of chase, no contact damage
        if (to.sqrMagnitude > 0.01f)
        {
            Vector3 dir = scared ? -to.normalized : to.normalized;
            Vector3 side = new Vector3(-dir.z, 0f, dir.x) * (Mathf.Sin(Time.time * 2.5f) * weaveAmount);
            if (!hunger.IsStaggered || scared) groundPos += (dir * chaseSpeed + side) * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        }
        float y = hoverHeight + Mathf.Sin(Time.time * 3f) * bobAmount;
        transform.position = new Vector3(groundPos.x, y, groundPos.z);

        if (!scared && to.magnitude <= contactRange && Time.time >= nextHitTime)
        {
            nextHitTime = Time.time + contactCooldown;
            if (playerHealth != null) playerHealth.TakeDamage(hitDamage);
            if (playerController != null) playerController.ApplyKnockback(to.normalized * knockbackForce);
        }
    }

    private void AnimateWings()
    {
        if (wingPivots == null) return;
        float flap = Mathf.Sin(Time.time * (awake ? 14f : 4f)) * flapDegrees;
        for (int i = 0; i < wingPivots.Length; i++)
        {
            if (wingPivots[i] == null) continue;
            float sign = i % 2 == 0 ? 1f : -1f;
            wingPivots[i].localRotation = Quaternion.Euler(0f, 0f, sign * flap);
        }
    }
}
