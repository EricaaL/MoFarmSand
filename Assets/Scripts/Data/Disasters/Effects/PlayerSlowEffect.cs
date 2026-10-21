using UnityEngine;

/// <summary>Heatwave / Storm: the farmer moves slower.</summary>
[CreateAssetMenu(fileName = "Effect_Slow", menuName = "MoFarmSand/Disaster Effects/Player Slow")]
public class PlayerSlowEffect : DisasterEffect
{
    [Range(0.2f, 1f)] public float speedAtFullSeverity = 0.7f;

    public override DisasterEffectRuntime CreateRuntime(float severity01) => new Runtime(this, severity01);

    private sealed class Runtime : DisasterEffectRuntime
    {
        private readonly string source;
        private readonly float mult;

        public Runtime(PlayerSlowEffect fx, float severity)
        {
            source = "disaster:" + fx.name;
            mult = Mathf.Lerp(1f, fx.speedAtFullSeverity, Mathf.Clamp01(severity));
        }

        public override void Begin(IFarmWorld world) { world.SetPlayerSpeed(source, mult); }
        public override void End(IFarmWorld world) { world.SetPlayerSpeed(source, 1f); }
    }
}
