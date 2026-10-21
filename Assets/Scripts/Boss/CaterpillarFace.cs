using UnityEngine;

/// <summary>Presentation only: the paper mouth chomps when the boss is fed or bites, and gapes while it winds up a spit.</summary>
public class CaterpillarFace : MonoBehaviour
{
    [SerializeField] private Transform mouth;
    [SerializeField] private float chompSeconds = 0.5f;
    [SerializeField] private float chompSpeed = 22f;
    [Range(0.05f, 1f)] [SerializeField] private float closedScale = 0.45f;
    [SerializeField] private float gapeScale = 1.3f;

    private CaterpillarBoss boss;
    private BossHunger hunger;
    private Vector3 baseScale;
    private float chompUntil;

    private void Awake()
    {
        boss = GetComponentInParent<CaterpillarBoss>();
        hunger = GetComponentInParent<BossHunger>();
        if (mouth != null) baseScale = mouth.localScale;
    }

    private void OnEnable() { if (hunger != null) hunger.OnBossFed += HandleFed; }
    private void OnDisable() { if (hunger != null) hunger.OnBossFed -= HandleFed; }

    private void HandleFed(float current, float max) { chompUntil = Time.time + chompSeconds; }

    private void LateUpdate()
    {
        if (mouth == null) return;
        float open;
        if (boss != null && boss.IsWinding) open = gapeScale;
        else if (Time.time < chompUntil || (boss != null && boss.IsChewing))
            open = Mathf.Lerp(closedScale, 1f, Mathf.Abs(Mathf.Sin(Time.time * chompSpeed)));
        else
            open = Mathf.Lerp(closedScale, 1f, 0.35f + 0.1f * Mathf.Sin(Time.time * 2f)); // idle grin
        mouth.localScale = new Vector3(baseScale.x, baseScale.y * open, baseScale.z);
    }
}
