using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public const int MaxShieldCharges = 4;

    [SerializeField] private int maxHp = 3;

    public int Hp { get; private set; }
    public int MaxHp => maxHp;
    /// <summary>Hits the Bark Shield can still block.</summary>
    public int ShieldCharges { get; private set; }
    public bool HasShield => ShieldCharges > 0;

    public event Action OnChanged;
    public event Action OnDied;

    private void Awake() { Hp = maxHp; }

    /// <summary>Sets max and current HP from saved run state; clears the shield.</summary>
    public void Restore(int hp, int max)
    {
        maxHp = Mathf.Max(1, max);
        Hp = Mathf.Clamp(hp, 1, maxHp);
        ShieldCharges = 0;
        OnChanged?.Invoke();
    }

    /// <summary>Adds Bark Shield charges; each one blocks one hit.</summary>
    public void GrantShield(int charges = 1)
    {
        ShieldCharges = Mathf.Min(MaxShieldCharges, ShieldCharges + Mathf.Max(1, charges));
        OnChanged?.Invoke();
    }

    /// <summary>Costs hearts directly. Not a hit, so the Bark Shield can't block it (used for falling off the map).</summary>
    public void LoseHeart(int amount = 1)
    {
        if (Hp <= 0 || amount <= 0) return;
        Hp = Mathf.Max(0, Hp - amount);
        OnChanged?.Invoke();
        if (Hp == 0) OnDied?.Invoke();
    }

    public void TakeDamage(int amount)
    {
        if (Hp <= 0 || amount <= 0) return;
        if (HasShield)
        {
            ShieldCharges--;
            OnChanged?.Invoke();
            return;
        }
        Hp = Mathf.Max(0, Hp - amount);
        OnChanged?.Invoke();
        if (Hp == 0) OnDied?.Invoke();
    }
}
