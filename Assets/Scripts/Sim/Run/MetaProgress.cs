using System;
using System.Collections.Generic;

/// <summary>Persists across runs. Coins only get here by being deposited at the bank.</summary>
[Serializable]
public class MetaProgress
{
    public int vaultCoins;
    public List<string> unlockedIds = new List<string>();
    public int runsStarted;
    public int runsWon;
    public int bestLayer;
    /// <summary>Extra farmlands (columns of plots) bought with vault coins. Permanent.</summary>
    public int farmlands;
    /// <summary>Where the player has placed bought plots. Plots owned minus these are waiting in the hotbar. Permanent.</summary>
    public List<UnityEngine.Vector3> placedPlots = new List<UnityEngine.Vector3>();
    /// <summary>Bigger Stockroom level (0 = none). Permanent.</summary>
    public int stockroomLevel;
    /// <summary>Bigger Vault level (0 = none). Raises the vault limit. Permanent.</summary>
    public int vaultLevel;
    /// <summary>Bigger Deposits level (0 = none). Raises the per-season deposit limit. Permanent.</summary>
    public int depositLevel;
    public int lastRunDeposited;
    public int lastRunCoinsLost;
}
