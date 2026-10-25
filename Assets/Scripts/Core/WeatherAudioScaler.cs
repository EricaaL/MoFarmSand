using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies the Options "Weather" volume to COZY's weather sounds (wind, rain, ambience loops and thunder strikes).
/// COZY owns those AudioSources and rewrites their volume itself, so each frame this remembers the volume COZY last set
/// and writes that times the slider on top; a new source (a thunder strike) is picked up the same frame it appears.
/// </summary>
public class WeatherAudioScaler
{
    private struct Entry { public float baseVolume, written; }

    private readonly Dictionary<AudioSource, Entry> entries = new Dictionary<AudioSource, Entry>();
    private readonly List<AudioSource> found = new List<AudioSource>();

    public void Apply(Transform root, float multiplier)
    {
        if (root == null) return;
        root.GetComponentsInChildren(true, found);
        foreach (AudioSource src in found)
        {
            float volume = src.volume;
            if (!entries.TryGetValue(src, out Entry e) || !Mathf.Approximately(volume, e.written)) e.baseVolume = volume;   // new, or COZY wrote a fresh value
            src.volume = e.baseVolume * multiplier;
            e.written = src.volume;
            entries[src] = e;
        }
        if (entries.Count > found.Count + 32) Prune();
    }

    private void Prune()
    {
        List<AudioSource> dead = new List<AudioSource>();
        foreach (AudioSource src in entries.Keys) if (src == null) dead.Add(src);
        foreach (AudioSource src in dead) entries.Remove(src);
    }
}
