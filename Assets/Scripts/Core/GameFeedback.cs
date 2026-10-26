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
    [SerializeField] private Color playerHitColor = new Color(1f, 0.15f, 0.15f);
    [SerializeField] private float playerFlashSeconds = 0.35f;
    [SerializeField] private Color zapBlue = new Color(0.25f, 0.55f, 1f);
    [SerializeField] private float zapFlashSeconds = 0.5f;
    [SerializeField] private float zapFlickerSeconds = 0.05f;
    [SerializeField] private float hitStopSeconds = 0.06f;
    [SerializeField] private float burstCutSeconds = 2.2f;

    private CinemachineImpulseSource impulse;
    private MaterialPropertyBlock block;
    private MaterialPropertyBlock playerBlock;
    private Coroutine playerFlash;
    private BossReactions reactions;
    private int lastHp = -1;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        impulse = GetComponent<CinemachineImpulseSource>();
        block = new MaterialPropertyBlock();
        playerBlock = new MaterialPropertyBlock();
        if (hunger != null) reactions = hunger.GetComponent<BossReactions>();
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
            playerHealth.OnRestored += HandleHealthRestored;
        }
        StrikeHazardEffect.PlayerStruck += HandleStruck;
    }

    private void OnDisable()
    {
        if (hunger != null)
        {
            hunger.OnBossFed -= HandleFed;
            hunger.OnBossSatisfied -= HandleBurst;
            hunger.OnBossEvolved -= HandleEvolved;
        }
        if (playerHealth != null)
        {
            playerHealth.OnChanged -= HandleHealthChanged;
            playerHealth.OnRestored -= HandleHealthRestored;
        }
        StrikeHazardEffect.PlayerStruck -= HandleStruck;
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
        if (playerHealth.Hp < lastHp)
        {
            impulse.GenerateImpulseWithForce(hitShake);
            if (playerFlash != null) StopCoroutine(playerFlash);
            playerFlash = StartCoroutine(FlashPlayer());
        }
        lastHp = playerHealth.Hp;
    }

    /// <summary>The dog goes red the moment it is hurt, then fades back to its own colours.</summary>
    private IEnumerator FlashPlayer()
    {
        Renderer[] renderers = playerHealth.GetComponentsInChildren<Renderer>();
        for (float t = 0f; t < playerFlashSeconds; t += Time.unscaledDeltaTime)
        {
            playerBlock.SetColor(BaseColorId, Color.Lerp(playerHitColor, Color.white, t / playerFlashSeconds));
            foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(playerBlock);
            yield return null;
        }
        foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(null);
        playerFlash = null;
    }

    private void HandleStruck()
    {
        if (playerHealth == null) return;
        if (playerFlash != null) StopCoroutine(playerFlash);
        playerFlash = StartCoroutine(FlashPlayerZap());
    }

    /// <summary>Thunder: the dog crackles between white and blue, then fades back to its own colours.</summary>
    private IEnumerator FlashPlayerZap()
    {
        Renderer[] renderers = playerHealth.GetComponentsInChildren<Renderer>();
        for (float t = 0f; t < zapFlashSeconds; t += Time.unscaledDeltaTime)
        {
            float fade = t / zapFlashSeconds;
            Color zap = Mathf.FloorToInt(t / zapFlickerSeconds) % 2 == 0 ? Color.white : zapBlue;
            playerBlock.SetColor(BaseColorId, Color.Lerp(zap, Color.white, fade * fade));
            foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(playerBlock);
            yield return null;
        }
        foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(null);
        playerFlash = null;
    }

    // A new day loads the run's hearts (below the scene's full default): that is not a hit, so no shake.
    private void HandleHealthRestored() { lastHp = playerHealth.Hp; }

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
        // Hand the renderers back to the blush tint (BossReactions); with no tint this clears the block as before.
        if (reactions != null) reactions.Reapply();
        else foreach (Renderer r in bossRenderers) if (r != null) r.SetPropertyBlock(null);
    }

    private void SetBossColor(Color c)
    {
        block.SetColor(BaseColorId, c);
        foreach (Renderer r in bossRenderers) if (r != null) r.SetPropertyBlock(block);
    }
}
