using UnityEngine;
using UnityEngine.AI;

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

    [Header("Collision")]
    [Tooltip("Radius of the sphere swept ahead of the head; a bit under the head collider so it clears the ground.")]
    [SerializeField] private float bodyRadius = 0.4f;
    [Tooltip("Raises the swept sphere so low obstacles (farm plots) pass under it and only taller scenery blocks.")]
    [SerializeField] private float castLift = 0.3f;
    [SerializeField] private float skin = 0.05f;

    [Header("Pathfinding")]
    [Tooltip("How often the route to the player is recalculated on the baked NavMesh (MoFarmSand > Bake Boss NavMesh).")]
    [SerializeField] private float repathSeconds = 0.25f;
    [SerializeField] private float cornerReachedDistance = 0.4f;
    [Tooltip("How far off the NavMesh the boss or player may be and still be snapped onto it.")]
    [SerializeField] private float sampleRange = 2f;

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
    private readonly RaycastHit[] moveHits = new RaycastHit[8];
    private NavMeshPath path;
    private readonly Vector3[] corners = new Vector3[32];
    private int cornerCount;
    private int cornerIndex;
    private float nextRepathTime;
    private readonly StuckTracker stuck = new StuckTracker();
    private Vector3 escapeHeading;
    private Vector3 lastBlockNormal;

    public bool IsAwake => awake;

    private void Awake()
    {
        hunger = GetComponent<BossHunger>();
        path = new NavMeshPath();
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
            Vector3 heading = scared ? -to : stuck.Escaping ? escapeHeading : ChaseHeading(to);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), 8f * Time.deltaTime);
            // Chewing after a bite or rearing up to spit: stay put (fleeing from garlic still wins).
            bool staggered = hunger != null && hunger.IsStaggered;
            if (scared || (!Holding && !Winding && !staggered))
            {
                Vector3 before = transform.position;
                lastBlockNormal = Vector3.zero;
                Vector3 step = heading.normalized * chaseSpeed * Time.deltaTime;
                Move(step);
                // Wedged between scenery (barn and a crate): sidestep along the wall for a moment, then chase again.
                if (!scared && stuck.Tick(Time.deltaTime, step.magnitude, Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude))
                    escapeHeading = EscapeHeading(to);
            }
            else stuck.Reset();
        }

        if (scared)
        {
            windupUntil = 0f;
            return;
        }
        // The boss grows as it fills up and its collider keeps the farmer that much farther from the centre, so the reach grows with it.
        float reach = contactRange * (hunger != null ? hunger.GrowScale : 1f);
        if (to.magnitude <= reach && Time.time >= nextHitTime)
        {
            nextHitTime = Time.time + biteCooldown;
            holdUntil = Time.time + biteRecoverSeconds;
            windupUntil = 0f;
            if (playerHealth != null) playerHealth.TakeDamage(hitDamage);
            if (playerController != null) playerController.ApplyKnockback(to.normalized * knockbackForce);
        }

        UpdateSpit(to.magnitude);
    }

    /// <summary>
    /// Direction to walk toward the player: the next corner of the shortest NavMesh route, so it goes around the barn
    /// instead of pushing into it. Falls back to straight at the player when there is no NavMesh, no route, or on the last leg.
    /// </summary>
    private Vector3 ChaseHeading(Vector3 toPlayer)
    {
        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + repathSeconds;
            Repath();
        }

        while (cornerIndex < cornerCount)
        {
            Vector3 flat = corners[cornerIndex] - transform.position;
            flat.y = 0f;
            if (flat.magnitude > cornerReachedDistance) return flat;
            cornerIndex++;
        }
        return toPlayer;
    }

    /// <summary>Along the wall it is pressed against, leaning a little away from it; straight sideways if no wall was seen.</summary>
    private Vector3 EscapeHeading(Vector3 toPlayer)
    {
        Vector3 normal = lastBlockNormal.sqrMagnitude > 0.0001f ? lastBlockNormal.normalized : -toPlayer.normalized;
        Vector3 along = Vector3.Cross(Vector3.up, normal) * stuck.Side;
        Vector3 heading = along + normal * 0.5f;
        heading.y = 0f;
        return heading.sqrMagnitude > 0.0001f ? heading : -toPlayer;
    }

    private void Repath()
    {
        cornerCount = 0;
        cornerIndex = 0;
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit from, sampleRange, NavMesh.AllAreas)) return;
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit to, sampleRange, NavMesh.AllAreas)) return;
        if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) || path.status == NavMeshPathStatus.PathInvalid) return;
        cornerCount = path.GetCornersNonAlloc(corners);
        cornerIndex = Mathf.Min(1, cornerCount); // corner 0 is where it stands now
    }

    /// <summary>Kinematic collide-and-slide: stops at solid scenery (barn, fences) and slides along it instead of passing through.</summary>
    private void Move(Vector3 delta)
    {
        for (int pass = 0; pass < 2 && delta.sqrMagnitude > 0.000001f; pass++)
        {
            float dist = delta.magnitude;
            Vector3 castOrigin = transform.position + Vector3.up * castLift;
            int n = Physics.SphereCastNonAlloc(castOrigin, bodyRadius, delta / dist, moveHits, dist + skin,
                ~0, QueryTriggerInteraction.Ignore);

            int best = -1;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = moveHits[i];
                if (h.transform.IsChildOf(transform)) continue;
                if (player != null && h.transform.IsChildOf(player)) continue;   // the bite logic owns player contact
                if (h.rigidbody != null && !h.rigidbody.isKinematic) continue;   // thrown crops, loose props
                if (h.normal.y > 0.5f) continue;                                 // floor and farm plots
                // Already touching it: a cast from inside reports distance 0 in every direction, so only block steps that push in.
                if (h.distance <= 0.0001f && !PushesInto(h.collider, castOrigin, delta)) continue;
                if (best < 0 || h.distance < moveHits[best].distance) best = i;
            }

            if (best < 0)
            {
                transform.position += delta;
                return;
            }

            RaycastHit hit = moveHits[best];
            transform.position += delta / dist * Mathf.Max(0f, hit.distance - skin);
            Vector3 normal = hit.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.0001f) return;
            lastBlockNormal = normal;
            Vector3 left = delta / dist * Mathf.Max(0f, dist - hit.distance);
            delta = Vector3.ProjectOnPlane(left, normal.normalized);
            delta.y = 0f;
        }
    }

    /// <summary>True if stepping by <paramref name="delta"/> moves toward a collider the head already overlaps; sliding along or away is fine.</summary>
    private static bool PushesInto(Collider other, Vector3 origin, Vector3 delta)
    {
        // Closest point is undefined on a concave mesh: use its centre instead.
        Vector3 closest = other is MeshCollider mc && !mc.convex ? other.bounds.center : other.ClosestPoint(origin);
        Vector3 away = origin - closest;
        away.y = 0f;
        if (away.sqrMagnitude < 0.000001f) return true; // centre inside it: no clean way out, so treat it as solid
        return Vector3.Dot(delta, away) < -0.0001f;
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
