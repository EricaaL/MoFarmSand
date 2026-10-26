using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class PlayerInventory : MonoBehaviour
{
    /// <summary>Hotbar slots every run has.</summary>
    public const int BaseSlots = 8;
    /// <summary>Slots that exist in storage: the base ones plus the one the Small Pouch adds. Only <see cref="Capacity"/> of them are usable.</summary>
    public const int SlotCount = BaseSlots + 1;

    [Serializable]
    public struct Slot
    {
        [FormerlySerializedAs("crop")] public ItemData item;
        public int count;
        /// <summary>Average strength of the stack: 1 = normal, lower for crops grown out of season. 0 reads as 1 (older data).</summary>
        public float quality;

        public float EffectiveQuality => quality > 0f ? quality : 1f;
    }

    [SerializeField] private Slot[] slots = new Slot[SlotCount];
    [SerializeField] private CropData[] starterCrops;

    public event Action OnChanged;
    /// <summary>Seam event for audio: the player picked a different hotbar slot (keys, scroll wheel or click).</summary>
    public static event Action SlotSelected;
    public int ActiveIndex { get; private set; }
    /// <summary>How many hotbar slots are usable right now (<see cref="BaseSlots"/> plus charms like the Small Pouch).</summary>
    public int Capacity { get; private set; } = BaseSlots;
    public ItemData ActiveItem => slots[ActiveIndex].item;
    /// <summary>Active slot as a crop (throwable / edible), or null if it holds a seed or nothing.</summary>
    public CropData ActiveCrop => slots[ActiveIndex].item as CropData;
    /// <summary>Active slot as a seed packet, or null.</summary>
    public SeedData ActiveSeed => slots[ActiveIndex].item as SeedData;
    public int ActiveCount => slots[ActiveIndex].count;
    /// <summary>Strength of the crops in the active slot (1 = normal).</summary>
    public float ActiveQuality => slots[ActiveIndex].EffectiveQuality;

    private static readonly Key[] HotKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    private void Awake()
    {
        if (slots == null || slots.Length != SlotCount) Array.Resize(ref slots, SlotCount);
    }

    private void Update()
    {
        // A run screen (map, shop, bank...) owns the wheel and number keys; don't cycle the hotbar behind it.
        if (RunDirector.ModalOpen) return;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            for (int i = 0; i < HotKeys.Length; i++)
            {
                if (i < Capacity && kb[HotKeys[i]].wasPressedThisFrame) { SetActive(i); break; }
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.1f) SetActive((ActiveIndex + Capacity - 1) % Capacity);
            else if (scroll < -0.1f) SetActive((ActiveIndex + 1) % Capacity);
        }
    }

    public Slot GetSlot(int i) => slots[i];

    /// <summary>Sets how many slots are usable (clamped to the base count..storage size). Items already in a slot stay put.</summary>
    public void SetCapacity(int capacity)
    {
        Capacity = Mathf.Clamp(capacity, BaseSlots, SlotCount);
        if (ActiveIndex >= Capacity) ActiveIndex = 0;
        OnChanged?.Invoke();
    }

    public void SetActive(int index)
    {
        index = Mathf.Clamp(index, 0, Capacity - 1);
        if (index == ActiveIndex) return;
        ActiveIndex = index;
        OnChanged?.Invoke();
        SlotSelected?.Invoke();
    }

    /// <summary>A slot with nothing in it, or only the leftover of a used-up stack (crop slots keep their item at count 0).</summary>
    private bool IsFree(int i) => slots[i].item == null || slots[i].count <= 0;

    /// <summary>True if <see cref="AddItem"/> would succeed: the item already has a stack, or a slot is free.</summary>
    public bool CanFit(ItemData item)
    {
        if (item == null) return false;
        for (int i = 0; i < SlotCount; i++)
            if (slots[i].item == item || (i < Capacity && IsFree(i))) return true;
        return false;
    }

    /// <summary>True if every item in <paramref name="items"/> could be added: each either stacks onto an existing slot or gets a free one of its own.</summary>
    public bool CanFitAll(IEnumerable<ItemData> items)
    {
        int free = 0;
        for (int i = 0; i < Capacity; i++) if (IsFree(i)) free++;
        HashSet<ItemData> needSlot = new HashSet<ItemData>();
        foreach (ItemData item in items)
        {
            if (item == null) continue;
            bool stacks = false;
            for (int i = 0; i < SlotCount && !stacks; i++) stacks = slots[i].item == item;
            if (!stacks) needSlot.Add(item);
        }
        return needSlot.Count <= free;
    }

    /// <summary>Adds items. Stacking blends quality by count, so a few weak crops weaken a strong stack proportionally.</summary>
    public bool AddItem(ItemData item, int amount, float quality = 1f)
    {
        if (item == null || amount <= 0) return false;
        int target = -1;
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i].item == item) { target = i; break; }
        }
        if (target < 0)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (IsFree(i)) { target = i; break; }
            }
        }
        if (target < 0) return false;

        int existing = slots[target].item == item ? slots[target].count : 0;
        float blended = existing > 0
            ? (slots[target].EffectiveQuality * existing + quality * amount) / (existing + amount)
            : quality;
        slots[target].item = item;
        slots[target].count = existing + amount;
        slots[target].quality = blended;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Strength of all the crops of this type you carry (1 = normal).</summary>
    public float QualityOf(ItemData item)
    {
        float total = 0f; int count = 0;
        for (int i = 0; i < SlotCount; i++)
            if (slots[i].item == item && slots[i].count > 0) { total += slots[i].EffectiveQuality * slots[i].count; count += slots[i].count; }
        return count > 0 ? total / count : 1f;
    }

    /// <summary>Consumes from the active slot if it holds the item.</summary>
    public bool TryConsume(ItemData item, int amount)
    {
        if (item == null || slots[ActiveIndex].item != item || slots[ActiveIndex].count < amount) return false;
        slots[ActiveIndex].count -= amount;
        if (slots[ActiveIndex].count <= 0 && !(item is CropData)) slots[ActiveIndex].item = null;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Removes from any slot(s) holding the item. All-or-nothing.</summary>
    public bool TryRemove(ItemData item, int amount)
    {
        if (item == null || amount <= 0 || CountOf(item) < amount) return false;
        int left = amount;
        for (int i = 0; i < SlotCount && left > 0; i++)
        {
            if (slots[i].item != item) continue;
            int take = Mathf.Min(left, slots[i].count);
            slots[i].count -= take;
            left -= take;
            if (slots[i].count <= 0 && item is FarmlandPlotItem) slots[i].item = null;
        }
        OnChanged?.Invoke();
        return true;
    }

    public int CountOf(ItemData item)
    {
        if (item == null) return 0;
        int total = 0;
        for (int i = 0; i < SlotCount; i++) if (slots[i].item == item) total += slots[i].count;
        return total;
    }

    /// <summary>Total number of throwable / edible crops (seeds don't count as ammo).</summary>
    public int TotalCount()
    {
        int total = 0;
        for (int i = 0; i < SlotCount; i++) if (slots[i].item is CropData) total += slots[i].count;
        return total;
    }

    public void Clear()
    {
        for (int i = 0; i < SlotCount; i++) slots[i] = default;
        ActiveIndex = 0;
        OnChanged?.Invoke();
    }

    public List<SlotSave> Snapshot()
    {
        List<SlotSave> list = new List<SlotSave>(SlotCount);
        for (int i = 0; i < SlotCount; i++)
        {
            Slot s = slots[i];
            bool filled = s.item != null && s.count > 0;
            list.Add(new SlotSave(filled ? s.item.Id : string.Empty, filled ? s.count : 0, filled ? s.EffectiveQuality : 1f));
        }
        return list;
    }

    public void Restore(IReadOnlyList<SlotSave> saved, int activeIndex, ItemRegistry registry)
    {
        for (int i = 0; i < SlotCount; i++) slots[i] = default;
        if (saved != null && registry != null)
        {
            for (int i = 0; i < SlotCount && i < saved.Count; i++)
            {
                if (saved[i].count <= 0 || !registry.TryGet(saved[i].itemId, out ItemData item)) continue;
                slots[i].item = item;
                slots[i].count = saved[i].count;
                slots[i].quality = saved[i].EffectiveQuality;
            }
        }
        ActiveIndex = Mathf.Clamp(activeIndex, 0, Capacity - 1);
        OnChanged?.Invoke();
    }

    [ContextMenu("Debug Add Starter Crops")]
    private void DebugAddStarterCrops()
    {
        if (starterCrops == null) return;
        foreach (CropData c in starterCrops) AddItem(c, 5);
    }
}
