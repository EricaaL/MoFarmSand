using UnityEngine;
using UnityEngine.Audio;

/// <summary>Every sound the game plays, by name. Drop clips into the cues; AudioDirector decides when each one plays.</summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "MoFarmSand/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [Header("Mixing")]
    [Tooltip("Mixer group with a +6 dB boost. Cues with Dialogue ticked play through it, so voices can go past full volume.")]
    public AudioMixerGroup dialogueGroup;

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
    [Tooltip("Plays when the pause menu opens (Esc).")]
    public AudioCue pauseOpen;
    [Tooltip("Plays when an in-play tip card pops up.")]
    public AudioCue tip;

    [Header("Dog")]
    [Tooltip("Played while the dog runs along the ground.")]
    public AudioCue footstep;
    public AudioCue wither;
    public AudioCue shieldBreak;
    public AudioCue death;
    [Tooltip("The dog's yelp on death. Plays together with the Death stinger.")]
    public AudioCue deathBark;
    public AudioCue fall;
    [Tooltip("Plays when the dog starts a dance from the Q wheel.")]
    public AudioCue dance;

    [Header("Crow merchant")]
    [Tooltip("Plays when the shop screen opens, on top of the shop-open sound.")]
    public AudioCue crowGreet;
    [Tooltip("Plays when the shop screen opens (one of several clips at random).")]
    public AudioCue shopOpen;
    public AudioCue crowBuy;
    public AudioCue crowSell;
    [Tooltip("Plays when BUY is clicked but can't go through: sold out, not enough coins, or inventory full.")]
    public AudioCue crowNo;
    [Tooltip("Plays when you leave the shop screen (back button or Esc).")]
    public AudioCue crowBye;

    [Header("Crow merchant dialogue")]
    [Tooltip("The crow's voice line after you buy something. Plays on top of the Crow Buy coin sound.")]
    public AudioCue crowBuyDialogue;
    [Tooltip("The crow's voice line after you sell something. Plays on top of the Crow Sell coin sound.")]
    public AudioCue crowSellDialogue;

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
