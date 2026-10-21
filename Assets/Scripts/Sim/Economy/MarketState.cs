using System;
using System.Collections.Generic;

[Serializable]
public class MarketEntry
{
    public string cropId;
    public float demand = 1f;
}

/// <summary>Per-crop sell demand (1 = normal). Saved with the run. List-based so JsonUtility can serialize it.</summary>
[Serializable]
public class MarketState
{
    public List<MarketEntry> entries = new List<MarketEntry>();

    public float GetDemand(string cropId)
    {
        for (int i = 0; i < entries.Count; i++) if (entries[i].cropId == cropId) return entries[i].demand;
        return 1f;
    }

    public void SetDemand(string cropId, float demand)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].cropId == cropId) { entries[i].demand = demand; return; }
        entries.Add(new MarketEntry { cropId = cropId, demand = demand });
    }
}
