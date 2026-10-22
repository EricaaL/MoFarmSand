using System.Collections.Generic;
using UnityEngine;

public class FarmTile : MonoBehaviour, IInteractable, IFarmPlot
{
    public enum TileState { Empty, Growing, Harvestable }

    [SerializeField] private CropData defaultCrop;
    [SerializeField] private FarmConditions conditions;
    [Tooltip("Legacy trailer behavior: plant the active crop (or defaultCrop) for free. Roguelike runs turn this off so planting needs a seed.")]
    [SerializeField] private bool allowFreePlanting = true;
    [SerializeField] private Transform cropVisual;
    [SerializeField] private Renderer cropRenderer;
    [SerializeField] private Renderer soilRenderer;
    [SerializeField] private TextMesh label;
    [SerializeField] private GameObject witherFx;
    [SerializeField] private Color soilEmpty = new Color(0.35f, 0.22f, 0.12f);
    [SerializeField] private Color soilReady = new Color(0.9f, 0.8f, 0.3f);
    [SerializeField] private Color soilFlooded = new Color(0.2f, 0.35f, 0.6f);
    [SerializeField] private float matureScale = 0.7f;
    private GameObject stageInstance;
    private Vector3 stageBaseScale = Vector3.one;
    private int stageIndex = -1;
    private float stagePop = 1f;
    private Vector3 stageBasePos;
    private float stagePhase;
    private TileState state;
    private CropData crop;
    private float quality = 1f;
    private float timer;
    private float popTime = 1f;
    private float blockTimer;
    private PlayerInventory playerInventory;

    public TileState State => state;
    public CropData Crop => crop;
    public bool IsBlocked => blockTimer > 0f;
    /// <summary>Strength of the crop growing here, fixed when planted (1 = normal; lower in the wrong season).</summary>
    public float Quality => quality;
    public float Progress01 => state == TileState.Harvestable ? 1f
        : state == TileState.Growing && crop != null && crop.growTime > 0f ? 1f - Mathf.Clamp01(timer / crop.growTime) : 0f;

    // IFarmPlot
    public bool IsGrowing => state == TileState.Growing;
    public Vector3 Position => transform.position;

    private float Rate => conditions != null && crop != null ? conditions.GrowthRateFor(crop) : 1f;

    public GameObject WitherFx => witherFx;
    public void SetWitherFx(GameObject fx) { witherFx = fx; }

    /// <summary>True for plots the player placed (not the starting field). Only these can be picked up.</summary>
    public bool IsPlaced { get; private set; }
    public bool CanPickUp => IsPlaced && state == TileState.Empty && !IsBlocked;
    public void SetPlaced(bool placed) { IsPlaced = placed; }

    /// <summary>Squash-pop, used when a plot is set down.</summary>
    public void Pop() { popTime = 0f; }

    public void SetFreePlanting(bool allowed) { allowFreePlanting = allowed; }
    public void SetConditions(FarmConditions c) { conditions = c; }

    public string Prompt
    {
        get
        {
            if (IsBlocked) return "Flooded...";
            switch (state)
            {
                case TileState.Empty: return EmptyPrompt();
                case TileState.Growing:
                {
                    float rate = Rate;
                    string extra = Mathf.Abs(rate - 1f) > 0.02f ? $" (x{rate:0.0})" : string.Empty;
                    string weak = quality < 0.9f ? $"  [weak crop {Mathf.RoundToInt(quality * 100f)}%]" : string.Empty;
                    return $"Growing... {Mathf.CeilToInt(timer / Mathf.Max(0.05f, rate))}s{extra}{weak}";
                }
                default: return "[E] Harvest";
            }
        }
    }

    private string EmptyPrompt()
    {
        string pickUp = CanPickUp ? "[R] Pick up plot" : string.Empty;
        if (allowFreePlanting) return "[E] Plant";
        if (playerInventory == null) return pickUp;
        SeedData seed = playerInventory.ActiveSeed;
        if (seed != null && playerInventory.ActiveCount > 0)
        {
            // Warn before planting out of season: the crop will be slow and weak.
            float q = conditions != null && seed.grows != null ? conditions.QualityFor(seed.grows) : 1f;
            string warn = q < 0.9f ? $"  - wrong season: slow, {Mathf.RoundToInt(q * 100f)}% strength" : string.Empty;
            return $"[E] Plant {seed.DisplayName} ({playerInventory.ActiveCount} left){warn}" + (CanPickUp ? "   " + pickUp : string.Empty);
        }
        // Not holding a seed: say nothing about planting. (The old "Need seeds" hint showed even with seeds elsewhere in the hotbar.)
        return pickUp;
    }

