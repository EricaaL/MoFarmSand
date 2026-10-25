using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A hungry chocobo eats one planted crop (growing or ripe) each time it calls out. It is only hungry when the player picked the
/// "pet it" option (<see cref="HungryPropEffect"/>); <see cref="RunDirector"/> switches it on for that day's prop.
/// </summary>
[RequireComponent(typeof(ChocoboVoice))]
public class ChocoboAppetite : MonoBehaviour
{
    private readonly List<FarmTile> planted = new List<FarmTile>();
    private ChocoboVoice voice;

    public bool Hungry { get; set; }

    private void Awake() { voice = GetComponent<ChocoboVoice>(); }
    private void OnEnable() { voice.Called += HandleCalled; }
    private void OnDisable() { voice.Called -= HandleCalled; }

    private void HandleCalled()
    {
        if (!Hungry) return;
        planted.Clear();
        foreach (FarmTile tile in FarmTile.All) if (tile.State != FarmTile.TileState.Empty) planted.Add(tile);
        if (planted.Count == 0) return;
        planted[Random.Range(0, planted.Count)].Wither();
    }
}
