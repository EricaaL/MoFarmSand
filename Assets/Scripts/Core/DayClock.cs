using UnityEngine;

/// <summary>
/// Limits time per session. Daylight counts down while you farm and the sun crosses the sky; when it runs out
/// night falls on its own. At night a second timer runs: if dawn breaks before the boss is fed, the run is lost.
/// It only runs while armed by RunDirector, so the original trailer loop (GoldenSlice) keeps unlimited time.
/// </summary>
public class DayClock : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private DayNightLighting lighting;

    private readonly DayTimer timer = new DayTimer();
    private float nightSeconds;
    private bool armed;

    /// <summary>True while a clock is counting (or waiting out a screen).</summary>
    public bool Active => armed;
    public bool IsNight => gameManager != null && gameManager.Phase == GamePhase.Night;
    public bool Unlimited => timer.Unlimited;
    public float Remaining => timer.Remaining;
    public float Duration => timer.Duration;

    /// <summary>Starts a day. A length of 0 means that phase is unlimited.</summary>
    public void Begin(float daySeconds, float nightSeconds)
    {
        this.nightSeconds = nightSeconds;
        timer.Start(daySeconds);
        armed = true;
        if (lighting != null && !timer.Unlimited) lighting.SetDayProgress(0f);
    }

    public void Stop() { armed = false; }

    private void OnEnable()
    {
        if (gameManager != null) gameManager.OnPhaseChanged += HandlePhase;
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.OnPhaseChanged -= HandlePhase;
    }

    private void HandlePhase(GamePhase phase)
    {
        if (!armed) return;
        if (phase == GamePhase.Night) timer.Start(nightSeconds);
        else if (phase != GamePhase.Day) armed = false;
    }

    private void Update()
    {
        // Shopping, events and the map don't burn daylight.
        if (!armed || gameManager == null || RunDirector.ModalOpen || timer.Unlimited) return;

        GamePhase phase = gameManager.Phase;
        if (phase == GamePhase.Day)
        {
            bool timeUp = timer.Tick(Time.deltaTime);
            if (lighting != null) lighting.SetDayProgress(timer.Progress01);
            if (timeUp) gameManager.StartNight();
        }
        else if (phase == GamePhase.Night)
        {
            if (timer.Tick(Time.deltaTime)) gameManager.NightTimeUp();
        }
    }
}
