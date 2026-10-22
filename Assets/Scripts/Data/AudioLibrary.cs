using UnityEngine;

/// <summary>Every sound the game plays, by name. Drop clips into the cues; AudioDirector decides when each one plays.</summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "MoFarmSand/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [Header("Farming")]
    public AudioCue plant;
    public AudioCue ripe;
    public AudioCue harvest;
    public AudioCue coin;

    [Header("Combat")]
    public AudioCue throwCrop;
    public AudioCue shoot;
    public AudioCue splat;
    public AudioCue shield;
    public AudioCue hurt;

    [Header("Boss")]
    public AudioCue bossGulp;
    public AudioCue bossScare;
    public AudioCue burst;
    public AudioCue evolve;

    [Header("World")]
    public AudioCue bell;

    [Header("Music")]
    public AudioCue dayMusic;
    public AudioCue nightMusic;
}
