using UnityEngine;

public class CaterpillarBoss : MonoBehaviour
{
    [SerializeField] private BossData data;
    [SerializeField] private Transform[] segments;
    [SerializeField] private float segmentSpacing = 0.9f;
    [SerializeField] private float contactRange = 1.3f;
    [SerializeField] private float biteCooldown = 2.5f;
    [Tooltip("Seconds the caterpillar stays in place chewing after a bite, before it chases again.")]
    [SerializeField] private float biteRecoverSeconds = 1.5f;
    [SerializeField] private float knockbackForce = 14f;

    [Header("Slime spit")]
    [SerializeField] private float spitCooldown = 6f;
    [SerializeField] private float spitMinRange = 3f;
    [SerializeField] private float spitMaxRange = 12f;
    [Tooltip("Seconds the caterpillar rears up and stands still before the glob flies.")]
    [SerializeField] private float spitWindup = 0.4f;
    [SerializeField] private SlimePuddle.Settings slime = new SlimePuddle.Settings
    {
        radius = 1.4f, seconds = 5f, speedMult = 0.5f, color = new Color(0.45f, 0.8f, 0.2f, 0.6f)
    };

    private Transform player;
    private PlayerHealth playerHealth;
    private PlayerController playerController;
    private BossHunger hunger;
    private bool awake;
    private float nextHitTime;
    private float holdUntil;
    private float nextSpitTime;
    private float windupUntil;
    private Vector3[] segBase;

    public bool IsAwake => awake;

    private void Awake()
    {
        hunger = GetComponent<BossHunger>();
        if (segments != null)
        {
            segBase = new Vector3[segments.Length];
            for (int i = 0; i < segments.Length; i++) segBase[i] = segments[i].localScale;
        }
    }

    private void OnEnable()
    {
        if (hunger != null) hunger.OnBossSatisfied += Sleep;
    }

    private void OnDisable()
    {
        if (hunger != null) hunger.OnBossSatisfied -= Sleep;
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;
        player = p.transform;
        playerHealth = p.GetComponent<PlayerHealth>();
        playerController = p.GetComponent<PlayerController>();
    }

    public void Wake()
    {
        awake = true;
        nextSpitTime = Time.time + spitCooldown; // no spit the instant the fight starts
    }

    public void Sleep()
    {
        awake = false;
        windupUntil = 0f;
    }

    private bool Holding => Time.time < holdUntil;
    private bool Winding => windupUntil > 0f;

    /// <summary>True while it stays put chewing after a bite. The paper face reads this to chomp.</summary>
    public bool IsChewing => Holding;
    /// <summary>True while it rears up to spit. The paper face reads this to open wide.</summary>
    public bool IsWinding => Winding;

    private void Update()
    {
        // BossHunger owns the day's encounter (an elite swaps it in) and the year's scaling; the local field is the fallback.
        bool fromHunger = hunger != null && hunger.Data != null;
        BossData stats = fromHunger ? hunger.Data : data;
        if (!awake || player == null || stats == null) return;
        float chaseSpeed = fromHunger ? hunger.ChaseSpeed : stats.chaseSpeed;
        int hitDamage = fromHunger ? hunger.Damage : stats.damage;

        Vector3 to = player.position - transform.position;
        to.y = 0f;
        bool scared = hunger != null && hunger.IsScared;
        if (to.sqrMagnitude > 0.01f)
        {
            // Scared (garlic): run directly away from the player; chasing resumes when the timer ends.
            Vector3 heading = scared ? -to : to;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), 8f * Time.deltaTime);
            // Chewing after a bite or rearing up to spit: stay put (fleeing from garlic still wins).
            bool staggered = hunger != null && hunger.IsStaggered;
            if (scared || (!Holding && !Winding && !staggered)) transform.position += heading.normalized * chaseSpeed * Time.deltaTime;
        }

        if (scared)
        {
            windupUntil = 0f;
            return;
        }
        if (to.magnitude <= contactRange && Time.time >= nextHitTime)
        {
            nextHitTime = Time.time + biteCooldown;
            holdUntil = Time.time + biteRecoverSeconds;
            windupUntil = 0f;
            if (playerHealth != null) playerHealth.TakeDamage(hitDamage);
            if (playerController != null) playerController.ApplyKnockback(to.normalized * knockbackForce);
        }

        UpdateSpit(to.magnitude);
    }

    private void UpdateSpit(float distance)
    {
        if (Holding) return;
        if (!Winding)
        {
            if (Time.time >= nextSpitTime && distance >= spitMinRange && distance <= spitMaxRange)
                windupUntil = Time.time + spitWindup;
            return;
        }
        if (Time.time < windupUntil) return;

        windupUntil = 0f;
        nextSpitTime = Time.time + spitCooldown;
        Vector3 mouth = transform.position + transform.forward * 0.8f + Vector3.up * 0.9f;
        SlimeGlob.Spawn(mouth, player.position, slime);
    }

    private void LateUpdate()
    {
        if (segments == null) return;
        Transform leader = transform;
        for (int i = 0; i < segments.Length; i++)
        {
            Transform seg = segments[i];
            Vector3 dir = seg.position - leader.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = -leader.forward;
            Vector3 pos = leader.position + dir.normalized * segmentSpacing;
            pos.y = seg.position.y;
            seg.position = Vector3.Lerp(seg.position, pos, 20f * Time.deltaTime);

            // Bigger, faster squash while chewing or rearing up so the pause reads on camera.
            float amp = Holding || Winding ? 0.25f : 0.12f;
            float freq = Holding ? 18f : 8f;
            float squash = awake ? 1f + amp * Mathf.Sin(Time.time * freq - i * 1.2f) : 1f;
            seg.localScale = new Vector3(segBase[i].x * squash, segBase[i].y * (2f - squash), segBase[i].z * squash);
            leader = seg;
        }
    }
}
