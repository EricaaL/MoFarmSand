using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class PlayerInventory : MonoBehaviour
{
    public const int SlotCount = 8;

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
    public int ActiveIndex { get; private set; }
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
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8
    };

    private void Awake()
    {
        if (slots == null || slots.Length != SlotCount) Array.Resize(ref slots, SlotCount);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            for (int i = 0; i < HotKeys.Length; i++)
            {
                if (kb[HotKeys[i]].wasPressedThisFrame) { SetActive(i); break; }
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.1f) SetActive((ActiveIndex + SlotCount - 1) % SlotCount);
            else if (scroll < -0.1f) SetActive((ActiveIndex + 1) % SlotCount);
        }
    }

    public Slot GetSlot(int i) => slots[i];

    public void SetActive(int index)
    {
        index = Mathf.Clamp(index, 0, SlotCount - 1);
        if (index == ActiveIndex) return;
        ActiveIndex = index;
        OnChanged?.Invoke();
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
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i].item == null) { target = i; break; }
            }
        }
        if (target < 0) return false;

        int existing = slots[target].item == item ? slots[target].count : 0;
        float blended = existing > 0
            ? (slots[target].EffectiveQuality * existing + quality * amount) / (existing + amount)
            : quality;
        slots[target].item = item;
        slots[target].count += amount;
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
        if (slots[ActiveIndex].count <= 0 && item is SeedData) slots[ActiveIndex].item = null;
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
        ActiveIndex = Mathf.Clamp(activeIndex, 0, SlotCount - 1);
        OnChanged?.Invoke();
    }

    [ContextMenu("Debug Add Starter Crops")]
    private void DebugAddStarterCrops()
    {
        if (starterCrops == null) return;
        foreach (CropData c in starterCrops) AddItem(c, 5);
    }
}
