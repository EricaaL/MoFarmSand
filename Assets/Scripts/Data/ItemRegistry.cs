using UnityEngine;

/// <summary>Every crop and seed. Saves and inventory snapshots look items up here by id.</summary>
[CreateAssetMenu(fileName = "ItemRegistry", menuName = "MoFarmSand/Registries/Item Registry")]
public class ItemRegistry : IdRegistry<ItemData> { }
