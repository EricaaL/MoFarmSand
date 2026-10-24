using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the game's sounds from the events it already raises, and crossfades the day and night music. Presentation only:
/// a cue with no clips is silent, so the game runs fine before any audio has been dropped in. Music and sound effects each have their own
/// Options volume (<see cref="GameSettings.MusicVolume"/>, <see cref="GameSettings.SfxVolume"/>), applied live.
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
    [SerializeField] private float footstepFadeSeconds = 0.08f;

    private PlayerDeath death;
    private CharacterController playerController;
    private AudioSource footstepSource;
    private float footstepTargetVolume;
    private const float TrimFadeSeconds = 0.1f;
    private AudioSource[] pool;
    private float[] voiceStopAt;
    private float[] voiceVolume;
    private int nextVoice;
    private int aimVoice = -1; // voice playing the aim sound, so a throw can cut it short
    private readonly Dictionary<AudioCue, float> lastPlayed = new Dictionary<AudioCue, float>();

    private AudioSource[] music;
    private readonly float[] musicTarget = new float[2];   // loudness of each bed at full fade (cue volume x mix), 0 = fading out
    private readonly float[] musicBase = new float[2];    // cue volume x the director's musicVolume mix
    private readonly float[] musicLevel = new float[2];    // fade 0..1 toward in/out; the Options volume is applied on top, so the slider is instant
    private readonly float[] musicFade = new float[2];     // seconds each bed takes to fade in or out (a cue's fadeInSeconds overrides the default)
    private int activeMusic;
    private AudioCue currentMusic;

    private int lastHp = -1;
    private int lastShield;

    private void Awake()
    {
        pool = new AudioSource[Mathf.Max(1, voices)];
        voiceStopAt = new float[pool.Length];
        voiceVolume = new float[pool.Length];
        for (int i = 0; i < voiceStopAt.Length; i++) voiceStopAt[i] = float.MaxValue;
        for (int i = 0; i < pool.Length; i++) pool[i] = NewSource("Voice " + i, false);
        music = new[] { NewSource("Music A", true), NewSource("Music B", true) };
        musicFade[0] = musicFade[1] = musicFadeSeconds;
        footstepSource = NewSource("Footsteps", true);
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
        FarmTile.GrewToSecondStage += HandleGrew;
        FarmTile.Ripened += HandleRipened;
        FarmTile.Harvested += HandleHarvested;
        PlayerCombat.Fired += HandleFired;
        PlayerCombat.AimStarted += HandleAimStarted;
        PlayerCombat.Ate += HandleAte;
        RunDirector.ShopOpened += HandleShopOpen;
        TipsView.Shown += HandleTip;
        CropProjectile.Landed += HandleLanded;
        BellTrigger.Rung += HandleBell;
        FarmTile.Withered += HandleWithered;
        ImGuiKit.Clicked += HandleButton;
        ImGuiKit.ClickedSmall += HandleButtonSmall;
        ImGuiKit.ClickedTab += HandleTab;
        ImGuiKit.ClickedSelect += HandleSelect;
        ImGuiKit.ClickedShopSelect += HandleShopSelect;
        PlayerInventory.SlotSelected += HandleSelect;
        RunDirector.Bought += HandleBought;
        RunDirector.Sold += HandleSold;
        RunDirector.BuyRefused += HandleBuyRefused;
        RunDirector.CoinsMoved += HandleCoinsMoved;
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
            death = health.GetComponent<PlayerDeath>();
            if (death != null) death.Died += HandleDied;
        }
    }

    private void Start()
    {
        if (health == null) health = Object.FindFirstObjectByType<PlayerHealth>();
        if (health != null)
        {
            if (death == null) death = health.GetComponent<PlayerDeath>();
            if (playerController == null) playerController = health.GetComponent<CharacterController>();
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPhaseChanged += HandlePhase;
            GameManager.Instance.OnVoidFall += HandleVoidFall;
        }
        if (library != null) SetMusic(library.dayMusic);
    }

    private void OnDisable()
    {
        FarmTile.Planted -= HandlePlanted;
        FarmTile.GrewToSecondStage -= HandleGrew;
        FarmTile.Ripened -= HandleRipened;
        FarmTile.Harvested -= HandleHarvested;
        PlayerCombat.Fired -= HandleFired;
        PlayerCombat.AimStarted -= HandleAimStarted;
        PlayerCombat.Ate -= HandleAte;
        RunDirector.ShopOpened -= HandleShopOpen;
        TipsView.Shown -= HandleTip;
        CropProjectile.Landed -= HandleLanded;
        BellTrigger.Rung -= HandleBell;
        FarmTile.Withered -= HandleWithered;
        ImGuiKit.Clicked -= HandleButton;
        ImGuiKit.ClickedSmall -= HandleButtonSmall;
        ImGuiKit.ClickedTab -= HandleTab;
        ImGuiKit.ClickedSelect -= HandleSelect;
        ImGuiKit.ClickedShopSelect -= HandleShopSelect;
        PlayerInventory.SlotSelected -= HandleSelect;
        RunDirector.Bought -= HandleBought;
        RunDirector.Sold -= HandleSold;
        RunDirector.BuyRefused -= HandleBuyRefused;
        RunDirector.CoinsMoved -= HandleCoinsMoved;
        if (death != null) death.Died -= HandleDied;
        if (hunger != null)
        {
            hunger.OnBossFed -= HandleFed;
            hunger.OnScared -= HandleScared;
            hunger.OnBossSatisfied -= HandleSatisfied;
            hunger.OnBossEvolved -= HandleEvolved;
        }
        if (health != null) health.OnChanged -= HandleHealth;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPhaseChanged -= HandlePhase;
            GameManager.Instance.OnVoidFall -= HandleVoidFall;
        }
    }

    // ---------------------------------------------------------------- handlers

    private void HandlePlanted(FarmTile tile, GameObject who) { Play(library != null ? library.plant : null); }
    private void HandleGrew(FarmTile tile) { Play(library != null ? library.grow : null); }
    private void HandleRipened(FarmTile tile) { Play(library != null ? library.ripe : null); }
    private void HandleHarvested(FarmTile tile, CropData crop) { Play(library != null ? library.harvest : null); }
    private void HandleBell() { Play(library != null ? library.bell : null); }
    private void HandleButton() { Play(library != null ? library.button : null); }
    private void HandleButtonSmall() { Play(library != null ? library.buttonSmall : null); }
    private void HandleAimStarted() { aimVoice = Play(library != null ? library.aimCrop : null); }
    private void HandleAte() { Play(library != null ? library.eat : null); }
    private void HandleShopOpen() { Play(library != null ? library.shopOpen : null); }
    private void HandleTip() { Play(library != null ? library.tip : null); }
    private void HandleSelect() { Play(library != null ? library.inventorySelect : null); }
    private void HandleShopSelect() { Play(library != null ? library.shopSelect : null); }
    private void HandleTab() { Play(library != null ? library.tabSwitch : null); }
    private void HandleWithered(FarmTile tile) { Play(library != null ? library.wither : null); }
    private void HandleDied() { Play(library != null ? library.death : null); }
    private void HandleVoidFall(bool endsDay) { Play(library != null ? library.fall : null); }
    private void HandleBought() { Play(library != null ? library.crowBuy : null); }
    private void HandleSold() { Play(library != null ? library.crowSell : null); }
    private void HandleBuyRefused() { Play(library != null ? library.crowNo : null); }
    private void HandleLanded(Vector3 position, bool hitBoss) { Play(library != null ? library.splat : null); }
    private void HandleFed(float current, float max) { Play(library != null ? library.bossGulp : null); }
    private void HandleScared(float seconds) { Play(library != null ? library.bossScare : null); }
    private void HandleSatisfied()
    {
        if (library == null) return;
        Play(library.burst);
        Play(library.victory);
    }
    private void HandleEvolved(BossData next)
    {
        if (library == null) return;
        Play(library.evolve);
        // Slowly bring in the butterfly theme over the night music (stays on the night music if none is set).
        if (library.butterflyMusic != null && !library.butterflyMusic.IsEmpty) SetMusic(library.butterflyMusic);
    }

    private void HandleFired(CropData crop)
    {
        if (library == null) return;
        StopAimSound();
        Play(crop != null && crop.type == CropType.Shooter ? library.shoot : library.throwCrop);
    }

    private void HandleHealth()
    {
        if (library != null)
        {
            if (health.Hp < lastHp) Play(library.hurt);
            else if (health.ShieldCharges > lastShield) Play(library.shield);
            else if (health.ShieldCharges < lastShield) Play(library.shieldBreak);
        }
        lastHp = health.Hp;
        lastShield = health.ShieldCharges;
    }

    private void HandleCoinsMoved() { Play(library != null ? library.coin : null); }

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

    private void StopAimSound()
    {
        if (aimVoice < 0) return;
        // Only stop it if that voice still holds an aim clip (it may have been reused by another sound since).
        if (library != null && library.aimCrop != null && pool[aimVoice].isPlaying && library.aimCrop.Contains(pool[aimVoice].clip)) { pool[aimVoice].Stop(); voiceStopAt[aimVoice] = float.MaxValue; }
        aimVoice = -1;
    }

    /// <summary>Plays the cue on the next free voice. Returns the voice index, or -1 if nothing played.</summary>
    private int Play(AudioCue cue)
    {
        if (cue == null || cue.IsEmpty) return -1;
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(cue, out float last) && now - last < cue.minInterval) return -1;
        lastPlayed[cue] = now;

        AudioSource src = pool[nextVoice];
        nextVoice = (nextVoice + 1) % pool.Length;
        src.clip = cue.Pick();
        src.volume = cue.volume * GameSettings.SfxVolume;
        src.pitch = cue.RandomPitch();
        src.Play();
        int voice = (nextVoice + pool.Length - 1) % pool.Length;
        voiceStopAt[voice] = cue.maxSeconds > 0f ? now + cue.maxSeconds / src.pitch : float.MaxValue;
        voiceVolume[voice] = cue.volume;
        src.volume = cue.volume * GameSettings.SfxVolume;
        return voice;
    }

    private void SetMusic(AudioCue cue)
    {
        if (cue == currentMusic) return;
        currentMusic = cue;
        float fade = cue != null && cue.fadeInSeconds > 0f ? cue.fadeInSeconds : musicFadeSeconds;
        musicTarget[activeMusic] = 0f; // fade the current bed out (as slowly as the new one comes in, so they crossfade)
        musicFade[activeMusic] = fade;
        if (cue == null || cue.IsEmpty) return;

        activeMusic = 1 - activeMusic;
        musicFade[activeMusic] = fade;
        AudioSource src = music[activeMusic];
        src.clip = cue.Pick();
        src.volume = 0f;
        musicLevel[activeMusic] = 0f;
        src.pitch = 1f;
        src.loop = true;
        src.Play();
        musicBase[activeMusic] = cue.volume * musicVolume;
        musicTarget[activeMusic] = musicBase[activeMusic];
    }

    private void Update()
    {
        // Trimmed cues (maxSeconds) fade out over their last moments, then stop. The SFX volume is applied live.
        float nowUnscaled = Time.unscaledTime;
        float sfx = GameSettings.SfxVolume;
        for (int i = 0; i < pool.Length; i++)
        {
            if (!pool[i].isPlaying) continue;
            float fade = 1f;
            if (voiceStopAt[i] != float.MaxValue)
            {
                float left = voiceStopAt[i] - nowUnscaled;
                if (left <= 0f) { pool[i].Stop(); voiceStopAt[i] = float.MaxValue; continue; }
                if (left < TrimFadeSeconds) fade = left / TrimFadeSeconds;
            }
            pool[i].volume = voiceVolume[i] * sfx * fade;
        }

        float musicMix = GameSettings.MusicVolume;
        for (int i = 0; i < music.Length; i++)
        {
            AudioSource src = music[i];
            if (!src.isPlaying) continue;
            float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, musicFade[i]);
            musicLevel[i] = Mathf.MoveTowards(musicLevel[i], musicTarget[i] > 0f ? 1f : 0f, step);
            src.volume = musicLevel[i] * musicBase[i] * musicMix;
            if (musicTarget[i] <= 0f && musicLevel[i] <= 0f) src.Stop();
        }

        UpdateFootsteps();
    }

    private void UpdateFootsteps()
    {
        if (footstepSource == null || library == null || library.footstep == null || library.footstep.IsEmpty)
        {
            if (footstepSource != null && footstepSource.isPlaying) footstepSource.Stop();
            return;
        }

        if (playerController == null)
        {
            if (health != null) playerController = health.GetComponent<CharacterController>();
            else
            {
                PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
                if (pc != null)
                {
                    playerController = pc.GetComponent<CharacterController>();
                    if (health == null) health = pc.GetComponent<PlayerHealth>();
                    if (death == null) death = pc.GetComponent<PlayerDeath>();
                }
            }
        }

        bool isDead = death != null && death.IsDead;
        bool isMoving = false;
        float speed = 0f;

        if (playerController != null && !isDead && Time.timeScale > 0f)
        {
            Vector3 v = playerController.velocity;
            v.y = 0f;
            speed = v.magnitude;
            isMoving = playerController.isGrounded && speed > 0.3f;
        }

        if (isMoving)
        {
            AudioCue cue = library.footstep;
            if (footstepSource.clip == null) footstepSource.clip = cue.Pick();
            if (!footstepSource.isPlaying)
            {
                footstepSource.volume = 0f;
                footstepSource.Play();
            }

            // Normal run is ~5 m/s, walk is ~2.5 m/s. Adjust pitch slightly with movement speed.
            footstepSource.pitch = Mathf.Clamp(speed / 5f, 0.85f, 1.15f);
            footstepTargetVolume = cue.volume * GameSettings.SfxVolume;
        }
        else
        {
            footstepTargetVolume = 0f;
        }

        float step = Time.unscaledDeltaTime / Mathf.Max(0.02f, footstepFadeSeconds);
        footstepSource.volume = Mathf.MoveTowards(footstepSource.volume, footstepTargetVolume, step);
        if (footstepTargetVolume <= 0f && footstepSource.volume <= 0f && footstepSource.isPlaying)
        {
            footstepSource.Stop();
        }
    }
}
