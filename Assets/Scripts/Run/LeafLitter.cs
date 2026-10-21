using UnityEngine;

/// <summary>
/// Fallen leaves lying on the ground for the whole season (autumn). Unlike the falling-leaf particles they never fade.
/// Built once with a fixed seed, so the piles start in the same places every day. Wind gusts slide and lift them, and
/// the player kicks them aside when walking past; a loose leash keeps them from drifting off for good. Presentation only.
/// </summary>
public class LeafLitter : MonoBehaviour
{
    [Tooltip("Flat leaf prefabs (MeshFilter + MeshRenderer, lying flat, face up). One is picked at random per leaf.")]
    [SerializeField] private GameObject[] leafPrefabs;
    [SerializeField, Min(1)] private int clusterCount = 18;
    [SerializeField] private float clusterRadius = 2.2f;
    [Tooltip("Share of leaves lying alone instead of in a pile.")]
    [SerializeField, Range(0f, 1f)] private float stragglerShare = 0.25f;
    [Tooltip("Length of a leaf in world units (its longest side); each leaf picks a size in this range.")]
    [SerializeField] private Vector2 leafSize = new Vector2(0.4f, 0.7f);
    [Tooltip("Each leaf takes a random colour from this gradient, multiplied with the leaf texture (red to yellow).")]
    [SerializeField] private Gradient leafColors = DefaultLeafColors();
    [Tooltip("Leaves stay out of this box over the starting plots so the soil stays clean.")]
    [SerializeField] private Vector3 keepClearCenter = new Vector3(0f, 0f, 5f);
    [SerializeField] private Vector2 keepClearHalfSize = new Vector2(4f, 4f);
    [Tooltip("Leaves are scattered in a square of this half size around the spread centre (x, z), roughly what the camera sees.")]
    [SerializeField] private Vector2 spreadCenter = new Vector2(0f, 5f);
    [SerializeField] private float spreadHalfSize = 18f;
    [SerializeField, Min(1)] private int maxLeaves = 600;

    [Header("Wind")]
    [Tooltip("Slides leaves along the wind during a gust. Gusts sweep across the field, so not every leaf moves at once.")]
    [SerializeField] private float windForce = 3.5f;
    [Tooltip("Upward push in a gust; has to beat Gravity for leaves to lift off.")]
    [SerializeField] private float windLift = 4.2f;
    [Tooltip("Perlin value above which a gust starts (0..1). Higher = rarer gusts.")]
    [SerializeField, Range(0.3f, 0.9f)] private float gustThreshold = 0.62f;

    [Header("Player")]
    [Tooltip("The farmer. Found by the Player tag if left empty.")]
    [SerializeField] private Transform player;
    [SerializeField] private float disturbRadius = 1.4f;
    [SerializeField] private float kickForce = 9f;
    [SerializeField] private float hopLift = 9f;
    [Tooltip("Player speed (units/s) at which the kick reaches full strength; standing still barely stirs them.")]
    [SerializeField] private float fullKickSpeed = 3f;

