using System;
using UnityEngine;

/// <summary>
/// Knows when the dog has really died (the day was lost with no hearts left). PlayerController and PlayerCombat read
/// <see cref="IsDead"/> to lock every control, and PlayerAnimator plays the death clip on <see cref="Died"/>.
/// The scene reloads after a loss, so there is no way back from here.
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
public class PlayerDeath : MonoBehaviour
{
    private PlayerHealth health;
    private GameManager subscribedTo;

    public bool IsDead { get; private set; }

    public event Action Died;

    private void Awake() { health = GetComponent<PlayerHealth>(); }

    private void Start()
    {
        subscribedTo = GameManager.Instance;
        if (subscribedTo != null) subscribedTo.OnPhaseChanged += HandlePhase;
    }

    private void OnDestroy()
    {
        if (subscribedTo != null) subscribedTo.OnPhaseChanged -= HandlePhase;
    }

    private void HandlePhase(GamePhase phase)
    {
        // Dawn breaking with hearts left is also a loss, but the dog doesn't collapse for it.
        if (IsDead || phase != GamePhase.Lost || health.Hp > 0) return;
        IsDead = true;
        Died?.Invoke();
    }
}
