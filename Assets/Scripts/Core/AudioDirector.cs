using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the game's sounds from the events it already raises, and crossfades the day and night music. Presentation only:
/// a cue with no clips is silent, so the game runs fine before any audio has been dropped in. Master volume comes from
/// <see cref="GameSettings.MasterVolume"/> through the AudioListener.
/// </summary>
public class AudioDirector : MonoBehaviour
{
    [SerializeField] private AudioLibrary library;
    [SerializeField] private BossHunger hunger;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerWallet wallet;
    [SerializeField] private int voices = 8;
    [SerializeField] private float musicFadeSeconds = 1.5f;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.6f;

    private AudioSource[] pool;
    private int nextVoice;
    private readonly Dictionary<AudioCue, float> lastPlayed = new Dictionary<AudioCue, float>();

    private AudioSource[] music;
    private readonly float[] musicTarget = new float[2];
    private int activeMusic;
    private AudioCue currentMusic;

    private int lastHp = -1;
    private int lastShield;
    private int lastCoins = -1;

    private void Awake()
    {
        pool = new AudioSource[Mathf.Max(1, voices)];
        for (int i = 0; i < pool.Length; i++) pool[i] = NewSource("Voice " + i, false);
        music = new[] { NewSource("Music A", true), NewSource("Music B", true) };
    }

    private AudioSource NewSource(string sourceName, bool loop)
    {
        GameObject go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f; // 2D: the camera follows the player, so positional audio adds nothing
        return src;
    }

    private void OnEnable()
    {
        FarmTile.Planted += HandlePlanted;
        FarmTile.Ripened += HandleRipened;
        FarmTile.Harvested += HandleHarvested;
        PlayerCombat.Fired += HandleFired;
        CropProjectile.Landed += HandleLanded;
        BellTrigger.Rung += HandleBell;
        if (hunger != null)
        {
            hunger.OnBossFed += HandleFed;
            hunger.OnScared += HandleScared;
            hunger.OnBossSatisfied += HandleSatisfied;
            hunger.OnBossEvolved += HandleEvolved;
        }
        if (health != null)
        {
            lastHp = health.Hp;
            lastShield = health.ShieldCharges;
            health.OnChanged += HandleHealth;
        }
        if (wallet != null) wallet.OnChanged += HandleCoins;
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnPhaseChanged += HandlePhase;
        if (library != null) SetMusic(library.dayMusic);
    }

    private void OnDisable()
    {
        FarmTile.Planted -= HandlePlanted;
        FarmTile.Ripened -= HandleRipened;
        FarmTile.Harvested -= HandleHarvested;
        PlayerCombat.Fired -= HandleFired;
        CropProjectile.Landed -= HandleLanded;
        BellTrigger.Rung -= HandleBell;
        if (hunger != null)
        {
            hunger.OnBossFed -= HandleFed;
            hunger.OnScared -= HandleScared;
            hunger.OnBossSatisfied -= HandleSatisfied;
            hunger.OnBossEvolved -= HandleEvolved;
        }
        if (health != null) health.OnChanged -= HandleHealth;
        if (wallet != null) wallet.OnChanged -= HandleCoins;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnPhaseChanged -= HandlePhase;
    }

    // ---------------------------------------------------------------- handlers

    private void HandlePlanted(FarmTile tile, GameObject who) { Play(library != null ? library.plant : null); }
    private void HandleRipened(FarmTile tile) { Play(library != null ? library.ripe : null); }
    private void HandleHarvested(FarmTile tile, CropData crop) { Play(library != null ? library.harvest : null); }
    private void HandleBell() { Play(library != null ? library.bell : null); }
    private void HandleLanded(Vector3 position, bool hitBoss) { Play(library != null ? library.splat : null); }
    private void HandleFed(float current, float max) { Play(library != null ? library.bossGulp : null); }
    private void HandleScared(float seconds) { Play(library != null ? library.bossScare : null); }
    private void HandleSatisfied() { Play(library != null ? library.burst : null); }
    private void HandleEvolved(BossData next) { Play(library != null ? library.evolve : null); }

    private void HandleFired(CropData crop)
    {
        if (library == null) return;
        Play(crop != null && crop.type == CropType.Shooter ? library.shoot : library.throwCrop);
    }

    private void HandleHealth()
    {
        if (library != null)
        {
            if (health.Hp < lastHp) Play(library.hurt);
            else if (health.ShieldCharges > lastShield) Play(library.shield);
        }
        lastHp = health.Hp;
        lastShield = health.ShieldCharges;
    }

    private void HandleCoins(int coins)
    {
        if (library != null && lastCoins >= 0 && coins > lastCoins) Play(library.coin);
        lastCoins = coins;
    }

    private void HandlePhase(GamePhase phase)
    {
        if (library == null) return;
        switch (phase)
        {
            case GamePhase.Day: SetMusic(library.dayMusic); break;
            case GamePhase.Night: SetMusic(library.nightMusic); break;
            default: SetMusic(null); break; // won, lost, day end: let the music fade out
        }
    }

    // ---------------------------------------------------------------- playback

    private void Play(AudioCue cue)
    {
        if (cue == null || cue.IsEmpty) return;
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(cue, out float last) && now - last < cue.minInterval) return;
        lastPlayed[cue] = now;

        AudioSource src = pool[nextVoice];
        nextVoice = (nextVoice + 1) % pool.Length;
        src.clip = cue.Pick();
        src.volume = cue.volume;
        src.pitch = cue.RandomPitch();
        src.Play();
    }

    private void SetMusic(AudioCue cue)
    {
        if (cue == currentMusic) return;
        currentMusic = cue;
        musicTarget[activeMusic] = 0f; // fade the current bed out
        if (cue == null || cue.IsEmpty) return;

        activeMusic = 1 - activeMusic;
        AudioSource src = music[activeMusic];
        src.clip = cue.Pick();
        src.volume = 0f;
        src.pitch = 1f;
        src.loop = true;
        src.Play();
        musicTarget[activeMusic] = cue.volume * musicVolume;
    }

    private void Update()
    {
        float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, musicFadeSeconds);
        for (int i = 0; i < music.Length; i++)
        {
            AudioSource src = music[i];
            if (!src.isPlaying) continue;
            src.volume = Mathf.MoveTowards(src.volume, musicTarget[i], step);
            if (musicTarget[i] <= 0f && src.volume <= 0f) src.Stop();
        }
    }
}
