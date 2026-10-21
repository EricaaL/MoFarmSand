using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float range = 1.8f;
    [SerializeField] private LayerMask mask = ~0;

    private InputAction interactAction;
    private Camera cam;
    private readonly Collider[] hits = new Collider[16];

    public IInteractable Current { get; private set; }

    private void Awake()
    {
        if (inputActions == null) inputActions = InputSystem.actions;
        interactAction = inputActions != null ? inputActions.FindAction("Player/Interact") : null;
    }

    private void OnEnable() { interactAction?.Enable(); }

    private void Update()
    {
        Current = FindNearest();
        // Interact action has a Hold interaction in the asset; read the E key directly for a tap.
        bool pressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        if (pressed && Current != null) Current.Interact(gameObject);

        // Left-click a ripe crop within reach to harvest it as well.
        if (!HotbarView.PointerBusy && !PlayerDance.WheelOpen && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            FarmTile tile = PointerHarvestable();
            if (tile != null && tile.CanInteract(gameObject)) tile.Interact(gameObject);
        }
    }

    /// <summary>The ripe plot under the mouse that is close enough to harvest, or null.</summary>
    public FarmTile PointerHarvestable()
    {
        if (Mouse.current == null) return null;
        if (cam == null) cam = Camera.main;
        FarmTile tile = FarmTile.HarvestableAt(cam, Mouse.current.position.ReadValue());
        if (tile == null) return null;
        Vector3 d = tile.transform.position - transform.position; d.y = 0f;
        return d.magnitude <= range + 0.5f ? tile : null;
    }

    private IInteractable FindNearest()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, range, hits, mask, QueryTriggerInteraction.Collide);
        IInteractable best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            IInteractable it = hits[i].GetComponentInParent<IInteractable>();
            if (it == null || !it.CanInteract(gameObject)) continue;
            float d = (hits[i].transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = it; }
        }
        return best;
    }
}
