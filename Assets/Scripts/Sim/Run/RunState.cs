using System;
using System.Collections.Generic;

public enum RunPhase { ChoosingNode, InDay, Banking, Won, Lost }

[Serializable]
public class RunStats
{
    public int daysSurvived;
    public int layersReached;
    public int coinsEarned;
    public int coinsLost;
    public int bossesFed;
}

/// <summary>
/// Everything about the active run, between days. Live scene components own these values during a day;
/// RunDirector snapshots them back at the end of each day.
/// </summary>
[Serializable]
public class RunState
{
    public int runSeed;
    /// <summary>Permanent unlocks owned when this year began. They apply all year (you can only buy new ones between years).</summary>
    public List<string> unlockedIds = new List<string>();
    /// <summary>Extra farmlands owned when this year began (fixed for the year).</summary>
    public int farmlands;
    /// <summary>Stockroom level when this year began (fixed for the year).</summary>
    public int stockroomLevel;
    /// <summary>Which year of the farm this is: 1 for the first run, 2 for the second... 0 in old saves.</summary>
    public int year;
    public MapGraph map = new MapGraph();
    public int currentNodeId = -1;
    public int pendingNodeId = -1;
    /// <summary>Node whose event was already resolved, so resuming mid-day doesn't repeat it.</summary>
    public int eventResolvedNodeId = -1;
    public List<int> visited = new List<int>();
    public RunPhase phase = RunPhase.ChoosingNode;

    public int coins;
    public int hp = 3;
    public int maxHp = 3;
    public List<SlotSave> inventory = new List<SlotSave>();
    public int activeSlot;

    public MarketState market = new MarketState();
    /// <summary>What is left on the merchant's shelf. Refilled only on Merchant days.</summary>
    public List<ShopStockEntry> shopStock = new List<ShopStockEntry>();
    public List<string> modifierIds = new List<string>();
    public RunStats stats = new RunStats();

    /// <summary>Coins credited to the vault during this run (after fees).</summary>
    public int depositsThisRun;
    /// <summary>Coins spent on deposits in the current season, for the per-season cap.</summary>
    public int depositedThisSeason;
    /// <summary>Set when the final boss fell on the last day of winter: bank first, then the year ends.</summary>
    public bool endYearAfterBanking;
    /// <summary>Season index whose crops grow slower because a boss fight was lost (-1 = none).</summary>
    public int growthDebuffSeason = -1;
    /// <summary>Growth speed multiplier applied during <see cref="growthDebuffSeason"/>.</summary>
    public float growthDebuffMult = 1f;
    /// <summary>Index of the season whose bank stop is pending (valid in the Banking phase).</summary>
    public int bankSeasonIndex;

    /// <summary>Days completed so far. The next node's layer equals this.</summary>
    public int DayIndex => visited.Count;

    /// <summary>Year number for display, falling back to the years played for saves that predate it.</summary>
    public int YearNumber(MetaProgress meta) => year > 0 ? year : System.Math.Max(1, meta != null ? meta.runsStarted : 1);
}
