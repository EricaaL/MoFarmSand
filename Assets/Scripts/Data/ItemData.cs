using UnityEngine;

/// <summary>Anything that can sit in an inventory slot: harvested crops and seed packets.</summary>
public abstract class ItemData : ScriptableObject, IHasId
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract Sprite Icon { get; }
    public abstract Color ItemColor { get; }
}
