using UnityEngine;

/// <summary>Pure slice math for a pie menu. Slice 0 is centred at the top, the rest follow clockwise.</summary>
public static class PieMenuMath
{
    /// <summary>Slice under a screen-space offset from the wheel centre (y grows downward), or -1 inside the dead zone.</summary>
    public static int SliceAt(Vector2 offset, int count, float deadZone)
    {
        if (count <= 0 || offset.magnitude < deadZone) return -1;
        float angle = Mathf.Atan2(offset.x, -offset.y) * Mathf.Rad2Deg; // 0 = up, clockwise positive
        float width = 360f / count;
        float shifted = Mathf.Repeat(angle + width * 0.5f, 360f);
        return Mathf.Min(count - 1, (int)(shifted / width));
    }

    /// <summary>Angle of the slice's middle in degrees, 0 = up, clockwise.</summary>
    public static float SliceCenterAngle(int index, int count) => count <= 0 ? 0f : index * (360f / count);

    /// <summary>Screen-space unit direction (y down) of the slice's middle.</summary>
    public static Vector2 SliceDirection(int index, int count)
    {
        float a = SliceCenterAngle(index, count) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
    }
}
