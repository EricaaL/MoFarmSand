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
    [Tooltip("Plays when you start holding left click to aim a thrown crop.")]
    public AudioCue aimCrop;
    public AudioCue throwCrop;
    public AudioCue shoot;
    public AudioCue splat;
    public AudioCue shield;
    public AudioCue hurt;

    [Header("UI")]
    public AudioCue button;
    [Tooltip("Small controls: back, close, tabs, shop cards, map nodes.")]
    public AudioCue buttonSmall;
    [Tooltip("Switching tabs: Options / How to Play / Item List, the save-slot upgrade tab.")]
    public AudioCue tabSwitch;
    [Tooltip("Picking a different item card in the inventory.")]
    public AudioCue inventorySelect;

    [Header("Dog")]
    [Tooltip("Played from an animation event on the run clip (not wired yet).")]
    public AudioCue footstep;
    public AudioCue wither;
    public AudioCue shieldBreak;
    public AudioCue death;
    public AudioCue fall;

    [Header("Crow merchant")]
    [Tooltip("Not wired yet: needs a signal for walking up to the booth.")]
    public AudioCue crowGreet;
    public AudioCue crowBuy;
    public AudioCue crowSell;
    public AudioCue crowNo;
    [Tooltip("Not wired yet: needs a signal for leaving the booth.")]
    public AudioCue crowBye;

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
