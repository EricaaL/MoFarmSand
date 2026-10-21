using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Sticky slime left by the caterpillar's spit. Slows the player while they stand in it.</summary>
public class SlimePuddle : MonoBehaviour
{
    private const string SlowSource = "boss:slime";

    // Puddles currently holding the player. One shared modifier, so overlapping puddles don't stack.
    private static int holders;
    private static PlayerController slowed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { holders = 0; slowed = null; }

    private float radius;
    private float lifetime;
    private float speedMult;
    private float age;
    private bool holding;
    private Transform player;
    private PlayerController controller;
    private Transform disc;

    [System.Serializable]
    public struct Settings
    {
        public float radius;
        public float seconds;
        [Range(0.1f, 1f)] public float speedMult;
        public Color color;
    }

    public static SlimePuddle Spawn(Vector3 position, Settings s)
    {
        GameObject go = new GameObject("SlimePuddle");
        go.transform.position = new Vector3(position.x, 0.03f, position.z);
        SlimePuddle puddle = go.AddComponent<SlimePuddle>();
        puddle.radius = s.radius;
        puddle.lifetime = s.seconds;
        puddle.speedMult = s.speedMult;
        puddle.Build(s.color);
        return puddle;
    }

    private void Build(Color color)
    {
        GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        d.name = "Disc";
        Destroy(d.GetComponent<Collider>());
        disc = d.transform;
        disc.SetParent(transform, false);
        disc.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);

        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetFloat("_Surface", 1f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.9f);
        d.GetComponent<Renderer>().sharedMaterial = m;
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            controller = p.GetComponent<PlayerController>();
        }
        if (GameManager.Instance != null) GameManager.Instance.OnPhaseChanged += HandlePhase;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnPhaseChanged -= HandlePhase;
        Release();
    }

    private void HandlePhase(GamePhase phase) { if (phase != GamePhase.Night) Destroy(gameObject); }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime) { Destroy(gameObject); return; }

        // Pop in quickly, shrink away over the last second.
        float grow = Mathf.Clamp01(age / 0.25f);
        float fade = Mathf.Clamp01((lifetime - age) / 1f);
        float s = Mathf.SmoothStep(0f, 1f, Mathf.Min(grow, fade));
        disc.localScale = new Vector3(radius * 2f * s, 0.02f, radius * 2f * s);

        if (player == null || controller == null) return;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        bool inside = d.magnitude <= radius * s;
        if (inside && !holding) Hold();
        else if (!inside && holding) Release();
    }

    private void Hold()
    {
        holding = true;
        holders++;
        slowed = controller;
        controller.SpeedModifiers.Set(SlowSource, speedMult);
    }

    private void Release()
    {
        if (!holding) return;
        holding = false;
        holders = Mathf.Max(0, holders - 1);
        if (holders == 0 && slowed != null)
        {
            slowed.SpeedModifiers.Remove(SlowSource);
            slowed = null;
        }
    }
}
