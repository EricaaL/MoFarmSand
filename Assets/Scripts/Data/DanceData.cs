using UnityEngine;

/// <summary>One dance on the emote wheel: its label, the Animator state that plays it and its slice colour.</summary>
[CreateAssetMenu(fileName = "Dance", menuName = "MoFarmSand/Dance")]
public class DanceData : ScriptableObject
{
    public string displayName = "Dance";
    [Tooltip("Name of the state in MainCharacter.controller that plays this dance.")]
    public string stateName = "Dance";
    public Color color = new Color(1f, 0.85f, 0.35f);
}
