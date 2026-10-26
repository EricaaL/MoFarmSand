using System;
using System.Collections.Generic;

/// <summary>One pen stroke on the run map. Points are in map units (x from the map's centre line, y from the window's bottom edge
/// at scroll 0, both divided by the UI scale), so a stroke stays on the same spot when the window or scroll changes.</summary>
[Serializable]
public class MapInkStroke
{
    public int color;
    /// <summary>x0, y0, x1, y1, ... flattened so it saves as plain JSON.</summary>
    public List<float> xy = new List<float>();

    public int PointCount => xy.Count / 2;
}

/// <summary>Pure rules for the map pen: adding points, erasing and undo. Strokes live on <see cref="RunState.ink"/>.</summary>
public static class MapInk
{
    /// <summary>Total points kept per run, so a save file can't grow without bound.</summary>
    public const int MaxPoints = 8000;

    public static int TotalPoints(List<MapInkStroke> ink)
    {
        int n = 0;
        foreach (MapInkStroke s in ink) n += s.PointCount;
        return n;
    }

    public static MapInkStroke Begin(List<MapInkStroke> ink, int color, float x, float y)
    {
        var stroke = new MapInkStroke { color = color };
        stroke.xy.Add(x);
        stroke.xy.Add(y);
        ink.Add(stroke);
        return stroke;
    }

    /// <summary>Adds a point unless it is closer than <paramref name="minDist"/> to the last one or the point budget is spent.</summary>
    public static void Extend(List<MapInkStroke> ink, MapInkStroke stroke, float x, float y, float minDist)
    {
        if (TotalPoints(ink) >= MaxPoints) return;
        int n = stroke.xy.Count;
        float dx = x - stroke.xy[n - 2], dy = y - stroke.xy[n - 1];
        if (dx * dx + dy * dy < minDist * minDist) return;
        stroke.xy.Add(x);
        stroke.xy.Add(y);
    }

    public static void Undo(List<MapInkStroke> ink)
    {
        if (ink.Count > 0) ink.RemoveAt(ink.Count - 1);
    }

    /// <summary>Removes every stroke that has a point within <paramref name="radius"/> of (x, y). Returns how many went.</summary>
    public static int EraseAt(List<MapInkStroke> ink, float x, float y, float radius)
    {
        float r2 = radius * radius;
        return ink.RemoveAll(s =>
        {
            for (int i = 0; i + 1 < s.xy.Count; i += 2)
            {
                float dx = s.xy[i] - x, dy = s.xy[i + 1] - y;
                if (dx * dx + dy * dy <= r2) return true;
            }
            return false;
        });
    }
}
