using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Starting loadout and run-level tuning.</summary>
[CreateAssetMenu(fileName = "RunConfig", menuName = "MoFarmSand/Run Config")]
public class RunConfig : ScriptableObject
{
    [Serializable]
    public struct SeedPoolEntry
    {
        public SeedData seed;
        public float weight;
        public Vector2Int countRange;
    }

    public int startingCoins = 5;
    public int startingMaxHp = 3;

    [Header("Random starting seeds")]
    public List<SeedPoolEntry> startingSeedPool = new List<SeedPoolEntry>();
    [Tooltip("How many different seed types a new run starts with (min, max inclusive).")]
    public Vector2Int startingSeedPicks = new Vector2Int(2, 3);

    [Tooltip("Crop variants you've unlocked can also be among your starting seeds. Weight in the starting pool and how many you get.")]
    public float unlockedSeedWeight = 1.5f;
    public Vector2Int unlockedSeedCount = new Vector2Int(1, 2);

    [Header("Clock")]
    [Tooltip("Seconds of daylight per day (scaled by the season). When it runs out, night falls on its own. 0 = unlimited.")]
    public float dayLengthSeconds = 50f;
    [Tooltip("Seconds to feed the boss at night. If dawn breaks first, the run is lost. 0 = unlimited.")]
    public float nightLengthSeconds = 75f;

    /// <summary>Seconds of daylight in a season's days (0 = no clock).</summary>
    public float DayLengthFor(SeasonData season)
        => dayLengthSeconds > 0f ? dayLengthSeconds * (season != null ? season.dayLengthMultiplier : 1f) : 0f;

    [Header("Flow")]
    public float dayEndDelay = 2.5f;
    [Tooltip("How long the YOU DIED screen plays (red title, black creeping in from the edges) before a lost day reloads.")]
    public float deathScreenSeconds = 4.5f;
}
