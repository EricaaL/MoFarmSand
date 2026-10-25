using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInventory), typeof(PlayerHealth))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private TrajectoryRenderer trajectory;
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private float maxRange = 14f;
    [SerializeField] private float minFlightTime = 0.5f;
    [SerializeField] private float flightTimePerMeter = 0.06f;
    [SerializeField] private float throwCooldown = 0.35f;
    [Tooltip("How long the dog keeps facing the throw (roughly the throw animation), even while walking.")]
    [SerializeField] private float throwFaceSeconds = 0.55f;
    [Tooltip("How far from the player a dragged seed can be planted (metres).")]
    [SerializeField] private float dragReach = 4.5f;

    private PlayerInventory inventory;
    private PlayerInteractor interactor;
    private PlayerHealth health;
    private PlayerController mover;
    private InputAction attackAction;
    private Camera cam;
    private float nextThrowTime;
    private readonly ThrowAim throwAim = new ThrowAim();
    private readonly CropMagazine magazine = new CropMagazine();

    /// <summary>Raised when a crop leaves the player's hands (thrown or fired). Drives the throw/shoot sounds.</summary>
    public static event System.Action<CropData> Fired;
    /// <summary>Raised when the throw button is first held and the arc appears. Drives the bow-draw sound.</summary>
    public static event System.Action AimStarted;
    /// <summary>Raised when a support crop is eaten. Drives the eating sound.</summary>
    public static event System.Action Ate;
    private bool wasAiming;

    private bool canAct = true;
    private bool menuClickHeld; // the click that closed a menu is still down: it belongs to the menu, not the world
    private PlayerDeath death;

    /// <summary>Always false once the dog is dead, whatever the run director sets.</summary>
    public bool CanAct
    {
        // A menu screen (shop, map, bank...) is up: clicks belong to its buttons, so a press there must never start a throw.
        get => canAct && !RunDirector.ModalOpen && !menuClickHeld && !(death != null && death.IsDead);
        set => canAct = value;
    }
    /// <summary>True while the throw button is held and the arc is showing.</summary>
    public bool IsAiming => throwAim.IsAiming;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        interactor = GetComponent<PlayerInteractor>();
        health = GetComponent<PlayerHealth>();
        mover = GetComponent<PlayerController>();
        death = GetComponent<PlayerDeath>();
        if (inputActions == null) inputActions = InputSystem.actions;
        attackAction = inputActions != null ? inputActions.FindAction("Player/Attack") : null;
    }

    private void OnEnable() { attackAction?.Enable(); }

    private void OnDisable()
    {
        throwAim.Reset();
        trajectory?.Hide();
    }

    private void Start() { cam = Camera.main; }

    private void UpdateMenuClickLatch()
    {
        bool down = Mouse.current != null && Mouse.current.leftButton.isPressed;
        if (!down) menuClickHeld = false;
        else if (Time.frameCount - RunDirector.ModalClosedFrame <= 2) menuClickHeld = true;
    }

    private void Update()
    {
        if (cam == null) cam = Camera.main;
        UpdateMenuClickLatch();
        CropData crop = inventory.ActiveCrop;
        bool harvestClick = interactor != null && interactor.PointerHarvestable() != null; // that click harvests, not throws
        bool canAim = CanAct && !harvestClick && !HotbarView.PointerBusy && !PlayerDance.WheelOpen && crop != null && crop.type == CropType.Projectile && inventory.ActiveCount > 0;

        // Hold left click to aim (the arc shows), release to throw. Right click cancels the aim.
        ThrowAimResult aim = attackAction == null ? ThrowAimResult.None : throwAim.Step(new ThrowAimInput
        {
            canAim = canAim,
            pressedThisFrame = attackAction.WasPressedThisFrame(),
            held = attackAction.IsPressed(),
            releasedThisFrame = attackAction.WasReleasedThisFrame(),
            cancelPressed = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame,
            cooldownReady = Time.time >= nextThrowTime
        });

        bool aimingNow = throwAim.IsAiming;
        if (aimingNow && !wasAiming) AimStarted?.Invoke();
        wasAiming = aimingNow;

        if ((aim == ThrowAimResult.Aiming || aim == ThrowAimResult.Throw) && TryGetAim(out Vector3 p0, out Vector3 v0, out float flight))
        {
            if (aim == ThrowAimResult.Throw)
            {
                trajectory?.Hide();
                Throw(crop, p0, v0);
            }
            else
            {
                trajectory?.Show(p0, v0, Physics.gravity, flight);
                FaceCursor();
            }
        }
        else
        {
            if (aim == ThrowAimResult.Throw || aim == ThrowAimResult.Cancelled) throwAim.Reset();
            trajectory?.Hide();
        }

        if (CanAct && !HotbarView.PointerBusy && !PlayerDance.WheelOpen && crop != null && crop.type == CropType.Support
            && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryEat(crop);
        }

        // Shooters (peas, corn): hold left click to auto-fire toward the cursor, looking at it the whole time (even between shots).
        if (CanAct && !harvestClick && !HotbarView.PointerBusy && !PlayerDance.WheelOpen && crop != null && crop.type == CropType.Shooter
            && attackAction != null && attackAction.IsPressed())
        {
            FaceCursor();
            if (Time.time >= nextThrowTime && magazine.CanFire(crop, inventory.ActiveCount)
                && Mouse.current != null && TryGetGroundPoint(Mouse.current.position.ReadValue(), out Vector3 aimPoint))
            {
                Shoot(crop, aimPoint);
            }
        }
    }

    /// <summary>Makes the dog look at the cursor's spot on the ground; call every frame while aiming or holding to fire.</summary>
    private void FaceCursor()
    {
        if (mover != null && Mouse.current != null && TryGetGroundPoint(Mouse.current.position.ReadValue(), out Vector3 spot))
            mover.AimAt(spot);
    }

    /// <summary>Rounds loaded from the current crop of this type, for the hotbar counter.</summary>
    public int LoadedRounds(CropData crop) => magazine.Loaded(crop);

    private bool TryGetGroundPoint(Vector2 screenPoint, out Vector3 point)
    {
        point = Vector3.zero;
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;
        Ray ray = cam.ScreenPointToRay(screenPoint);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, 0.1f, 0f));
        if (!ground.Raycast(ray, out float enter)) return false;
        point = ray.GetPoint(enter);
        return true;
    }

    /// <summary>Fires one straight bullet. Loads a fresh crop into the magazine first when it is empty.</summary>
    private void Shoot(CropData crop, Vector3 aimPoint)
    {
        if (crop.projectilePrefab == null) return;
        float stackQuality = inventory.ActiveQuality; // read before the stack shrinks
        if (!magazine.TryFire(crop, () => inventory.TryConsume(crop, 1), stackQuality, out float quality)) return;

        nextThrowTime = Time.time + crop.fireInterval;
        Vector3 p0 = throwOrigin != null ? throwOrigin.position : transform.position + Vector3.up;
        Vector3 target = aimPoint; target.y = 0.6f; // chest height of a crawling boss, so flat shots connect
        Vector3 dir = target - p0;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        dir.Normalize();

        GameObject go = Instantiate(crop.projectilePrefab, p0, Quaternion.LookRotation(dir));
        CropProjectile proj = go.GetComponent<CropProjectile>();
        if (proj != null) proj.Init(crop, dir * crop.bulletSpeed, quality, useGravity: false);
        Fired?.Invoke(crop);

        Vector3 face = dir; face.y = 0f;
        if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
    }

    private bool TryGetAim(out Vector3 p0, out Vector3 v0, out float flight)
    {
        Vector2 pointer = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        return TryGetAim(pointer, out p0, out v0, out flight);
    }

    private bool TryGetAim(Vector2 screenPoint, out Vector3 p0, out Vector3 v0, out float flight)
    {
        p0 = throwOrigin != null ? throwOrigin.position : transform.position + Vector3.up;
        v0 = Vector3.zero;
        flight = minFlightTime;
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPoint);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, 0.1f, 0f));
        if (!ground.Raycast(ray, out float enter)) return false;

        Vector3 target = ray.GetPoint(enter);
        Vector3 flat = target - transform.position;
        flat.y = 0f;
        if (flat.magnitude > maxRange) target = transform.position + flat.normalized * maxRange;
        target.y = 0.1f;

        flight = minFlightTime + flat.magnitude * flightTimePerMeter;
        v0 = Ballistics.SolveLaunchVelocity(p0, target, flight, Physics.gravity);
        return true;
    }

    /// <summary>
    /// Uses the active hotbar item where it was dropped (screen pixels, origin bottom-left): plants a seed on the plot there,
    /// throws an attack crop at that point, or eats a defense crop. Returns what happened, for a short on-screen note.
    /// </summary>
    public bool TryUseAt(Vector2 screenPoint, out string message)
    {
        message = string.Empty;
        if (!CanAct) { message = "Can't do that right now"; return false; }

        if (inventory.ActiveItem is FarmlandPlotItem)
        {
            PlayerPlotPlacer placer = GetComponent<PlayerPlotPlacer>();
            if (placer == null) { message = "Can't place a plot here"; return false; }
            return placer.TryPlaceAt(screenPoint, out message);
        }

        SeedData seed = inventory.ActiveSeed;
        if (seed != null)
            return TryPlantAt(screenPoint, out message);

        CropData crop = inventory.ActiveCrop;
        if (crop == null || (inventory.ActiveCount <= 0 && magazine.Loaded(crop) <= 0)) { message = "Nothing to use"; return false; }

        if (crop.type == CropType.Shooter)
        {
            if (Time.time < nextThrowTime) { message = "Wait a moment"; return false; }
            if (!TryGetGroundPoint(screenPoint, out Vector3 spot) || crop.projectilePrefab == null) { message = "Can't shoot there"; return false; }
            Shoot(crop, spot);
            return true;
        }

        if (crop.type == CropType.Support)
        {
            if (TryEat(crop)) { message = "Bark Shield up!"; return true; }
            message = health.ShieldFull ? "Bark Shield is full" : "Couldn't eat that";
            return false;
        }

        if (Time.time < nextThrowTime) { message = "Wait a moment"; return false; }
        if (!TryGetAim(screenPoint, out Vector3 p0, out Vector3 v0, out _) || crop.projectilePrefab == null) { message = "Can't throw there"; return false; }
        Throw(crop, p0, v0);
        return true;
    }

    private bool TryPlantAt(Vector2 screenPoint, out string message)
    {
        message = "Drop a seed on a plot";
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPoint);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, 0.1f, 0f));
        if (!ground.Raycast(ray, out float enter)) return false;
        Vector3 hit = ray.GetPoint(enter);

        FarmTile best = null;
        float bestDist = 1.6f; // roughly half a plot plus some slack
        foreach (FarmTile tile in FindObjectsByType<FarmTile>(FindObjectsSortMode.None))
        {
            Vector3 d = tile.transform.position - hit; d.y = 0f;
            if (d.magnitude < bestDist) { bestDist = d.magnitude; best = tile; }
        }
        if (best == null) return false;

        Vector3 toTile = best.transform.position - transform.position; toTile.y = 0f;
        if (toTile.magnitude > dragReach) { message = "Too far away - move closer"; return false; }
        if (!best.CanInteract(gameObject)) { message = best.IsBlocked ? "That plot is flooded" : "Something is already growing there"; return false; }

        if (toTile.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toTile);
        best.Interact(gameObject);
        message = string.Empty;
        return true;
    }

    /// <summary>Eats a support crop. Out-of-season crops are weaker and grant a shorter Bark Shield.</summary>
    public bool TryEat(CropData crop)
    {
        if (health.ShieldFull) return false;
        float quality = inventory.ActiveQuality;
        if (!inventory.TryConsume(crop, 1)) return false;
        health.GrantShield(ClimateModel.ShieldCharges(crop, quality), ClimateModel.ShieldSeconds(crop, quality));
        Ate?.Invoke();
        return true;
    }

    private void Throw(CropData crop, Vector3 p0, Vector3 v0)
    {
        if (crop.projectilePrefab == null) return;
        float quality = inventory.ActiveQuality; // read before the stack shrinks
        if (!inventory.TryConsume(crop, 1)) return;
        nextThrowTime = Time.time + throwCooldown;
        GameObject go = Instantiate(crop.projectilePrefab, p0, Quaternion.identity);
        CropProjectile proj = go.GetComponent<CropProjectile>();
        if (proj != null) proj.Init(crop, v0, quality);
        Fired?.Invoke(crop);
        mover?.PlayThrowSquash();

        Vector3 face = v0; face.y = 0f;
        if (face.sqrMagnitude > 0.01f)
        {
            // Hold the facing for the swing so walking at the same time doesn't turn the throw sideways.
            if (mover != null) mover.FaceDirection(face, throwFaceSeconds);
            else transform.rotation = Quaternion.LookRotation(face);
        }
    }
}
