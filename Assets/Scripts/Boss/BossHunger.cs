using System;
using UnityEngine;

public class BossHunger : MonoBehaviour
{
    [SerializeField] private BossData data;
    [SerializeField] private float maxGrowScale = 1.5f;
    [SerializeField] private GameObject burstPrefab;

    [SerializeField] private float evolveGraceSeconds = 1.5f;

    private float current;
    private float wobble;
    private float graceUntil;
    private Vector3 baseScale;

    private BossScale scale = BossScale.Identity;

    /// <summary>Fullness needed to satisfy the current stage, including the year's scaling.</summary>
    public float MaxFullness => data != null ? data.maxFullness * scale.fullnessMult : 1f;
    /// <summary>Chase speed of the current stage, including the year's scaling. The boss movers read this.</summary>
    public float ChaseSpeed => data != null ? data.chaseSpeed * scale.speedMult : 0f;
    /// <summary>Hearts taken per hit by the current stage, including the year's scaling.</summary>
    public int Damage => data != null ? data.damage + (data.damage > 0 ? scale.damageBonus : 0) : 0;

    public float Fullness01 => data != null ? Mathf.Clamp01(current / MaxFullness) : 0f;
    public bool IsSatisfied { get; private set; }
    public BossData Data => data;

    public BossScale Scale => scale;

    /// <summary>Sets how much stronger the boss is this year. Applies to every stage (e.g. the butterfly too).</summary>
    public void ApplyScaling(BossScale yearScale) { scale = yearScale; }
    public float EvolveGraceSeconds => evolveGraceSeconds;

    public event Action<float, float> OnBossFed;
    public event Action OnBossSatisfied;
    public event Action<BossData> OnBossEvolved;

    private void Awake() { baseScale = transform.localScale; }

    /// <summary>Swaps in the day's encounter (e.g. an elite). Call before the first feed.</summary>
    public void Configure(BossData encounter)
    {
        if (encounter == null) return;
        data = encounter;
        current = 0f;
        IsSatisfied = false;
        graceUntil = 0f;
        scaredUntil = 0f;
    }

    /// <summary>The boss only eats at night. By day it is asleep, so thrown crops do nothing (no daytime kills or evolutions).</summary>
    public bool CanBeFed => GameManager.Instance == null || GameManager.Instance.Phase == GamePhase.Night;

    private float scaredUntil;

    /// <summary>True while the boss is running away (garlic). The boss movers flee instead of chase and deal no contact damage.</summary>
    public bool IsScared => Time.time < scaredUntil;

    /// <summary>Makes the boss flee for <paramref name="seconds"/>, then it chases again by itself. Night only, like feeding.</summary>
    public void Scare(float seconds)
    {
        if (IsSatisfied || data == null || !CanBeFed || seconds <= 0f) return;
        scaredUntil = Mathf.Max(scaredUntil, Time.time + seconds);
        wobble = 1f;
    }

    public void Feed(float fullness)
    {
        if (IsSatisfied || data == null || Time.time < graceUntil || !CanBeFed) return;
        float max = MaxFullness;
        current = Mathf.Min(max, current + fullness);
        wobble = 1f;
        OnBossFed?.Invoke(current, max);

        if (current >= max && data.nextStage != null)
        {
            SpawnBurst();
            data = data.nextStage;
            current = 0f;
            graceUntil = Time.time + evolveGraceSeconds;
            OnBossEvolved?.Invoke(data);
        }
        else if (current >= max)
        {
            IsSatisfied = true;
            SpawnBurst();
            OnBossSatisfied?.Invoke();
            gameObject.SetActive(false);
        }
    }

    private void SpawnBurst()
    {
        if (burstPrefab == null) return;
        GameObject fx = Instantiate(burstPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        Destroy(fx, 3f);
    }

    private void Update()
    {
        float grow = Mathf.Lerp(1f, maxGrowScale, Fullness01);
        wobble = Mathf.MoveTowards(wobble, 0f, Time.deltaTime * 1.5f);
        float w = 1f + wobble * 0.25f * Mathf.Sin(Time.time * 25f);
        transform.localScale = new Vector3(baseScale.x * grow * w, baseScale.y * grow * (2f - w), baseScale.z * grow * w);
    }
}
