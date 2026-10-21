using System.Collections.Generic;
using UnityEngine;

/// <summary>Storm / Hail: telegraphed strikes near the player. Great trailer moment at night.</summary>
[CreateAssetMenu(fileName = "Effect_Strike", menuName = "MoFarmSand/Disaster Effects/Strike Hazard")]
public class StrikeHazardEffect : DisasterEffect
{
    public float firstDelay = 3f;
    public float interval = 4f;
    public float telegraphSeconds = 1.2f;
    public float radius = 1.6f;
    [Tooltip("How far from the player a strike can land.")]
    public float spawnRadius = 5f;
    public int damage = 1;
    public float shake = 0.6f;
    public GameObject telegraphFx;
    public GameObject strikeFx;

    public override DisasterEffectRuntime CreateRuntime(float severity01) => new Runtime(this, severity01);

    private sealed class Runtime : DisasterEffectRuntime
    {
        private struct Pending { public Vector3 pos; public float timeLeft; }

        private readonly StrikeHazardEffect fx;
        private readonly float interval;
        private readonly List<Pending> pending = new List<Pending>();
        private float timer;

        public Runtime(StrikeHazardEffect fx, float severity)
        {
            this.fx = fx;
            // Higher severity = more frequent strikes.
            interval = Mathf.Lerp(fx.interval * 1.6f, fx.interval * 0.7f, Mathf.Clamp01(severity));
            timer = fx.firstDelay;
        }

        public override void Tick(IFarmWorld world, float dt)
        {
            timer -= dt;
            if (timer <= 0f)
            {
                timer = interval;
                float ang = world.Rng.Range(0f, Mathf.PI * 2f);
                float dist = world.Rng.Range(0f, fx.spawnRadius);
                Vector3 p = world.PlayerPosition + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                pending.Add(new Pending { pos = p, timeLeft = fx.telegraphSeconds });
                if (fx.telegraphFx != null) world.SpawnFx(fx.telegraphFx, p, fx.telegraphSeconds);
            }

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Pending s = pending[i];
                s.timeLeft -= dt;
                if (s.timeLeft > 0f) { pending[i] = s; continue; }
                pending.RemoveAt(i);

                if (fx.strikeFx != null) world.SpawnFx(fx.strikeFx, s.pos, 1.5f);
                world.Shake(fx.shake);
                Vector3 d = world.PlayerPosition - s.pos;
                d.y = 0f;
                if (d.magnitude <= fx.radius) world.DamagePlayer(fx.damage);
            }
        }
    }
}
