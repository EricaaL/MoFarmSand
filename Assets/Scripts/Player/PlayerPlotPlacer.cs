using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Places bought farmland plots from the hotbar. While a Farmland Plot is selected a ghost follows the cursor
/// (green = fits, red = blocked); left-click sets it down. [R] picks an empty placed plot back up.
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
public class PlayerPlotPlacer : MonoBehaviour
{
    [Tooltip("Layers that count as ground for placing a plot.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("A plot may sit this far above or below the starting field's height (metres).")]
    [SerializeField] private float heightTolerance = 0.6f;
    [Tooltip("Half-size of the box checked for decorations where a plot would go.")]
    [SerializeField] private Vector3 clearHalfExtents = new Vector3(0.9f, 0.4f, 0.9f);
    [SerializeField] private Color okColor = new Color(0.3f, 1f, 0.4f, 0.55f);
    [SerializeField] private Color badColor = new Color(1f, 0.25f, 0.25f, 0.55f);

    private PlayerInventory inventory;
    private PlayerCombat combat;
    private RunDirector director;
    private Camera cam;
    private GameObject ghost;
    private Renderer[] ghostRenderers;
    private string reason = string.Empty;
    private readonly Collider[] overlaps = new Collider[16];

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        combat = GetComponent<PlayerCombat>();
    }

    private void OnDisable() { HideGhost(); }

    private void Update()
    {
        if (director == null) director = FindFirstObjectByType<RunDirector>();
        if (cam == null) cam = Camera.main;
        if (director == null || cam == null || !director.IsDayRunning) { HideGhost(); return; }

        bool canAct = combat == null || combat.CanAct;
        FarmlandPlotItem item = inventory.ActiveItem as FarmlandPlotItem;
        bool holding = canAct && item != null && inventory.ActiveCount > 0 && !PlayerDance.WheelOpen;

        if (holding && !HotbarView.PointerBusy && Mouse.current != null)
        {
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (TryGetSpot(pointer, out Vector3 spot))
            {
                bool ok = CanPlaceAt(spot, out reason);
                ShowGhost(spot, ok);
                if (ok && Mouse.current.leftButton.wasPressedThisFrame) Place(spot, item);
            }
            else HideGhost();
        }
        else HideGhost();

        if (canAct && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) PickUpNearest();
    }

    /// <summary>Drop-from-hotbar placement. Same checks as clicking.</summary>
    public bool TryPlaceAt(Vector2 screenPoint, out string message)
    {
        message = string.Empty;
        FarmlandPlotItem item = inventory.ActiveItem as FarmlandPlotItem;
        if (item == null || inventory.ActiveCount <= 0) { message = "Nothing to place"; return false; }
        if (cam == null) cam = Camera.main;
        if (director == null) director = FindFirstObjectByType<RunDirector>();
        if (director == null || !TryGetSpot(screenPoint, out Vector3 spot)) { message = "No ground there"; return false; }
        if (!CanPlaceAt(spot, out message)) return false;
        return Place(spot, item, out message);
    }

    // ------------------------------------------------------------------ placing

    private bool Place(Vector3 spot, FarmlandPlotItem item) => Place(spot, item, out _);

    private bool Place(Vector3 spot, FarmlandPlotItem item, out string message)
    {
        if (!director.TryPlacePlot(spot, out FarmTile tile, out message)) { reason = message; return false; }
        inventory.TryConsume(item, 1);
        return true;
    }

    private void PickUpNearest()
    {
        float reach = Reach();
        FarmTile best = null;
        float bestDist = reach;
        foreach (FarmTile tile in FindObjectsByType<FarmTile>(FindObjectsSortMode.None))
        {
            if (!tile.CanPickUp) continue;
            Vector3 d = tile.transform.position - transform.position; d.y = 0f;
            if (d.magnitude < bestDist) { bestDist = d.magnitude; best = tile; }
        }
        if (best != null) director.TryPickUpPlot(best, out _);
    }

    private float Reach() => director != null && director.Db != null && director.Db.farmland != null ? director.Db.farmland.placeReach : 6f;

    // ------------------------------------------------------------------ checks

    private bool TryGetSpot(Vector2 screenPoint, out Vector3 spot)
    {
        spot = Vector3.zero;
        if (cam == null) return false;
        Ray ray = cam.ScreenPointToRay(screenPoint);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, 0.1f, 0f));
        if (!ground.Raycast(ray, out float enter)) return false;
        spot = director.SnapPlotSpot(ray.GetPoint(enter));
        return true;
    }

    private bool CanPlaceAt(Vector3 spot, out string why)
    {
        why = string.Empty;
        Vector3 flat = spot - transform.position; flat.y = 0f;
        if (flat.magnitude > Reach()) { why = "Too far away"; return false; }

        // Ground under the spot, near the field's height (so nothing floats over the void).
        if (!Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, groundMask, QueryTriggerInteraction.Ignore)
            || Mathf.Abs(hit.point.y - spot.y) > heightTolerance)
        { why = "No ground there"; return false; }

        // Decorations: anything solid in the way that isn't the ground we hit or the player.
        Vector3 center = new Vector3(spot.x, spot.y + clearHalfExtents.y + 0.1f, spot.z);
        int n = Physics.OverlapBoxNonAlloc(center, clearHalfExtents, overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = overlaps[i];
            if (c == hit.collider || c.transform.IsChildOf(transform) || c.GetComponentInParent<FarmTile>() != null) continue;
            why = "Something's in the way";
            return false;
        }

        if (!director.IsPlotSpotClear(spot)) { why = "Too close to another plot"; return false; }
        return true;
    }

    // ------------------------------------------------------------------ ghost

    private void ShowGhost(Vector3 spot, bool ok)
    {
        if (ghost == null && !BuildGhost()) return;
        ghost.SetActive(true);
        ghost.transform.position = spot + Vector3.up * 0.03f;
        Color c = ok ? okColor : badColor;
        foreach (Renderer r in ghostRenderers) r.material.color = c;
    }

    private void HideGhost()
    {
        if (ghost != null) ghost.SetActive(false);
    }

    /// <summary>A flat tinted slab the size of a plot, built from the plot template's soil footprint.</summary>
    private bool BuildGhost()
    {
        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = "PlotGhost";
        Collider col = slab.GetComponent<Collider>();
        if (col != null) Destroy(col);
        slab.transform.localScale = new Vector3(clearHalfExtents.x * 2f, 0.06f, clearHalfExtents.z * 2f);

        Renderer r = slab.GetComponent<Renderer>();
        // Sprites/Default is always included in builds and tints with alpha from the material colour, so no URP surface setup is needed.
        r.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

        ghost = slab;
        ghostRenderers = new[] { r };
        return true;
    }
}
