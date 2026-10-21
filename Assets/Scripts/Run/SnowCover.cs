using DistantLands.Cozy;
using UnityEngine;

/// <summary>
/// Makes snow visibly pile up: whitens the ground and grows scattered drifts as the season (and the day) goes on.
/// The amount comes from <see cref="SnowAccumulation"/>; this class is presentation only. Null-safe without COZY.
/// </summary>
public class SnowCover : MonoBehaviour
{
    [SerializeField] private Renderer ground;
    [SerializeField] private Material moundMaterial;
    [SerializeField] private Color snowColor = new Color(0.95f, 0.97f, 1f);
    [SerializeField, Range(0f, 1f)] private float maxGroundWhite = 0.85f;
    [SerializeField, Min(0)] private int moundCount = 40;
    [SerializeField] private float maxMoundHeight = 0.55f;
    [SerializeField] private Vector2 moundRadius = new Vector2(0.5f, 1.3f);
    [Tooltip("Drifts stay out of this box around the farm so plots, merchant and bell are never buried.")]
    [SerializeField] private Vector3 keepClearCenter = new Vector3(0f, 0f, 6f);
    [SerializeField] private Vector2 keepClearHalfSize = new Vector2(10f, 9f);
    [SerializeField] private float spreadHalfSize = 24f;

    private struct Mound
    {
        public Transform t;
        public float threshold;   // snow amount at which this drift starts to grow
        public float radius;
        public float height;
    }

    private Mound[] mounds;
    private Transform moundRoot;
    private MaterialPropertyBlock block;
    private Color groundBase = Color.white;
    private bool haveGroundBase;

    private float start01, end01, seasonProgress01, dayShare, dayProgress01;
    private float applied = -1f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        if (ground != null && ground.sharedMaterial != null && ground.sharedMaterial.HasProperty(BaseColorId))
        {
            groundBase = ground.sharedMaterial.GetColor(BaseColorId);
            haveGroundBase = true;
        }
        block = new MaterialPropertyBlock();
    }

    /// <summary>Called once per day by <see cref="CozyBridge"/>. Resets the within-day progress to morning.</summary>
    public void SetSeason(CozyWeatherMap.SeasonLook look, float seasonProgress01, float dayShare)
    {
        start01 = look.snowStart01;
        end01 = look.snowEnd01;
        this.seasonProgress01 = seasonProgress01;
        this.dayShare = dayShare;
        dayProgress01 = 0f;
        applied = -1f;
        Refresh();
    }

    /// <summary>0 = morning, 1 = dusk; fed by <see cref="DayClock"/> so snow creeps up while you play.</summary>
    public void SetDayProgress(float progress01)
    {
        dayProgress01 = progress01;
        Refresh();
    }

    private void Refresh()
    {
        float amount = SnowAccumulation.Amount(start01, end01, seasonProgress01, dayProgress01, dayShare);
        if (Mathf.Abs(amount - applied) < 0.002f) return;
        applied = amount;

        CozyWeather cozy = CozyWeather.instance;
        if (cozy != null && cozy.climateModule != null) cozy.climateModule.snowAmount = amount;

        if (ground != null && haveGroundBase)
        {
            ground.GetPropertyBlock(block);
            block.SetColor(BaseColorId, Color.Lerp(groundBase, snowColor, amount * maxGroundWhite));
            ground.SetPropertyBlock(block);
        }

        if (amount > 0f) EnsureMounds();
        if (mounds == null) return;
        for (int i = 0; i < mounds.Length; i++)
        {
            Mound m = mounds[i];
            float grow = Mathf.Clamp01((amount - m.threshold) / Mathf.Max(0.01f, 1f - m.threshold));
            m.t.gameObject.SetActive(grow > 0f);
            if (grow > 0f) m.t.localScale = new Vector3(m.radius * 2f * Mathf.Sqrt(grow), m.height * 2f * grow, m.radius * 2f * Mathf.Sqrt(grow));
        }
    }

    private void EnsureMounds()
    {
        if (mounds != null || moundMaterial == null || moundCount <= 0) return;

        moundRoot = new GameObject("SnowMounds").transform;
        moundRoot.SetParent(transform, false);
        mounds = new Mound[moundCount];

        // Fixed seed: drifts sit in the same places every day, so snow reads as piling up, not reshuffling.
        SeededRng rng = new SeededRng(7391);
        for (int i = 0; i < moundCount; i++)
        {
            Vector3 pos = Vector3.zero;
            for (int tries = 0; tries < 12; tries++)
            {
                pos = new Vector3(rng.Range(-spreadHalfSize, spreadHalfSize), 0f, rng.Range(-spreadHalfSize, spreadHalfSize));
                bool clear = Mathf.Abs(pos.x - keepClearCenter.x) < keepClearHalfSize.x && Mathf.Abs(pos.z - keepClearCenter.z) < keepClearHalfSize.y;
                if (!clear) break;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SnowMound";
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = moundMaterial;
            go.transform.SetParent(moundRoot, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);

            mounds[i] = new Mound
            {
                t = go.transform,
                threshold = rng.Range(0f, 0.6f),
                radius = rng.Range(moundRadius.x, moundRadius.y),
                height = rng.Range(0.5f, 1f) * maxMoundHeight,
            };
        }
    }
}
