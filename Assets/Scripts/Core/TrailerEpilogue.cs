using System;
using System.Collections;
using DistantLands.Cozy;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Capture-only ending for the trailer. The dog lies on the last pose of the death clip while the seasons race by as a
/// timelapse (each one from morning to night), then it is summer again, the dog gets up and looks at the sky as the camera
/// tilts up, and the DOGSEND title fades in at the top middle. Timings are Inspector data. Run it with E or from
/// <see cref="TrailerDirector"/>, which plays it after the last shot.
/// </summary>
public class TrailerEpilogue : MonoBehaviour
{
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int GettingUpHash = Animator.StringToHash("GettingUp");

    private static readonly Type[] ControlTypes =
    {
        typeof(PlayerController), typeof(PlayerCombat), typeof(PlayerInteractor),
        typeof(PlayerDance), typeof(PlayerPlotPlacer), typeof(PlayerAnimator),
    };

    [Header("Scene")]
    [Tooltip("Where the dog lies down: in front of the farm tiles. Its rotation is the way the dog faces.")]
    [SerializeField] private Transform lieSpot;
    [SerializeField] private CinemachineCamera epilogueCamera;
    [SerializeField] private CozyBridge cozyBridge;
    [SerializeField] private DayNightLighting lighting;
    [SerializeField] private GameDatabase db;
    [SerializeField] private int liveCameraPriority = 25;

    [Header("Lying down")]
    [Tooltip("Frame of the death clip the dog is held on (the clip runs frames 0-87).")]
    [SerializeField] private int deathFrame = 86;
    [SerializeField] private float holdBeforeTimelapseSeconds = 1.5f;

    [Header("Timelapse")]
    [Tooltip("Seasons in the order they flash past; each runs from morning to night.")]
    [SerializeField] private SeasonData[] timelapse;
    [SerializeField] private float secondsPerSeason = 2.2f;
    [Tooltip("The stretch from one season's night into the next season's morning: the clock runs on and the look fades over it.")]
    [SerializeField] private float dawnSeconds = 1.6f;

    [Header("Waking up")]
    [Tooltip("The season it loops back to; the dog gets up as its morning rolls on to midday.")]
    [SerializeField] private SeasonData wakeSeason;
    [Tooltip("How far into the day (0 morning, 1 night) the last sweep stops: bright and sunny for the sky shot.")]
    [SerializeField, Range(0f, 1f)] private float wakeDay01 = 0.35f;
    [SerializeField] private float wakeSweepSeconds = 4f;
    [Tooltip("Blend from the lying death pose into the getting-up clip.")]
    [SerializeField] private float getUpFadeSeconds = 0.5f;
    [SerializeField] private float lookUpSeconds = 2.5f;
    [Tooltip("The dog starts looking up this many frames (of the Getting Up clip) before it would have: the get-up and the sweep to midday are not waited out.")]
    [SerializeField] private int lookUpLeadFrames = 10;

    [Header("Camera")]
    [SerializeField] private float cameraPitchDegrees = -31f;
    [SerializeField] private float cameraTiltSeconds = 4f;

    [Header("Title")]
    [SerializeField] private string titleText = "DOGSEND";
    [SerializeField] private float titleDelaySeconds = 1.2f;
    [SerializeField] private float titleFadeSeconds = 3f;
    [SerializeField] private float titleSize = 250f;
    [Tooltip("Where the title sits from the top, as a fraction of the screen: 0.333 is the upper rule-of-thirds line. It stays centred across.")]
    [SerializeField, Range(0f, 1f)] private float titleCenterY01 = 1f / 3f;

