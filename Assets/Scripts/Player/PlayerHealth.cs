using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public const int MaxShieldCharges = 4;
    /// <summary>How long a Bark Shield lasts when a crop doesn't say otherwise (CropData.shieldSeconds starts from this).</summary>
    public const float DefaultShieldSeconds = 20f;

    [SerializeField] private int maxHp = 3;

    public int Hp { get; private set; }
    public int MaxHp => maxHp;
    /// <summary>Hits the Bark Shield can still block.</summary>
    public int ShieldCharges { get; private set; }
    public bool HasShield => ShieldCharges > 0;
    /// <summary>Seconds until the Bark Shield wears off, whether or not it has blocked anything.</summary>
    public float ShieldTimeLeft { get; private set; }

    public event Action OnChanged;
    public event Action OnDied;

    private void Awake() { Hp = maxHp; }

    private void Update() { TickShield(Time.deltaTime); }

    /// <summary>Counts the shield down; when time runs out every remaining charge is lost.</summary>
    public void TickShield(float dt)
    {
        if (ShieldTimeLeft <= 0f) return;
        ShieldTimeLeft -= dt;
        if (ShieldTimeLeft > 0f) return;
        ShieldTimeLeft = 0f;
        if (ShieldCharges == 0) return;
        ShieldCharges = 0;
        OnChanged?.Invoke();
    }

    /// <summary>Sets max and current HP from saved run state; clears the shield.</summary>
    public void Restore(int hp, int max)
    {
        maxHp = Mathf.Max(1, max);
        Hp = Mathf.Clamp(hp, 1, maxHp);
        ShieldCharges = 0;
        ShieldTimeLeft = 0f;
        OnChanged?.Invoke();
    }

    /// <summary>Adds Bark Shield charges; each one blocks one hit. The shield lasts <paramref name="seconds"/>; eating again never shortens it.</summary>
    public void GrantShield(int charges = 1, float seconds = DefaultShieldSeconds)
    {
        ShieldCharges = Mathf.Min(MaxShieldCharges, ShieldCharges + Mathf.Max(1, charges));
        ShieldTimeLeft = Mathf.Max(ShieldTimeLeft, seconds);
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
            if (ShieldCharges == 0) ShieldTimeLeft = 0f;
            OnChanged?.Invoke();
            return;
        }
        Hp = Mathf.Max(0, Hp - amount);
        OnChanged?.Invoke();
        if (Hp == 0) OnDied?.Invoke();
    }
}