    [Header("Motion")]
    [SerializeField] private float gravity = 3.5f;
    [Tooltip("Wind and kicks stop lifting a leaf above this height.")]
    [SerializeField] private float maxHeight = 0.4f;
    [SerializeField] private float maxRiseSpeed = 1.4f;
    [SerializeField] private float groundFriction = 5f;
    [SerializeField] private float airDrag = 1.5f;
    [SerializeField] private float spinSpeed = 7f;
    [Tooltip("Leaves farther than this from where they landed are eased back, so the litter never blows away.")]
    [SerializeField] private float leash = 3f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private static Gradient DefaultLeafColors()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.85f, 0.18f, 0.12f), 0f),    // red
                new GradientColorKey(new Color(0.95f, 0.50f, 0.15f), 0.5f),  // orange
                new GradientColorKey(new Color(1.00f, 0.85f, 0.30f), 1f),    // yellow
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    private struct LeafState
    {
        public Vector3 home;       // where it first landed (local to the leaf root)
        public Vector3 pos;
        public Vector3 vel;
        public float yaw;
        public float yawVel;
        public float lightness;    // lighter leaves are pushed around more
        public float spin;         // -1 or +1: which way it tumbles
    }

    private Transform leafRoot;
    private Transform[] leaves;
    private LeafState[] state;
    private int shown = -1;
    private Vector3 lastPlayer;
    private bool playerSeen;

    /// <summary>Shows the first <paramref name="count"/> leaves and hides the rest (0 hides all, e.g. outside autumn).</summary>
    public void SetCount(int count)
    {
        count = Mathf.Clamp(count, 0, maxLeaves);
        if (count > 0) Build();
        if (leaves == null || count == shown) return;
        shown = count;
        for (int i = 0; i < leaves.Length; i++) leaves[i].gameObject.SetActive(i < count);
    }

    private void Update() => Step(Time.deltaTime, Time.time);

    /// <summary>Advances the leaves by <paramref name="dt"/> seconds at game time <paramref name="time"/> (wind noise is time-based).</summary>
    public void Step(float dt, float time)
    {
        if (leaves == null || shown <= 0) return;
        dt = Mathf.Min(dt, 0.05f);   // frozen screens run at timeScale 0
        if (dt <= 0f) return;

        // The wind direction wanders slowly; gusts come from noise that drifts over the field.
        float windAngle = (35f + 40f * Mathf.Sin(time * 0.05f)) * Mathf.Deg2Rad;
        Vector3 windDir = new Vector3(Mathf.Cos(windAngle), 0f, Mathf.Sin(windAngle));

        Vector3 playerLocal = Vector3.zero;
        float playerSpeed01 = 0f;
        if (player != null)
        {
            Vector3 p = player.position;
            if (playerSeen) playerSpeed01 = Mathf.Clamp01((p - lastPlayer).magnitude / dt / Mathf.Max(0.01f, fullKickSpeed));
            lastPlayer = p;
            playerSeen = true;
            playerLocal = leafRoot.InverseTransformPoint(p);
        }

        float airDamp = Mathf.Exp(-airDrag * dt);
        float groundDamp = Mathf.Exp(-groundFriction * dt);
        float spinDamp = Mathf.Exp(-3f * dt);

        for (int i = 0; i < shown; i++)
        {
            ref LeafState s = ref state[i];
            float baseY = s.home.y;
            float height = s.pos.y - baseY;
            bool grounded = height <= 0.001f;

            Vector3 acc = Vector3.zero;
            float lift = 0f;

            float gust = Mathf.PerlinNoise(time * 0.12f + s.pos.x * 0.04f, s.pos.z * 0.04f + 17f);
            gust = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(gustThreshold, gustThreshold + 0.25f, gust));
            if (gust > 0f)
            {
                acc += windDir * (gust * windForce * s.lightness);
                if (height < maxHeight) lift += gust * windLift * s.lightness;
            }

            float kick = 0f;
            if (player != null && playerSpeed01 > 0f)
            {
                Vector3 away = s.pos - playerLocal;
                away.y = 0f;
                float dist = away.magnitude;
                if (dist < disturbRadius)
                {
                    kick = (1f - dist / disturbRadius) * playerSpeed01;
                    Vector3 dir = dist > 0.001f ? away / dist : windDir;
                    acc += dir * (kickForce * kick * s.lightness);
                    if (height < maxHeight) lift += hopLift * kick;
                    s.yawVel = Mathf.Lerp(s.yawVel, s.spin * spinSpeed, Mathf.Min(1f, kick * dt * 8f));
                }
            }

            // Fully at rest and nothing pushing it: skip the work.
            bool resting = grounded && s.vel.sqrMagnitude < 0.0004f && Mathf.Abs(s.yawVel) < 0.01f;
            if (resting && acc == Vector3.zero && lift <= 0f) continue;

            s.vel.x += acc.x * dt;
            s.vel.z += acc.z * dt;
            s.vel.y += (lift - gravity) * dt;
            if (lift > 0f) s.vel.y = Mathf.Min(s.vel.y, maxRiseSpeed);

            float damp = grounded ? groundDamp : airDamp;
            s.vel.x *= damp;
            s.vel.z *= damp;

            Vector3 offset = s.pos - s.home;
            offset.y = 0f;
            float over = offset.magnitude - leash;
            if (over > 0f)
            {
                Vector3 back = -offset.normalized * (over * 6f * dt);
                s.vel.x += back.x;
                s.vel.z += back.z;
            }

            s.pos += s.vel * dt;
            if (s.pos.y <= baseY)
            {
                s.pos.y = baseY;
                if (s.vel.y < 0f) s.vel.y = 0f;
            }

            s.yawVel *= spinDamp;
            if (Mathf.Abs(s.yawVel) < 0.01f) s.yawVel = 0f;
            s.yaw += s.yawVel * dt * Mathf.Rad2Deg;

            // Tip up while airborne so a lifted leaf reads as tumbling, then lie flat again on landing.
            float tilt = Mathf.Min((s.pos.y - baseY) * 90f, 45f) * s.spin;
            leaves[i].SetLocalPositionAndRotation(s.pos, Quaternion.Euler(tilt, s.yaw, 0f));
        }
    }

    private void Build()
    {
        if (leaves != null || leafPrefabs == null || leafPrefabs.Length == 0) return;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        leafRoot = new GameObject("FallenLeaves").transform;
        leafRoot.SetParent(transform, false);
        leaves = new Transform[maxLeaves];
        state = new LeafState[maxLeaves];
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        // Own stream for colours, so changing the palette never shifts where the leaves lie.
        SeededRng colorRng = new SeededRng(9911);

        // Fixed seed: leaves start in the same places every day instead of reshuffling.
        SeededRng rng = new SeededRng(4417);
        Vector3[] centers = new Vector3[clusterCount];
        for (int c = 0; c < clusterCount; c++) centers[c] = RandomSpot(rng);

        for (int i = 0; i < maxLeaves; i++)
        {
            Vector3 pos;
            if (rng.NextFloat() < stragglerShare) pos = RandomSpot(rng);
            else
            {
                Vector3 center = centers[rng.NextInt(0, clusterCount)];
                // Square the distance so piles are dense in the middle and thin out at the edge.
                float r = clusterRadius * rng.NextFloat() * rng.NextFloat();
                float a = rng.Range(0f, Mathf.PI * 2f);
                pos = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (InKeepClear(pos)) pos = RandomSpot(rng);
            }

            GameObject prefab = leafPrefabs[rng.NextInt(0, leafPrefabs.Length)];
            if (prefab == null) prefab = leafPrefabs[0];

            // The pivot is what moves and spins. The leaf mesh hangs off the stem end, so the model sits in a
            // child shifted by minus its bounds centre: yaw and tilt then turn the leaf around its middle.
            GameObject pivot = new GameObject("FallenLeaf");
            GameObject model = Instantiate(prefab, pivot.transform);
            float size = rng.Range(leafSize.x, leafSize.y);
            float yaw = rng.Range(0f, 360f);
            MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
            Bounds b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            float scale = size / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.y, b.size.z));
            model.transform.localScale = Vector3.one * scale;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localPosition = -b.center * scale;

            block.SetColor(BaseColorId, leafColors.Evaluate(colorRng.NextFloat()));
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);

            // Tiny height offsets stop z-fighting between overlapping leaves.
            Vector3 local = new Vector3(pos.x, 0.02f + rng.Range(0f, 0.03f), pos.z);
            Transform t = pivot.transform;
            t.SetParent(leafRoot, false);
            t.SetLocalPositionAndRotation(local, Quaternion.Euler(0f, yaw, 0f));
            leaves[i] = t;
            state[i] = new LeafState
            {
                home = local, pos = local, yaw = yaw,
                lightness = rng.Range(0.6f, 1.4f),
                spin = rng.NextFloat() < 0.5f ? -1f : 1f,
            };
            pivot.SetActive(false);
        }
    }

    private Vector3 RandomSpot(SeededRng rng)
    {
        Vector3 pos = Vector3.zero;
        for (int tries = 0; tries < 12; tries++)
        {
            pos = new Vector3(spreadCenter.x + rng.Range(-spreadHalfSize, spreadHalfSize), 0f, spreadCenter.y + rng.Range(-spreadHalfSize, spreadHalfSize));
            if (!InKeepClear(pos)) break;
        }
        return pos;
    }

    private bool InKeepClear(Vector3 p)
        => Mathf.Abs(p.x - keepClearCenter.x) < keepClearHalfSize.x && Mathf.Abs(p.z - keepClearCenter.z) < keepClearHalfSize.y;
}
