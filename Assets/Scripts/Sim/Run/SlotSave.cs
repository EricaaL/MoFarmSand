using System;

/// <summary>Saved form of one inventory slot. Items are referenced by stable id (see ItemRegistry).</summary>
[Serializable]
public struct SlotSave
{
    public string itemId;
    public int count;
    /// <summary>Average strength of the stack (1 = normal). Old saves without it read as 0 and are treated as 1.</summary>
    public float quality;

    public SlotSave(string itemId, int count, float quality = 1f)
    {
        this.itemId = itemId;
        this.count = count;
        this.quality = quality;
    }

    public float EffectiveQuality => quality > 0f ? quality : 1f;
}
