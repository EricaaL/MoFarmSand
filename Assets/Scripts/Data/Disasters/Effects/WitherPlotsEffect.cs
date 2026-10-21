using System.Collections.Generic;
using UnityEngine;

/// <summary>Blight / Locust Swarm: periodically withers growing plots.</summary>
[CreateAssetMenu(fileName = "Effect_Wither", menuName = "MoFarmSand/Disaster Effects/Wither Plots")]
public class WitherPlotsEffect : DisasterEffect
{
    public float firstDelay = 4f;
    public float interval = 6f;
    [Tooltip("Plots withered per tick at full severity (at least 1).")]
    public int maxPerTick = 2;
    public GameObject fxPrefab;

    public override DisasterEffectRuntime CreateRuntime(float severity01) => new Runtime(this, severity01);

    private sealed class Runtime : DisasterEffectRuntime
    {
        private readonly WitherPlotsEffect fx;
        private readonly int perTick;
        private float timer;

        public Runtime(WitherPlotsEffect fx, float severity)
        {
            this.fx = fx;
            perTick = Mathf.Max(1, Mathf.RoundToInt(fx.maxPerTick * Mathf.Clamp01(severity)));
            timer = fx.firstDelay;
        }

        public override void Tick(IFarmWorld world, float dt)
        {
            timer -= dt;
            if (timer > 0f) return;
            timer = fx.interval;

            List<IFarmPlot> growing = new List<IFarmPlot>();
            for (int i = 0; i < world.Plots.Count; i++) if (world.Plots[i].IsGrowing) growing.Add(world.Plots[i]);
            world.Rng.Shuffle(growing);
            int n = Mathf.Min(perTick, growing.Count);
            for (int i = 0; i < n; i++)
            {
                if (fx.fxPrefab != null) world.SpawnFx(fx.fxPrefab, growing[i].Position, 2f);
                growing[i].Wither();
            }
        }
    }
}