    [Header("Birds (fly past as the title comes in)")]
    [Tooltip("COZY's 'Bird V Large' flock: a V of birds that spawns 120 m back and 50 m up and flies along world +z at 15 m/s (its velocity is world-space, so rotating it changes nothing). Empty = no birds.")]
    [SerializeField] private GameObject birdFlockPrefab;
    [Tooltip("How high above the camera the flock flies.")]
    [SerializeField] private float birdHeight = 30f;
    [Tooltip("How far ahead of the camera (along world z) the flock's spawn point sits: the larger it is, the EARLIER the V reaches the sky shot (10 = about 9.5 s after release, 115 = about 2.5 s, 150 = already in the frame).")]
    [SerializeField] private float birdSpawnAhead = 150f;
    [Tooltip("Sideways shift of the flock from the middle of the view, in metres: negative is toward the left of the screen, positive toward the right. About 0.015 of the screen width per metre at the distance the V flies.")]
    [SerializeField] private float birdSidewaysMeters = -14f;
    [Tooltip("Seconds after the dog starts looking up (when the camera starts tilting) that the flock is released. 0 = straight away, before the title starts to fade in.")]
    [SerializeField] private float birdDelaySeconds;

    // The flock prefab's own geometry: its spawn shapes sit this high, and the V is centred about this far sideways.
    private const float FlockSpawnHeight = 40f;
    private const float FlockCenterSideways = 2f;

    private Coroutine running;
    private float titleAlpha;

    public bool IsPlaying => running != null;

