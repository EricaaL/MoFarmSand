using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the day's disaster. Adapts the scene (tiles, player, camera shake, FX) to <see cref="IFarmWorld"/>
/// so the effect ScriptableObjects stay free of scene references.
/// </summary>
public class DisasterDirector : MonoBehaviour, IFarmWorld
{
    [SerializeField] private FarmConditions conditions;
    [SerializeField] private DisasterRegistry registry;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameFeedback feedback;

    private readonly List<IFarmPlot> plots = new List<IFarmPlot>();
    private readonly List<DisasterEffectRuntime> runtimes = new List<DisasterEffectRuntime>();
    private DisasterData current;
    private bool active;
    private bool ended;

    /// <summary>Seam event for the HUD banner and audio.</summary>
    public event Action<DisasterData, float> OnDisasterStarted;

    public DisasterData Current => active ? current : null;
    public float Severity { get; private set; }

    // IFarmWorld
    public IReadOnlyList<IFarmPlot> Plots => plots;
    public ModifierStack Growth => conditions != null ? conditions.Growth : fallbackGrowth;
    public ModifierStack SellPrice { get; } = new ModifierStack();
    public ModifierStack SeedPrice { get; } = new ModifierStack();
    public Vector3 PlayerPosition => playerHealth != null ? playerHealth.transform.position : Vector3.zero;
    public SeededRng Rng { get; private set; } = new SeededRng(1);

    private readonly ModifierStack fallbackGrowth = new ModifierStack();

    public void Begin(DisasterRoll roll, int runSeed, int nodeId)
    {
        End();
        if (roll.IsNone || registry == null) return;
        current = registry.Get(roll.disasterId);
        if (current == null) return;

        Severity = roll.severity01;
        Rng = SeededRng.Derive(runSeed, nodeId, RngStreams.DisasterRuntime);

        plots.Clear();
        foreach (FarmTile t in FindObjectsByType<FarmTile>(FindObjectsSortMode.None)) plots.Add(t);

        runtimes.Clear();
        for (int i = 0; i < current.effects.Count; i++)
        {
            if (current.effects[i] == null) continue;
            DisasterEffectRuntime rt = current.effects[i].CreateRuntime(Severity);
            runtimes.Add(rt);
            rt.Begin(this);
        }
        active = true;
        ended = false;
        OnDisasterStarted?.Invoke(current, Severity);
    }

    public void End()
    {
        if (!active || ended) { active = false; return; }
        ended = true;
        for (int i = 0; i < runtimes.Count; i++) runtimes[i].End(this);
        runtimes.Clear();
        active = false;
    }

    private void Update()
    {
        if (!active) return;
        GamePhase phase = GameManager.Instance != null ? GameManager.Instance.Phase : GamePhase.Day;
        bool dayOver = phase == GamePhase.Won || phase == GamePhase.Lost || phase == GamePhase.DayEnd;
        if (dayOver) { End(); return; }

        if (phase == GamePhase.Night && !current.activeAtNight) return;
        for (int i = 0; i < runtimes.Count; i++) runtimes[i].Tick(this, Time.deltaTime);
    }

    private void OnDisable() { End(); }

    public void DamagePlayer(int amount) { if (playerHealth != null) playerHealth.TakeDamage(amount); }

    public void SetPlayerSpeed(string source, float multiplier)
    {
        if (playerController == null) return;
        if (Mathf.Approximately(multiplier, 1f)) playerController.SpeedModifiers.Remove(source);
        else playerController.SpeedModifiers.Set(source, multiplier);
    }

    public void SpawnFx(GameObject prefab, Vector3 position, float lifetime)
    {
        if (prefab == null) return;
        Destroy(Instantiate(prefab, position, Quaternion.identity), Mathf.Max(0.1f, lifetime));
    }

    public void Shake(float strength) { if (feedback != null) feedback.Shake(strength); }

    [ContextMenu("Debug: Force Random Disaster")]
    private void DebugForce()
    {
        if (registry == null || registry.All.Count == 0) return;
        DisasterData d = registry.All[UnityEngine.Random.Range(0, registry.All.Count)];
        Begin(new DisasterRoll { disasterId = d.Id, severity01 = 1f }, UnityEngine.Random.Range(0, 99999), 0);
    }
}
