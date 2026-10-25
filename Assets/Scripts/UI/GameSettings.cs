using System.Collections.Generic;
using UnityEngine;

/// <summary>Player options that outlive a run (master, music and sound-effect volume, tips). UI size lives in <see cref="ImGuiKit.UserScale"/>. Saved in PlayerPrefs.</summary>
public static class GameSettings
{
    private const string OldVolumeKey = "audio.masterVolume";
    private const string MasterKey = "audio.master";
    private const string MusicKey = "audio.musicVolume";
    private const string SfxKey = "audio.sfxVolume";
    private const string WeatherKey = "audio.weatherVolume";
    private const string TipsEnabledKey = "tips.enabled";
    private const string TipsSeenKey = "tips.seen";

    /// <summary>0..1 multiplier on everything audible, on top of the music and SFX sliders.</summary>
    public static float MasterVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
        set => PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(value));
    }

    /// <summary>What the audio actually plays at: the music slider times master.</summary>
    public static float OutputMusicVolume => MusicVolume * MasterVolume;

    /// <summary>What the audio actually plays at: the SFX slider times master.</summary>
    public static float OutputSfxVolume => SfxVolume * MasterVolume;

    /// <summary>0..1 multiplier on the day and night music. Starts from the old single "Sound" setting if there was one.</summary>
    public static float MusicVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, PlayerPrefs.GetFloat(OldVolumeKey, 1f)));
        set => PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
    }

    /// <summary>0..1 multiplier on every sound effect (everything that is not music).</summary>
    public static float SfxVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, PlayerPrefs.GetFloat(OldVolumeKey, 1f)));
        set => PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
    }

    /// <summary>What COZY's weather sounds (wind, rain, thunder, ambience) play at: the weather slider times master.</summary>
    public static float OutputWeatherVolume => WeatherVolume * MasterVolume;

    /// <summary>0..1 multiplier on the weather sounds.</summary>
    public static float WeatherVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(WeatherKey, 1f));
        set => PlayerPrefs.SetFloat(WeatherKey, Mathf.Clamp01(value));
    }

    /// <summary>Whether the in-play tip cards show at all.</summary>
    public static bool TipsEnabled
    {
        get => PlayerPrefs.GetInt(TipsEnabledKey, 1) != 0;
        set => PlayerPrefs.SetInt(TipsEnabledKey, value ? 1 : 0);
    }

    /// <summary>Ids of the tips already shown, so each one appears once per machine until reset.</summary>
    public static IEnumerable<string> SeenTips
    {
        get
        {
            string raw = PlayerPrefs.GetString(TipsSeenKey, string.Empty);
            return raw.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        }
    }

    public static void SaveSeenTips(IEnumerable<string> ids) { PlayerPrefs.SetString(TipsSeenKey, string.Join(",", ids)); }

    /// <summary>Bumped by <see cref="ResetTips"/>; live tip views compare it to know when to forget what they showed.</summary>
    public static int TipsResetCount { get; private set; }

    /// <summary>Makes every tip show again (e.g. between showcase players).</summary>
    public static void ResetTips()
    {
        PlayerPrefs.DeleteKey(TipsSeenKey);
        TipsResetCount++;
    }
}