    /// <summary>True from the first Play on, so the trigger button stays gone once the ending has run.</summary>
    public bool HasPlayed { get; private set; }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && !IsPlaying) Play();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        running = null;
    }

    [ContextMenu("Play Epilogue")]
    public void Play()
    {
        if (running != null) return;
        HasPlayed = true;
        running = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        titleAlpha = 0f;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Animator animator = player != null ? player.GetComponentInChildren<Animator>() : null;
        if (animator == null || db == null)
        {
            Debug.LogWarning("[TrailerEpilogue] Needs a Player with an Animator and a GameDatabase.");
            running = null;
            yield break;
        }

        LockPlayer(player, animator);
        PlaceDog(player);
        float deathTime = DeathNormalizedTime(animator);
        float wakeSeconds = ClipSeconds(animator, "GettingUp", 3f);
        Vector3 baseCameraEuler = epilogueCamera != null ? epilogueCamera.transform.eulerAngles : Vector3.zero;

        // The dog lies down in front of the tiles, held on the last frames of the death clip, before the camera cuts to it.
        animator.speed = 1f;
        animator.Play(DeathHash, 0, deathTime);
        animator.Update(0f);
        animator.speed = 0f;

        if (epilogueCamera != null)
        {
            // Jump straight to the cutscene camera: the Brain's default blend is held at a cut for the switch.
            CinemachineBrain brain = CinemachineBrain.GetActiveBrain(0);
            CinemachineBlendDefinition savedBlend = brain != null ? brain.DefaultBlend : default;
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            epilogueCamera.Priority.Enabled = true;
            epilogueCamera.Priority.Value = liveCameraPriority;
            yield return null;
            yield return null;
            if (brain != null) brain.DefaultBlend = savedBlend;
        }

        if (timelapse != null && timelapse.Length > 0) ApplySeason(timelapse[0]);
        yield return Wait(holdBeforeTimelapseSeconds);

        for (int i = 0; i < timelapse.Length; i++)
        {
            yield return Sweep(0f, 1f, secondsPerSeason);
            // Night runs on into the next season's dawn (the last one is the wake-up summer).
            yield return DawnInto(i + 1 < timelapse.Length ? timelapse[i + 1] : wakeSeason);
        }

        // Back to summer: the morning rolls on while the dog gets up.
        StartCoroutine(Sweep(0f, wakeDay01, wakeSweepSeconds));
        animator.speed = 1f;
        animator.CrossFadeInFixedTime(GettingUpHash, getUpFadeSeconds, 0);
        // The dog starts looking up a few frames before it is fully on its feet (and before the sweep to midday ends).
        float lookLead = lookUpLeadFrames / ClipFrameRate(animator, "GettingUp", 30f);
        yield return Wait(Mathf.Max(0f, Mathf.Max(wakeSeconds, wakeSweepSeconds) - lookLead));

        // Look up at the sky while the camera tilts after it and the title comes in.
        SkyLookIK look = animator.GetComponent<SkyLookIK>();
        if (look == null) look = animator.gameObject.AddComponent<SkyLookIK>();
        Coroutine tilt = StartCoroutine(TiltCamera(baseCameraEuler));
        Coroutine title = StartCoroutine(FadeTitle());
        StartCoroutine(SpawnBirdsAfter(birdDelaySeconds, baseCameraEuler.y));
        for (float t = 0f; t < lookUpSeconds; t += Time.deltaTime)
        {
            look.weight = Mathf.SmoothStep(0f, 1f, t / lookUpSeconds);
            yield return null;
        }
        look.weight = 1f;
        yield return tilt;
        yield return title;
        running = null;
    }

    /// <summary>The HUD (hearts, hotbar, fullness bar) and the in-play tips stay off for the whole ending.</summary>
    private static void HideUi()
    {
        foreach (DebugHUD hud in FindObjectsByType<DebugHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None)) hud.enabled = false;
        foreach (TipsView tips in FindObjectsByType<TipsView>(FindObjectsInactive.Include, FindObjectsSortMode.None)) tips.enabled = false;
    }

    private static void LockPlayer(GameObject player, Animator animator)
    {
        HideUi();
        foreach (Type type in ControlTypes)
        {
            Behaviour b = player.GetComponent(type) as Behaviour;
            if (b != null) b.enabled = false;
        }
        int throwLayer = animator.GetLayerIndex("Throw");
        if (throwLayer >= 0) animator.SetLayerWeight(throwLayer, 0f);
        animator.SetFloat("Speed", 0f);
    }

    /// <summary>The death clip is held on its lying frame: frame / total frames of the clip.</summary>
    private float DeathNormalizedTime(Animator animator)
    {
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip != null && clip.name == "Death")
                return Mathf.Clamp01(deathFrame / Mathf.Max(1f, clip.length * clip.frameRate));
        return Mathf.Clamp01(deathFrame / 87f);
    }

    private static float ClipFrameRate(Animator animator, string clipName, float fallback)
    {
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip != null && clip.name == clipName) return Mathf.Max(1f, clip.frameRate);
        return fallback;
    }

    private static float ClipSeconds(Animator animator, string clipName, float fallback)
    {
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip != null && clip.name == clipName) return clip.length;
        return fallback;
    }

    /// <summary>
    /// Night flows into the next season's morning: the season's look and weather change while it is dark, fading over the
    /// same stretch the clock runs on through the small hours to dawn, so there is no jump.
    /// </summary>
    private IEnumerator DawnInto(SeasonData season)
    {
        ApplySeason(season, dawnSeconds, false);
        for (float t = 0f; t < dawnSeconds; t += Time.deltaTime)
        {
            if (lighting != null) lighting.ScrubDawn(t / dawnSeconds);
            yield return null;
        }
        if (lighting != null) lighting.ScrubDawn(1f);
    }

    /// <summary>The dog lies down at the spot in front of the tiles, facing the way the spot faces.</summary>
    private void PlaceDog(GameObject player)
    {
        if (lieSpot == null) return;
        CharacterController body = player.GetComponent<CharacterController>();
        if (body != null) body.enabled = false;   // a controller would fight the teleport
        player.transform.SetPositionAndRotation(lieSpot.position, lieSpot.rotation);
        if (body != null) body.enabled = true;
    }

    /// <param name="resetLight">True to put the sun back at morning (the very first season); false when the clock is already running on.</param>
    private void ApplySeason(SeasonData season, float transitionSeconds = 0f, bool resetLight = true)
    {
        if (resetLight && lighting != null) lighting.Scrub(0f);
        if (cozyBridge == null || season == null) return;
        cozyBridge.ApplyDate(DateInMiddleOf(season), WeatherKind.Clear, null, db.cozyWeather, transitionSeconds);
    }

    private CalendarDate DateInMiddleOf(SeasonData season)
    {
        int day = 0;
        foreach (SeasonData s in db.calendar.seasons)
        {
            if (s == null) continue;
            if (s == season) { day += Mathf.Max(1, s.lengthInDays) / 2; break; }
            day += Mathf.Max(1, s.lengthInDays);
        }
        return db.Calendar.DateOf(day);
    }

    /// <summary>Moves the sun through the day between two points (0 morning, 1 night).</summary>
    private IEnumerator Sweep(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (lighting != null) lighting.Scrub(Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        if (lighting != null) lighting.Scrub(to);
    }

    private IEnumerator TiltCamera(Vector3 baseEuler)
    {
        if (epilogueCamera == null) yield break;
        Transform cam = epilogueCamera.transform;
        Quaternion from = cam.rotation;
        Quaternion to = Quaternion.Euler(cameraPitchDegrees, baseEuler.y, 0f);
        for (float t = 0f; t < cameraTiltSeconds; t += Time.deltaTime)
        {
            cam.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / cameraTiltSeconds));
            yield return null;
        }
        cam.rotation = to;
    }

    /// <summary>
    /// A V of birds flying over the sky shot. The flock always flies along world +z (its velocity is world-space), so it is
    /// placed from the camera's final pose: high up and centred on where the camera looks, entering the top of the frame
    /// a moment after the title starts and flying away into the distance over the barn.
    /// </summary>
    private void SpawnBirds(float cameraYaw)
    {
        if (birdFlockPrefab == null || epilogueCamera == null) return;
        Vector3 cam = epilogueCamera.transform.position;
        const float sightDistance = 60f;   // where along the flight the V should line up with the view centre
        float centreX = cam.x + Mathf.Tan(Mathf.Deg2Rad * Mathf.DeltaAngle(0f, cameraYaw)) * sightDistance + birdSidewaysMeters;
        Vector3 origin = new Vector3(centreX - FlockCenterSideways, cam.y + birdHeight - FlockSpawnHeight, cam.z + birdSpawnAhead);
        GameObject flock = Instantiate(birdFlockPrefab, origin, Quaternion.identity);
        // COZY's CozyParticles zeroes every emission rate when it wakes and expects the weather system to turn it back on
        // with Play(weight); the flock stays silent until that is called.
        CozyParticles cozyParticles = flock.GetComponent<CozyParticles>();
        if (cozyParticles != null) cozyParticles.Play(1f);
        ParticleSystem system = flock.GetComponent<ParticleSystem>();
        if (system != null && !system.isPlaying) system.Play(true);
        Destroy(flock, 25f);
    }

    private IEnumerator SpawnBirdsAfter(float seconds, float cameraYaw)
    {
        yield return Wait(seconds);
        SpawnBirds(cameraYaw);
    }

    private IEnumerator FadeTitle()
    {
        yield return Wait(titleDelaySeconds);
        for (float t = 0f; t < titleFadeSeconds; t += Time.deltaTime)
        {
            titleAlpha = Mathf.SmoothStep(0f, 1f, t / titleFadeSeconds);
            yield return null;
        }
        titleAlpha = 1f;
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
    }

    private void OnGUI()
    {
        if (titleAlpha <= 0.001f) return;
        GUI.depth = -40;   // in front of the run HUD and the death screen

        bool pageMode = ImGuiKit.PageMode;
        ImGuiKit.PageMode = true;
        float s = ImGuiKit.Scale;
        Vector2 offset = ImGuiKit.PageOffset;

        Color c = ImGuiKit.Text; c.a = titleAlpha;
        float w = 1400f * s;
        float h = titleSize * 1.1f * s;
        float centerY = offset.y + ImGuiKit.CanvasHeight * s * titleCenterY01;
        ImGuiKit.Heading(new Rect((Screen.width - w) * 0.5f, centerY - h * 0.5f, w, h), titleText, titleSize, c, TextAnchor.MiddleCenter);
        ImGuiKit.PageMode = pageMode;
    }
}
