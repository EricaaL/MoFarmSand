using System.Collections.Generic;
using UnityEngine;

/// <summary>A permanent unlock bought from the vault between runs. Its effects apply at the start of every run.</summary>
[CreateAssetMenu(fileName = "Unlock_New", menuName = "MoFarmSand/Meta/Unlock")]
public class UnlockData : ScriptableObject, IHasId
{
    public string unlockId;
    public string displayName;
    [TextArea] public string description;
    [Tooltip("Vault coins required.")]
    public int cost = 40;
    public List<RunEffect> onRunStart = new List<RunEffect>();

    [Header("New crops")]
    [Tooltip("Seeds this unlock makes available. Until it is bought they never appear in the shop, starting seeds or bonus drops. Single owner of which seeds are locked.")]
    public List<SeedData> unlocksSeeds = new List<SeedData>();

    public bool UnlocksCrops => unlocksSeeds != null && unlocksSeeds.Count > 0;

    public string Id => unlockId;
}
