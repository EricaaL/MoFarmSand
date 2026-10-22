using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One dance on the emote wheel: its label, the animation it plays and its slice colour.</summary>
[Serializable]
public class DanceSlot
{
    public string displayName = "Dance";
    [Tooltip("Humanoid animation clip (e.g. from the Breakdance Pack). Slots without a clip are skipped.")]
    public AnimationClip clip;
    public Color color = new Color(1f, 0.85f, 0.35f);
}

/// <summary>The dances on the emote wheel, in slice order (first slice at the top, then clockwise). At most <see cref="MaxSlots"/>.</summary>
[CreateAssetMenu(fileName = "DanceLibrary", menuName = "MoFarmSand/Dance Library")]
public class DanceLibrary : ScriptableObject
{
    public const int MaxSlots = 10;

    public List<DanceSlot> slots = new List<DanceSlot>();

    /// <summary>Fills <paramref name="into"/> with the slots that can play (they have a clip), capped at <see cref="MaxSlots"/>.</summary>
    public void CollectPlayable(List<DanceSlot> into)
    {
        into.Clear();
        foreach (DanceSlot slot in slots)
        {
            if (into.Count >= MaxSlots) break;
            if (slot != null && slot.clip != null) into.Add(slot);
        }
    }

    private void OnValidate()
    {
        if (slots.Count > MaxSlots) slots.RemoveRange(MaxSlots, slots.Count - MaxSlots);
    }
}