    private static readonly List<FarmTile> all = new List<FarmTile>();

    /// <summary>Raised after a seed is planted: the tile and who planted it (drives the farming animation).</summary>
    public static event System.Action<FarmTile, GameObject> Planted;
    /// <summary>Raised when a growing crop becomes ripe.</summary>
    public static event System.Action<FarmTile> Ripened;
    /// <summary>Raised after a ripe crop is picked (the crop that was harvested).</summary>
    public static event System.Action<FarmTile, CropData> Harvested;
    /// <summary>Raised when a disaster destroys what was growing.</summary>
    public static event System.Action<FarmTile> Withered;

    private void OnEnable() { all.Add(this); }
    private void OnDisable() { all.Remove(this); }

    /// <summary>The ripe plot under the screen point (nearest to where the cursor meets the ground), or null.</summary>
    public static FarmTile HarvestableAt(Camera cam, Vector2 screenPoint, float maxDistance = 1.6f)
    {
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(screenPoint);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, 0.1f, 0f));
        if (!ground.Raycast(ray, out float enter)) return null;
        Vector3 hit = ray.GetPoint(enter);
        FarmTile best = null;
        float bestDist = maxDistance;
        foreach (FarmTile tile in all)
        {
            if (tile.state != TileState.Harvestable) continue;
            Vector3 d = tile.transform.position - hit; d.y = 0f;
            if (d.magnitude < bestDist) { bestDist = d.magnitude; best = tile; }
        }
        return best;
    }

    private void Awake()
    {
        SetState(TileState.Empty);
    }

    private void Start()
    {
        playerInventory = FindFirstObjectByType<PlayerInventory>();
    }

    public bool CanInteract(GameObject who)
    {
        if (GameManager.Instance != null && !GameManager.Instance.CanFarm) return false;
        if (IsBlocked) return false;
        return state != TileState.Growing;
    }

    public void Interact(GameObject who)
    {
        if (!CanInteract(who)) return;
        PlayerInventory inv = who.GetComponent<PlayerInventory>();
        if (state == TileState.Empty)
        {
            SeedData seed = inv != null ? inv.ActiveSeed : null;
            if (seed != null && seed.grows != null && inv.TryConsume(seed, 1))
            {
                crop = seed.grows;
            }
            else if (allowFreePlanting)
            {
                CropData pick = inv != null ? inv.ActiveCrop : null;
                crop = pick != null ? pick : defaultCrop;
            }
            else
            {
                return;
            }
            if (crop == null) return;
            // Strength is locked in when planted. Legacy (no conditions) crops are always full strength.
            quality = conditions != null ? conditions.QualityFor(crop) : 1f;
            timer = crop.growTime;
            SetState(TileState.Growing);
            popTime = 0f;
            Planted?.Invoke(this, who);
        }
        else if (state == TileState.Harvestable)
        {
            if (inv != null && !inv.AddItem(crop, crop.harvestYield, quality)) return;
            // Permanent unlocks (e.g. Lucky Sprout) can add a small chance of a free random seed on top of the crop.
            if (conditions != null) conditions.TryGrantBonusSeed(inv);
            SetState(TileState.Empty);
            popTime = 0f;
            Harvested?.Invoke(this, crop);
        }
    }

    /// <summary>Disaster hook: destroys whatever is growing.</summary>
    public void Wither()
    {
        if (state == TileState.Empty) return;
        if (witherFx != null) Destroy(Instantiate(witherFx, transform.position + Vector3.up * 0.3f, Quaternion.identity), 2f);
        SetState(TileState.Empty);
        popTime = 0f;
        Withered?.Invoke(this);
    }

    /// <summary>Disaster hook: the tile can't be used for a while (flood).</summary>
    public void Block(float seconds)
    {
        blockTimer = Mathf.Max(blockTimer, seconds);
        if (soilRenderer != null) soilRenderer.material.color = soilFlooded;
    }

    private void Update()
    {
        if (blockTimer > 0f)
        {
            blockTimer -= Time.deltaTime;
            if (blockTimer <= 0f && soilRenderer != null && state != TileState.Harvestable) soilRenderer.material.color = soilEmpty;
        }

        if (state == TileState.Growing)
        {
            timer -= Time.deltaTime * Rate;
            float t = crop.growTime <= 0f ? 1f : 1f - Mathf.Clamp01(timer / crop.growTime);
            if (HasStages) ShowStage(crop.StageIndexFor(t, false));
            else if (cropVisual != null) cropVisual.localScale = Vector3.one * matureScale * Mathf.Lerp(0.3f, 1f, t);
            if (label != null) label.text = Mathf.CeilToInt(Mathf.Max(0f, timer / Mathf.Max(0.05f, Rate))) + "s";
            if (timer <= 0f)
            {
                SetState(TileState.Harvestable);
                Ripened?.Invoke(this);
            }
        }
        else if (state == TileState.Harvestable && soilRenderer != null && !IsBlocked)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
            soilRenderer.material.color = Color.Lerp(soilEmpty, soilReady, pulse);
        }

        // Squash/pop when a growth stage model swaps in.
        if (stageInstance != null && stagePop < 1f)
        {
            stagePop = Mathf.Min(1f, stagePop + Time.deltaTime * 4f);
            float s = 1f + 0.3f * Mathf.Sin(stagePop * Mathf.PI);
            stageInstance.transform.localScale = Vector3.Scale(stageBaseScale, new Vector3(s, 2f - s, s));
        }

        // A ripe model bobs and turns slowly so it reads as "ready" from across the field.
        if (stageInstance != null && state == TileState.Harvestable)
        {
            Transform t = stageInstance.transform;
            t.position = stageBasePos + Vector3.up * (0.06f * Mathf.Sin(Time.time * 3f + stagePhase));
            t.Rotate(Vector3.up, 25f * Time.deltaTime, Space.World);
        }

        // Squash/pop feedback on plant and harvest.
        if (popTime < 1f)
        {
            popTime += Time.deltaTime * 4f;
            float s = 1f + 0.3f * Mathf.Sin(Mathf.Clamp01(popTime) * Mathf.PI);
            transform.localScale = new Vector3(s, 2f - s, s);
        }
        else if (transform.localScale != Vector3.one)
        {
            transform.localScale = Vector3.one;
        }
    }

    private bool HasStages => crop != null && crop.growthStages != null && crop.growthStages.Length > 0;

    private void ShowStage(int i)
    {
        if (i == stageIndex || cropVisual == null) return;
        GameObject prefab = crop.growthStages[i];
        if (prefab == null) return;
        if (stageInstance != null) Destroy(stageInstance);
        stageIndex = i;
        // The plot root can be scaled (the soil is a scaled mesh), so size and place the model in world units.
        Transform parent = cropVisual.parent;
        stageInstance = Instantiate(prefab, parent);
        // Keep the model's authored scale (FBX unit fix-ups can be x100+) and multiply growthStageScale on top.
        Vector3 authored = prefab.transform.localScale;
        Vector3 lossy = parent.lossyScale;
        // The tile root squash-pops on plant; divide that out so it isn't baked into the stage scale.
        if (parent == transform)
        {
            Vector3 pop = transform.localScale;
            lossy = new Vector3(lossy.x / pop.x, lossy.y / pop.y, lossy.z / pop.z);
        }
        stageBaseScale = new Vector3(
            authored.x * crop.growthStageScale / lossy.x,
            authored.y * crop.growthStageScale / lossy.y,
            authored.z * crop.growthStageScale / lossy.z);
        stageInstance.transform.localScale = stageBaseScale;
        // Seat the model on the soil by its bounds (pivots on imported meshes are often off-centre).
        float top = soilRenderer != null ? soilRenderer.bounds.max.y : transform.position.y;
        stageInstance.transform.position = new Vector3(transform.position.x, top, transform.position.z);
        Renderer[] renderers = stageInstance.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int r = 1; r < renderers.Length; r++) b.Encapsulate(renderers[r].bounds);
            stageInstance.transform.position += new Vector3(transform.position.x - b.center.x, top - b.min.y, transform.position.z - b.center.z);
        }
        stageBasePos = stageInstance.transform.position;
        stagePhase = transform.position.x * 1.7f + transform.position.z * 0.9f; // neighbours bob out of step
        stagePop = 0f;
    }

    private void ClearStage()
    {
        if (stageInstance != null) Destroy(stageInstance);
        stageInstance = null;
        stageIndex = -1;
    }

    private void SetState(TileState next)
    {
        state = next;
        bool hasCrop = next != TileState.Empty;
        if (!hasCrop) ClearStage();
        bool stages = hasCrop && HasStages;
        if (stages && next == TileState.Harvestable) ShowStage(crop.growthStages.Length - 1);
        if (cropVisual != null)
        {
            cropVisual.gameObject.SetActive(hasCrop && !stages);
            if (next == TileState.Harvestable) cropVisual.localScale = Vector3.one * matureScale;
        }
        if (hasCrop && cropRenderer != null && crop != null) cropRenderer.material.color = crop.color;
        if (soilRenderer != null) soilRenderer.material.color = IsBlocked ? soilFlooded : soilEmpty;
        if (label != null) label.gameObject.SetActive(next == TileState.Growing);
    }
}
