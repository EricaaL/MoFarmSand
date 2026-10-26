using UnityEngine;

/// <summary>Gives seeds or crops. Replaces the old "AddCrop" idea now that seeds are separate items.</summary>
[CreateAssetMenu(fileName = "Effect_AddItem", menuName = "MoFarmSand/Run Effects/Add Item")]
public class AddItemEffect : RunEffect
{
    public ItemData item;
    public int count = 3;

    public override void Apply(RunState run, GameDatabase db)
    {
        if (item != null) InventoryOps.Add(run.inventory, item.Id, count, InventoryOps.BaseSlots + DayPlanner.BonusSlots(run, db));
    }

    public override string Describe() => item != null ? $"+{count} {item.DisplayName}" : "nothing";
}
