using System.Collections.Generic;
using UnityEngine;

/// <summary>Flood: blocks some plots for a while.</summary>
[CreateAssetMenu(fileName = "Effect_Block", menuName = "MoFarmSand/Disaster Effects/Block Plots")]
public class BlockPlotsEffect : DisasterEffect
{
    [Tooltip("Fraction of plots blocked at full severity.")]
    [Range(0f, 1f)] public float fractionAtFullSeverity = 0.4f;
    public float seconds = 25f;
    public GameObject fxPrefab;

    public override DisasterEffectRuntime CreateRuntime(float severity01) => new Runtime(this, severity01);

    private sealed class Runtime : DisasterEffectRuntime
    {
        private readonly BlockPlotsEffect fx;
        private readonly float severity;

        public Runtime(BlockPlotsEffect fx, float severity) { this.fx = fx; this.severity = Mathf.Clamp01(severity); }

        public override void Begin(IFarmWorld world)
        {
            List<IFarmPlot> plots = new List<IFarmPlot>(world.Plots);
            world.Rng.Shuffle(plots);
            int n = Mathf.Min(plots.Count, Mathf.Max(1, Mathf.RoundToInt(plots.Count * fx.fractionAtFullSeverity * severity)));
            for (int i = 0; i < n; i++)
            {
                plots[i].Block(fx.seconds);
                if (fx.fxPrefab != null) world.SpawnFx(fx.fxPrefab, plots[i].Position, fx.seconds);
            }
        }
    }
}
