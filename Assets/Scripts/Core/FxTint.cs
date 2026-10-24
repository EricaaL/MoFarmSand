using UnityEngine;

/// <summary>Presentation helper: recolours a spawned particle effect (the effect's material is white, so the start colour is the colour seen).</summary>
public static class FxTint
{
    public static void Apply(GameObject fx, Color color)
    {
        if (fx == null) return;
        ParticleSystem ps = fx.GetComponent<ParticleSystem>();
        if (ps == null) return;
        ParticleSystem.MainModule main = ps.main;
        main.startColor = color;
    }
}
