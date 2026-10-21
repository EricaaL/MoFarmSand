using UnityEngine;

/// <summary>The merchant's booth. Only active on Merchant days; interacting opens the shop screen.</summary>
public class MerchantStall : MonoBehaviour, IInteractable
{
    [SerializeField] private RunDirector director;

    public string Prompt => "[E] Trade with the Merchant";

    public bool CanInteract(GameObject who)
    {
        if (director == null || !director.MerchantOpen) return false;
        return GameManager.Instance == null || GameManager.Instance.Phase == GamePhase.Day;
    }

    public void Interact(GameObject who)
    {
        if (CanInteract(who)) director.OpenShop();
    }
}
