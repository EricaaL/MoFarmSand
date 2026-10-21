using UnityEngine;

public interface IInteractable
{
    string Prompt { get; }
    bool CanInteract(GameObject who);
    void Interact(GameObject who);
}
