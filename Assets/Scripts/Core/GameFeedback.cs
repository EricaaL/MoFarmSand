using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>Presentation layer: camera shake, hit-stop and boss flash, driven by events at the seams.</summary>
[RequireComponent(typeof(CinemachineImpulseSource))]
public class GameFeedback : MonoBehaviour
{
    [SerializeField] private BossHunger hunger;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private TrailerDirector trailer;
    [SerializeField] private Renderer[] bossRenderers;
    [SerializeField] private float feedShake = 0.25f;
    [SerializeField] private float burstShake = 1.2f;
    [SerializeField] private float hitShake = 0.6f;
    [SerializeField] private float hitStopSeconds = 0.06f;
    [SerializeField] private float burstCutSeconds = 2.2f;

    private CinemachineImpulseSource impulse;
    private MaterialPropertyBlock block;
    private int lastHp = -1;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        impulse = GetComponent<CinemachineImpulseSource>();
        block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (hunger != null)
        {
            hunger.OnBossFed += HandleFed;
            hunger.OnBossSatisfied += HandleBurst;
            hunger.OnBossEvolved += HandleEvolved;
        }
        if (playerHealth != null)
        {
            lastHp = playerHealth.Hp;
            playerHealth.OnChanged += HandleHealthChanged;
        }
    }

    private void OnDisable()
    {
        if (hunger != null)
        {
            hunger.OnBossFed -= HandleFed;
            hunger.OnBossSatisfied -= HandleBurst;
            hunger.OnBossEvolved -= HandleEvolved;
        }
        if (playerHealth != null) playerHealth.OnChanged -= HandleHealthChanged;
        Time.timeScale = 1f;
    }

    /// <summary>Camera shake on demand (disaster strikes etc.).</summary>
    public void Shake(float strength) { impulse.GenerateImpulseWithForce(strength); }

    private void HandleFed(float current, float max)
    {
        impulse.GenerateImpulseWithForce(feedShake);
        StartCoroutine(FlashBoss());
    }

    private void HandleEvolved(BossData next) { HandleBurst(); }

    private void HandleBurst()
    {
        impulse.GenerateImpulseWithForce(burstShake);
        StartCoroutine(HitStop());
        if (trailer != null) trailer.CutToBurst(burstCutSeconds);
    }

    private void HandleHealthChanged()
    {
        if (playerHealth.Hp < lastHp) impulse.GenerateImpulseWithForce(hitShake);
        lastHp = playerHealth.Hp;
    }

    private IEnumerator HitStop()
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(hitStopSeconds);
        Time.timeScale = 1f;
    }

    private IEnumerator FlashBoss()
    {
        SetBossColor(Color.white);
        yield return new WaitForSeconds(0.08f);
        foreach (Renderer r in bossRenderers) if (r != null) r.SetPropertyBlock(null);
    }

    private void SetBossColor(Color c)
    {
        block.SetColor(BaseColorId, c);
        foreach (Renderer r in bossRenderers) if (r != null) r.SetPropertyBlock(block);
    }
}
