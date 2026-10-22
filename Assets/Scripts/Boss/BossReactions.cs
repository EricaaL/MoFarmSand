using UnityEngine;

/// <summary>
/// Presentation layer on the boss: a "!" pops up when garlic scares it, big hits stagger it for a beat, and it blushes and
/// sweats as it fills up (tiers come from BossData). Works for both stages because they share one BossHunger.
/// </summary>
[RequireComponent(typeof(BossHunger))]
public class BossReactions : MonoBehaviour
{
    [SerializeField] private Renderer[] tintRenderers;
    [SerializeField] private GameObject scareMarkPrefab;
    [SerializeField] private GameObject sweatPrefab;
    [Tooltip("A feed has to add at least this much fullness to stagger the boss, so rapid-fire peas never pin it in place.")]
    [SerializeField] private float staggerMinFullness = 10f;
    [SerializeField] private float staggerSeconds = 0.15f;
    [SerializeField] private Color blushColor = new Color(1f, 0.5f, 0.62f);
    [Range(0f, 1f)] [SerializeField] private float blushStrength = 0.75f;
    [SerializeField] private Vector3 markOffset = new Vector3(0f, 1.9f, 0f);
    [SerializeField] private float markScale = 1.3f;
    [SerializeField] private Vector3 sweatOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float blushSweatRate = 6f;
    [SerializeField] private float stuffedSweatRate = 16f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private BossHunger hunger;
    private Color[] baseColors;
    private MaterialPropertyBlock block;
    private GameObject mark;
    private ParticleSystem sweat;
    private Camera cam;
    private float lastFullness;
    private float markUntil;
    private float markStart;
    private int tier;

    private void Awake()
    {
        hunger = GetComponent<BossHunger>();
        block = new MaterialPropertyBlock();
        if (tintRenderers != null)
        {
            baseColors = new Color[tintRenderers.Length];
            for (int i = 0; i < tintRenderers.Length; i++)
            {
                Renderer r = tintRenderers[i];
                baseColors[i] = r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId)
                    ? r.sharedMaterial.GetColor(BaseColorId) : Color.white;
            }
        }
        if (scareMarkPrefab != null)
        {
            mark = Instantiate(scareMarkPrefab, transform);
            mark.transform.localPosition = markOffset;
            mark.SetActive(false);
        }
        if (sweatPrefab != null)
        {
            GameObject go = Instantiate(sweatPrefab, transform);
            go.transform.localPosition = sweatOffset;
            sweat = go.GetComponent<ParticleSystem>();
            SetSweatRate(0f);
        }
    }

    private void OnEnable()
    {
        hunger.OnBossFed += HandleFed;
        hunger.OnScared += HandleScared;
        hunger.OnBossEvolved += HandleEvolved;
        ResetTier();
    }

    private void OnDisable()
    {
        hunger.OnBossFed -= HandleFed;
        hunger.OnScared -= HandleScared;
        hunger.OnBossEvolved -= HandleEvolved;
        if (mark != null) mark.SetActive(false);
    }

    /// <summary>Re-applies the blush after something else (the feed flash) reset the renderers' property blocks.</summary>
    public void Reapply()
    {
        if (tintRenderers == null) return;
        float amount = tier == 2 ? blushStrength : tier == 1 ? blushStrength * 0.5f : 0f;
        for (int i = 0; i < tintRenderers.Length; i++)
        {
            Renderer r = tintRenderers[i];
            if (r == null) continue;
            if (amount <= 0f) { r.SetPropertyBlock(null); continue; }
            block.SetColor(BaseColorId, Color.Lerp(baseColors[i], blushColor, amount));
            r.SetPropertyBlock(block);
        }
    }

    private void HandleFed(float current, float max)
    {
        float added = Mathf.Max(0f, current - lastFullness);
        lastFullness = current;
        if (added >= staggerMinFullness) hunger.Stagger(staggerSeconds);
        SetTier(TierFor(hunger.Fullness01));
    }

    private void HandleEvolved(BossData next) { ResetTier(); }

    private void HandleScared(float seconds)
    {
        if (mark == null) return;
        mark.SetActive(true);
        markStart = Time.time;
        markUntil = Time.time + Mathf.Min(seconds, 1.2f);
    }

    private int TierFor(float fullness01)
    {
        BossData data = hunger.Data;
        if (data == null) return 0;
        return fullness01 >= data.stuffedTier ? 2 : fullness01 >= data.blushTier ? 1 : 0;
    }

    private void ResetTier()
    {
        lastFullness = 0f;
        tier = -1; // force the apply
        SetTier(0);
    }

    private void SetTier(int next)
    {
        if (next == tier) return;
        tier = next;
        SetSweatRate(next == 2 ? stuffedSweatRate : next == 1 ? blushSweatRate : 0f);
        Reapply();
    }

    private void SetSweatRate(float rate)
    {
        if (sweat == null) return;
        ParticleSystem.EmissionModule em = sweat.emission;
        em.rateOverTime = rate;
        if (rate > 0f && !sweat.isPlaying) sweat.Play();
    }

    private void LateUpdate()
    {
        if (mark == null || !mark.activeSelf) return;
        if (Time.time >= markUntil) { mark.SetActive(false); return; }
        if (cam == null) cam = Camera.main;
        // Pop in with a little overshoot, then hold. Scale is divided by the boss scale so wobble doesn't distort the mark.
        float t = Mathf.Clamp01((Time.time - markStart) / 0.2f);
        float pop = markScale * (t < 1f ? 1f + 0.4f * Mathf.Sin(t * Mathf.PI) : 1f);
        Vector3 s = transform.lossyScale;
        mark.transform.localScale = new Vector3(pop / s.x, pop / s.y, pop / s.z);
        if (cam != null) mark.transform.rotation = cam.transform.rotation;
    }
}
