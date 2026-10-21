using UnityEngine;

/// <summary>Scarcity: changes what the merchant pays for crops and charges for seeds while the disaster lasts.</summary>
[CreateAssetMenu(fileName = "Effect_PriceShock", menuName = "MoFarmSand/Disaster Effects/Price Shock")]
public class PriceShockEffect : DisasterEffect
{
    [Tooltip("Sell-price multiplier at full severity (>1 = merchant pays more).")]
    public float sellMultiplier = 1.5f;
    [Tooltip("Seed-price multiplier at full severity.")]
    public float seedMultiplier = 1.3f;

    public override DisasterEffectRuntime CreateRuntime(float severity01) => new Runtime(this, severity01);

    private sealed class Runtime : DisasterEffectRuntime
    {
        private readonly string source;
        private readonly float sell;
        private readonly float seed;

        public Runtime(PriceShockEffect fx, float severity)
        {
            source = "disaster:" + fx.name;
            float s = Mathf.Clamp01(severity);
            sell = Mathf.Lerp(1f, fx.sellMultiplier, s);
            seed = Mathf.Lerp(1f, fx.seedMultiplier, s);
        }

        public override void Begin(IFarmWorld world)
        {
            world.SellPrice.Set(source, sell);
            world.SeedPrice.Set(source, seed);
        }

        public override void End(IFarmWorld world)
        {
            world.SellPrice.Remove(source);
            world.SeedPrice.Remove(source);
        }
    }
}
