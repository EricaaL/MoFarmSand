using System;
using UnityEngine;

/// <summary>The farmer's coins for the current run.</summary>
public class PlayerWallet : MonoBehaviour
{
    public int Coins { get; private set; }

    /// <summary>Seam event for the HUD.</summary>
    public event Action<int> OnChanged;

    public void Set(int coins)
    {
        Coins = Mathf.Max(0, coins);
        OnChanged?.Invoke(Coins);
    }

    public void Add(int amount) { Set(Coins + amount); }

    public bool TrySpend(int amount)
    {
        if (amount < 0 || Coins < amount) return false;
        Set(Coins - amount);
        return true;
    }
}
