using UnityEngine;

public class BellTrigger : MonoBehaviour, IInteractable
{
    public string Prompt
    {
        get
        {
            bool encounter = GameManager.Instance == null || GameManager.Instance.HasEncounter;
            return encounter ? "[E] Ring the night bell" : "[E] Ring the bell to end the day";
        }
    }

    public bool CanInteract(GameObject who)
    {
        if (GameManager.Instance == null || GameManager.Instance.Phase != GamePhase.Day) return false;
        // No night fight today: the bell simply ends the day.
        if (!GameManager.Instance.HasEncounter) return true;
        // Strict ammo cap: need at least one harvested crop before facing the night.
        PlayerInventory inv = who != null ? who.GetComponent<PlayerInventory>() : null;
        return inv == null || inv.TotalCount() > 0;
    }

    /// <summary>Raised when the bell is rung (before the day ends / night falls). Drives the bell sound.</summary>
    public static event System.Action Rung;

    /// <summary>The day clock ran out: the bell rings by itself (sound and swing) as night falls.</summary>
    public static void RingByItself()
    {
        BellTrigger bell = FindFirstObjectByType<BellTrigger>();
        if (bell != null && bell.swing != null) bell.swing.Ring(bell.transform.position - bell.transform.forward);
        Rung?.Invoke();
    }

    private BellSwing swing;

    private void Awake() => swing = GetComponent<BellSwing>();

    public void Interact(GameObject who)
    {
        if (!CanInteract(who)) return;
        if (swing != null) swing.Ring(who != null ? who.transform.position : transform.position - transform.forward);
        Rung?.Invoke();
        GameManager.Instance.StartNight();
    }
}
