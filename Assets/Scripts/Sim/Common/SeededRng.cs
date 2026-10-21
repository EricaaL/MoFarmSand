using System;
using System.Collections.Generic;

/// <summary>
/// Deterministic SplitMix64 generator. Pure C#: no UnityEngine.Random, so results are stable across
/// platforms and every roll can be re-derived from (runSeed, salt, stream) without saving RNG state.
/// </summary>
public sealed class SeededRng
{
    private ulong state;

    public SeededRng(int seed) { state = Mix((ulong)(uint)seed + 0x9E3779B97F4A7C15UL); }

    private SeededRng(ulong raw) { state = raw; }

    /// <summary>Independent stream for a decision, e.g. Derive(runSeed, nodeId, RngStreams.Disaster).</summary>
    public static SeededRng Derive(int runSeed, int salt, int stream)
    {
        ulong h = Mix((ulong)(uint)runSeed);
        h = Mix(h ^ ((ulong)(uint)salt * 0xBF58476D1CE4E5B9UL));
        h = Mix(h ^ ((ulong)(uint)stream * 0x94D049BB133111EBUL));
        return new SeededRng(h);
    }

    private static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public ulong NextULong()
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform float in [0,1).</summary>
    public float NextFloat() => (NextULong() >> 40) / (float)(1UL << 24);

    /// <summary>Uniform int in [min, max). Returns min if max &lt;= min.</summary>
    public int NextInt(int min, int max)
    {
        if (max <= min) return min;
        return min + (int)(NextULong() % (ulong)(max - min));
    }

    /// <summary>Uniform float in [a, b].</summary>
    public float Range(float a, float b) => a + (b - a) * NextFloat();

    /// <summary>Roughly normal (sum of 3 uniforms), mean 0, in [-1,1]. Cheap noise for weather.</summary>
    public float NextSigned() => (NextFloat() + NextFloat() + NextFloat()) * (2f / 3f) - 1f;

    /// <summary>Weighted pick. Items with weight &lt;= 0 are never chosen. Returns default if none qualify.</summary>
    public T PickWeighted<T>(IReadOnlyList<T> items, Func<T, float> weight)
    {
        float total = 0f;
        for (int i = 0; i < items.Count; i++) total += Math.Max(0f, weight(items[i]));
        if (total <= 0f) return default;
        float roll = NextFloat() * total;
        T last = default;
        for (int i = 0; i < items.Count; i++)
        {
            float w = Math.Max(0f, weight(items[i]));
            if (w <= 0f) continue;
            last = items[i];
            if (roll < w) return items[i];
            roll -= w;
        }
        return last;
    }

    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = NextInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
