using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// New values are appended so existing ordinals never change.
public enum GamePhase { Day, Night, Won, Lost, DayEnd }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private CaterpillarBoss boss;
    [SerializeField] private BossHunger bossHunger;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private DayNightLighting lighting;
    [SerializeField] private float resetDelay = 2.5f;
    [Tooltip("Falling below this height counts as falling into the void.")]
    [SerializeField] private float voidY = -10f;

    public GamePhase Phase { get; private set; } = GamePhase.Day;
    public bool CanFarm => Phase == GamePhase.Day;
    /// <summary>False on days with no night fight (farm, merchant, rest...). The bell then just ends the day.</summary>
    public bool HasEncounter { get; private set; } = true;
    /// <summary>Set by RunDirector. When null the legacy reload-on-end behavior is used.</summary>
    public IDayOutcomeHandler OutcomeHandler { get; set; }

    public event Action<GamePhase> OnPhaseChanged;
    /// <summary>The player fell off the map. True when the day ends because of it (false = respawned and the fight goes on).</summary>
    public event Action<bool> OnVoidFall;

    private Vector3 respawnPoint;
    private bool respawnKnown;

    /// <summary>True on season-boss nights: losing costs next season's crop speed, not the year.</summary>
    public bool SurvivableLoss { get; private set; }

    public void Configure(DayRules rules) { HasEncounter = rules.hasEncounter; SurvivableLoss = rules.survivableLoss; }

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnEnable()
    {
        if (bossHunger != null) bossHunger.OnBossSatisfied += HandleWin;
        if (playerHealth != null) playerHealth.OnDied += HandleLose;
    }

    private void OnDisable()
    {
        if (bossHunger != null) bossHunger.OnBossSatisfied -= HandleWin;
        if (playerHealth != null) playerHealth.OnDied -= HandleLose;
    }

    private void Start()
    {
        if (playerHealth == null) return;
        respawnPoint = playerHealth.transform.position;
        respawnKnown = true;
    }

    /// <param name="instant">Skip the dusk fade. Used on days that start at night.</param>
    public void StartNight(bool instant = false)
    {
        if (Phase != GamePhase.Day) return;
        if (!HasEncounter)
        {
            SetPhase(GamePhase.DayEnd);
            if (lighting != null) lighting.TransitionToNight();
            Resolve();
            return;
        }
        SetPhase(GamePhase.Night);
        if (lighting != null) { if (instant) lighting.SnapToNight(); else lighting.TransitionToNight(); }
        // Safety net: if the boss was somehow already satisfied, finish the day instead of waiting forever.
        if (bossHunger != null && bossHunger.IsSatisfied) { HandleWin(); return; }
        if (boss != null) boss.Wake();
    }

    /// <summary>The night clock ran out before the boss was fed: dawn breaks and the run is lost.</summary>
    public void NightTimeUp()
    {
        if (Phase == GamePhase.Night) HandleLose();
    }

    private void Update()
    {
        // Running out of crops is no longer an instant loss: you keep dodging until dawn or until you are caught.
        if (playerHealth != null && playerHealth.transform.position.y < voidY
            && (Phase == GamePhase.Day || Phase == GamePhase.Night)) HandleVoidFall();
    }

    /// <summary>
    /// Falling off the map costs a heart. On a boss night you are put back on the farm and the fight goes on (a mandatory
    /// boss can't be skipped); on any other day the day simply ends and the run moves on.
    /// </summary>
    private void HandleVoidFall()
    {
        bool bossNight = Phase == GamePhase.Night && HasEncounter;
        bool endsDay = !bossNight && OutcomeHandler != null;

        // Move first so a fall can't trigger twice while the hit is processed.
        if (!endsDay) Respawn();
        playerHealth.LoseHeart();
        if (Phase == GamePhase.Lost) return; // that was the last heart: HandleLose already ran

        OnVoidFall?.Invoke(endsDay);
        if (!endsDay) return;

        SetPhase(GamePhase.DayEnd);
        if (boss != null) boss.Sleep();
        Resolve();
    }

    private void Respawn()
    {
        Transform player = playerHealth.transform;
        Vector3 target = respawnKnown ? respawnPoint : new Vector3(0f, 1f, 0f);
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.position = target;
        if (controller != null) controller.enabled = true;
    }

    private void HandleWin()
    {
        if (Phase != GamePhase.Night) return;
        SetPhase(GamePhase.Won);
        Resolve();
    }

    private void HandleLose()
    {
        // A run can also be lost by day (e.g. a storm), but the legacy loop only loses at night.
        bool lossAllowed = Phase == GamePhase.Night || (Phase == GamePhase.Day && OutcomeHandler != null);
        if (!lossAllowed) return;
        if (bossHunger != null && bossHunger.IsSatisfied) return;
        SetPhase(GamePhase.Lost);
        if (boss != null) boss.Sleep();
        Resolve();
    }

    /// <summary>End of day: hand the outcome to the run director, or reload like the original loop.</summary>
    private void Resolve()
    {
        if (OutcomeHandler != null) OutcomeHandler.OnDayResolved(Phase);
        else Invoke(nameof(ReloadScene), resetDelay);
    }

    private void SetPhase(GamePhase next)
    {
        Phase = next;
        OnPhaseChanged?.Invoke(next);
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
