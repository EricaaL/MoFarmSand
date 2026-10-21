using System.Collections.Generic;
using UnityEngine;

/// <summary>A plot disaster effects can act on. FarmTile implements it; tests use a fake.</summary>
public interface IFarmPlot
{
    bool IsGrowing { get; }
    Vector3 Position { get; }
    void Wither();
    void Block(float seconds);
}

/// <summary>Everything a disaster effect is allowed to touch. Adapted by DisasterDirector in the scene.</summary>
public interface IFarmWorld
{
    IReadOnlyList<IFarmPlot> Plots { get; }
    ModifierStack Growth { get; }
    ModifierStack SellPrice { get; }
    ModifierStack SeedPrice { get; }
    Vector3 PlayerPosition { get; }
    SeededRng Rng { get; }
    void DamagePlayer(int amount);
    void SetPlayerSpeed(string source, float multiplier);
    void SpawnFx(GameObject prefab, Vector3 position, float lifetime);
    void Shake(float strength);
}
