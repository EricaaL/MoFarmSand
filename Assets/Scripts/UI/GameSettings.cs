using System.Collections.Generic;
using UnityEngine;

/// <summary>Player options that outlive a run (master volume, tips). UI size lives in <see cref="ImGuiKit.UserScale"/>. Saved in PlayerPrefs.</summary>
public static class GameSettings
{
    private const string VolumeKey = "audio.masterVolume";
    private const string TipsEnabledKey = "tips.enabled";
    private const string TipsSeenKey = "tips.seen";

    /// <summary>0..1 multiplier on all audio, applied through <see cref="AudioListener.volume"/>.</summary>
    public static float MasterVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        set
        {
            float v = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, v);
            AudioListener.volume = v;
        }
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply() { AudioListener.volume = MasterVolume; }
}
