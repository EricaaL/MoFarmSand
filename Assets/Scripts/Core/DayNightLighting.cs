using DistantLands.Cozy;
using UnityEngine;

public class DayNightLighting : MonoBehaviour
{
    [SerializeField] private Light sun;
    [SerializeField] private UnityEngine.Rendering.Volume nightVolume;
    [SerializeField] private float transitionSeconds = 3f;
    [SerializeField] private float dayTime = 0.45f;
    [SerializeField] private float nightTime = 0.73f;   // just after COZY sunset: spooky but readable on camera

    [Header("Fallback (no COZY in scene)")]
    [SerializeField] private Color dayColor = new Color(1f, 0.96f, 0.85f);
    [SerializeField] private Color nightColor = new Color(0.35f, 0.3f, 0.65f);
    [SerializeField] private Color dayAmbient = new Color(0.7f, 0.75f, 0.85f);
    [SerializeField] private Color nightAmbient = new Color(0.12f, 0.1f, 0.25f);
    [SerializeField] private float dayIntensity = 1.1f;
    [SerializeField] private float nightIntensity = 0.35f;
    [SerializeField] private Vector3 dayRotation = new Vector3(55f, -30f, 0f);
    [SerializeField] private Vector3 nightRotation = new Vector3(25f, -30f, 0f);

    [Header("Day clock (used when a DayClock drives the sun)")]
    [SerializeField] private float morningTime = 0.30f;
    [SerializeField] private float duskTime = 0.66f;

    [Header("Farmer glow (a small warm light that fades in with the night)")]
    [SerializeField] private Color glowColor = new Color(1f, 0.72f, 0.4f);
    [SerializeField] private float glowIntensity = 2.5f;
    [SerializeField] private float glowRange = 5f;
    [SerializeField] private Vector3 glowOffset = new Vector3(0f, 1.6f, 0f);

    private float blend;
    private float target;
    private float dayProgress = -1f;
    private Light glow;

    /// <summary>Daytime COZY clock value: fixed unless a DayClock is moving the sun from morning to dusk.</summary>
    private float DayTime => dayProgress < 0f ? dayTime : Mathf.Lerp(morningTime, duskTime, dayProgress);

    /// <summary>0 = morning, 1 = dusk. Called every frame by DayClock so the sun crosses the sky as time runs out.</summary>
    public void SetDayProgress(float progress01) { dayProgress = Mathf.Clamp01(progress01); }

    private void Start()
    {
        BuildGlow();
        Apply(0f);
    }

    private void BuildGlow()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        GameObject go = new GameObject("NightGlow");
        go.transform.SetParent(player.transform, false);
        go.transform.localPosition = glowOffset;
        glow = go.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = glowColor;
        glow.range = glowRange;
        glow.shadows = LightShadows.None;
        glow.intensity = 0f;
        glow.enabled = false;
    }

    public void TransitionToNight() { target = 1f; }

    /// <summary>Jump straight to night with no fade (days that skip the daytime).</summary>
    public void SnapToNight()
    {
        target = 1f;
        blend = 1f;
        Apply(1f);
    }
    public void TransitionToDay() { target = 0f; }

    private void Update()
    {
        // COZY advances its own clock, so re-pin the time every frame to hold day/night.
        bool usingCozy = CozyWeather.instance != null && CozyWeather.instance.timeModule != null;
        if (Mathf.Approximately(blend, target) && !usingCozy) return;
        blend = Mathf.MoveTowards(blend, target, Time.deltaTime / Mathf.Max(0.01f, transitionSeconds));
        Apply(blend);
    }

    private void Apply(float t)
    {
        if (nightVolume != null) nightVolume.weight = t;
        if (glow != null)
        {
            glow.intensity = glowIntensity * t;
            glow.enabled = t > 0.01f;
        }

        CozyWeather cozy = CozyWeather.instance;
        if (cozy != null && cozy.timeModule != null)
        {
            cozy.timeModule.currentTime = Mathf.Lerp(DayTime, nightTime, t);
            return;
        }

        RenderSettings.ambientLight = Color.Lerp(dayAmbient, nightAmbient, t);
        if (sun == null) return;
        sun.color = Color.Lerp(dayColor, nightColor, t);
        sun.intensity = Mathf.Lerp(dayIntensity, nightIntensity, t);
        sun.transform.rotation = Quaternion.Euler(Vector3.Lerp(dayRotation, nightRotation, t));
    }
}
