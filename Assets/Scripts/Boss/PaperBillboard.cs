using UnityEngine;

/// <summary>
/// Presentation only: keeps a flat paper card facing the gameplay camera, flips it like a paper cutout when the boss
/// changes horizontal heading, and adds a phase-offset bob + tilt so a chain of cards ripples like a crawl.
/// Put it on a pivot (no mesh); the artwork quad is a child, so the flip mirrors around the pivot.
/// </summary>
public class PaperBillboard : MonoBehaviour
{
    [Tooltip("Boss root whose heading drives the left/right flip. Found in the parents when empty.")]
    [SerializeField] private Transform heading;
    [SerializeField] private bool flip = true;
    [SerializeField] private float flipSpeed = 10f;
    [Tooltip("Ignore heading changes while the boss points almost straight at/away from the camera, so cards don't flicker.")]
    [SerializeField] private float flipDeadZone = 0.25f;

    [Header("Crawl wobble")]
    [SerializeField] private float bobHeight = 0.06f;
    [SerializeField] private float bobSpeed = 7f;
    [SerializeField] private float tiltDegrees = 5f;
    [Tooltip("Offsets the wobble per card so neighbours move out of sync (a wave down the body).")]
    [SerializeField] private float phase;
    [Tooltip("Wobble multiplier while the caterpillar chews or rears up (needs a CaterpillarBoss in the parents).")]
    [SerializeField] private float busyMultiplier = 2f;

    private Camera cam;
    private CaterpillarBoss boss;
    private BossHunger hunger;
    private float flipSign = 1f;
    private float flipValue = 1f;
    private float baseScaleX;
    private float baseY;

    private void Awake()
    {
        if (heading == null)
        {
            boss = GetComponentInParent<CaterpillarBoss>();
            heading = boss != null ? boss.transform : transform.root;
        }
        else boss = heading.GetComponent<CaterpillarBoss>();
        hunger = GetComponentInParent<BossHunger>();
        baseScaleX = transform.localScale.x;
        baseY = transform.localPosition.y;
    }

    private void Start() { cam = Camera.main; }

    private void LateUpdate()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        bool awake = boss != null && boss.IsAwake;
        bool busy = boss != null && (boss.IsChewing || boss.IsWinding);
        bool scared = hunger != null && hunger.IsScared;
        float amount = awake ? (busy ? busyMultiplier : 1f) * (scared ? 1.6f : 1f) : 0.25f;
        float t = Time.time * bobSpeed + phase;

        transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t) * tiltDegrees * amount);
        Vector3 lp = transform.localPosition;
        lp.y = baseY + Mathf.Abs(Mathf.Sin(t)) * bobHeight * amount;
        transform.localPosition = lp;

        if (!flip) return;
        float side = Vector3.Dot(heading.forward, cam.transform.right);
        if (Mathf.Abs(side) > flipDeadZone) flipSign = Mathf.Sign(side);
        flipValue = Mathf.MoveTowards(flipValue, flipSign, flipSpeed * Time.deltaTime);
        Vector3 s = transform.localScale;
        s.x = baseScaleX * flipValue;
        transform.localScale = s;
    }
}
