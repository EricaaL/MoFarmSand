using UnityEngine;

/// <summary>Drought / Frost / Heatwave: scales crop growth for the whole day.</summary>
[CreateAssetMenu(fileName = "Effect_Growth", menuName = "MoFarmSand/Disaster Effects/Growth Multiplier")]
public class GrowthMultiplierEffect : DisasterEffect
{
    [Tooltip("Growth multiplier at full severity (0.5 = half speed). Scaled toward 1 at lower severity.")]
    [Range(0.05f, 2f)] public float multiplierAtFullSeverity = 0.5f;

    public override DisasterEffectRuntime CreateRuntime(float severity01) => new Runtime(this, severity01);

    private sealed class Runtime : DisasterEffectRuntime
    {
        private readonly string source;
        private readonly float mult;

        public Runtime(GrowthMultiplierEffect fx, float severity)
        {
            source = "disaster:" + fx.name;
            mult = Mathf.Lerp(1f, fx.multiplierAtFullSeverity, Mathf.Clamp01(severity));
        }

        public override void Begin(IFarmWorld world) { world.Growth.Set(source, mult); }
        public override void End(IFarmWorld world) { world.Growth.Remove(source); }
    }
}
