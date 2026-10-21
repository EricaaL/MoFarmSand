using System.Collections.Generic;

/// <summary>Multiplicative modifiers keyed by source, so each source can be set and removed independently.</summary>
public sealed class ModifierStack
{
    private readonly Dictionary<string, float> mults = new Dictionary<string, float>();

    public int Version { get; private set; }

    public float Product
    {
        get
        {
            float p = 1f;
            foreach (KeyValuePair<string, float> kv in mults) p *= kv.Value;
            return p;
        }
    }

    public void Set(string source, float mult)
    {
        mults[source] = mult;
        Version++;
    }

    public bool Remove(string source)
    {
        bool removed = mults.Remove(source);
        if (removed) Version++;
        return removed;
    }

    public bool Has(string source) => mults.ContainsKey(source);

    public void Clear()
    {
        mults.Clear();
        Version++;
    }
}
