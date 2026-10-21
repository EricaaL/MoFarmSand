using UnityEngine;

/// <summary>Live state of one effect during a day. Plain object so the shared SO asset is never mutated.</summary>
public abstract class DisasterEffectRuntime
{
    public virtual void Begin(IFarmWorld world) { }
    public virtual void Tick(IFarmWorld world, float dt) { }
    public virtual void End(IFarmWorld world) { }
}

/// <summary>Template for one disaster behavior. Compose several on a <see cref="DisasterData"/>.</summary>
public abstract class DisasterEffect : ScriptableObject
{
    public abstract DisasterEffectRuntime CreateRuntime(float severity01);
}
