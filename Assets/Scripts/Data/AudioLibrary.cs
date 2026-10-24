using UnityEngine;

/// <summary>Every sound the game plays, by name. Drop clips into the cues; AudioDirector decides when each one plays.</summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "MoFarmSand/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [Header("Farming")]
    public AudioCue plant;
    [Tooltip("Plays when a crop grows into its second growth stage.")]
    public AudioCue grow;
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
    [Tooltip("Plays when you eat a support crop (lettuce, spinach, turnip).")]
    public AudioCue eat;

    [Header("UI")]
    public AudioCue button;
    [Tooltip("Small controls: back, close, tabs, shop cards, map nodes.")]
    public AudioCue buttonSmall;
    [Tooltip("Switching tabs: Options / How to Play / Item List, the save-slot upgrade tab.")]
    public AudioCue tabSwitch;
    [Tooltip("Picking a different item card in the inventory.")]
    public AudioCue inventorySelect;
    [Tooltip("Picking a different item card in the merchant's shop.")]
    public AudioCue shopSelect;
    [Tooltip("Plays when an in-play tip card pops up.")]
    public AudioCue tip;

    [Header("Dog")]
    [Tooltip("Played while the dog runs along the ground.")]
    public AudioCue footstep;
    public AudioCue wither;
    public AudioCue shieldBreak;
    public AudioCue death;
    public AudioCue fall;

    [Header("Crow merchant")]
    [Tooltip("Not wired yet: needs a signal for walking up to the booth.")]
    public AudioCue crowGreet;
    [Tooltip("Plays when the shop screen opens (one of several clips at random).")]
    public AudioCue shopOpen;
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
    [Tooltip("Orchestral stinger when the final boss stage (the butterfly) is fed full and defeated.")]
    public AudioCue victory;

    [Header("World")]
    public AudioCue bell;

    [Header("Music")]
    public AudioCue dayMusic;
    public AudioCue nightMusic;
    [Tooltip("Fades in over the night music when the caterpillar evolves into the butterfly.")]
    public AudioCue butterflyMusic;
}
