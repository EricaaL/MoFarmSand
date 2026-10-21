using System.Collections.Generic;
using UnityEngine;

/// <summary>Central collection of templates indexed by stable id. Subclass once per template type.</summary>
public abstract class IdRegistry<T> : ScriptableObject where T : ScriptableObject, IHasId
{
    [SerializeField] private List<T> items = new List<T>();

    private Dictionary<string, T> lookup;

    public IReadOnlyList<T> All => items;

    public T Get(string id)
    {
        TryGet(id, out T item);
        return item;
    }

    public bool TryGet(string id, out T item)
    {
        item = null;
        if (string.IsNullOrEmpty(id)) return false;
        if (lookup == null) Rebuild();
        return lookup.TryGetValue(id, out item);
    }

    /// <summary>Adds the item if no entry with the same id exists. Returns true if added.</summary>
    public bool Ensure(T item)
    {
        if (item == null) return false;
        for (int i = 0; i < items.Count; i++) if (items[i] == item) return false;
        items.Add(item);
        lookup = null;
        return true;
    }

    /// <summary>Drops entries whose asset was deleted (editor migrations). Returns how many were removed.</summary>
    public int PruneMissing()
    {
        int removed = items.RemoveAll(i => i == null);
        if (removed > 0) lookup = null;
        return removed;
    }

    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();
        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < items.Count; i++)
        {
            T it = items[i];
            if (it == null) { errors.Add($"{name}: null entry at {i}"); continue; }
            string id = it.Id;
            if (string.IsNullOrEmpty(id)) errors.Add($"{name}: '{it.name}' has an empty id");
            else if (!seen.Add(id)) errors.Add($"{name}: duplicate id '{id}'");
        }
        return errors.Count == 0;
    }

    private void Rebuild()
    {
        lookup = new Dictionary<string, T>();
        for (int i = 0; i < items.Count; i++)
        {
            T it = items[i];
            if (it != null && !string.IsNullOrEmpty(it.Id)) lookup[it.Id] = it;
        }
    }

    private void OnEnable() { lookup = null; }
    private void OnValidate() { lookup = null; }
}
