using UnityEngine;

/// <summary>Pure ballistic math: r(t) = p0 + v0 t + 1/2 g t^2. Single source for arc preview and projectile.</summary>
public static class Ballistics
{
    public static Vector3 SolveLaunchVelocity(Vector3 p0, Vector3 target, float flightTime, Vector3 gravity)
    {
        flightTime = Mathf.Max(0.05f, flightTime);
        return (target - p0 - 0.5f * gravity * flightTime * flightTime) / flightTime;
    }

    public static Vector3 Evaluate(Vector3 p0, Vector3 v0, Vector3 gravity, float t)
    {
        return p0 + v0 * t + 0.5f * gravity * t * t;
    }
}
