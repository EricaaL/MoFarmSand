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

    public void Interact(GameObject who)
    {
        if (CanInteract(who)) GameManager.Instance.StartNight();
    }
}
