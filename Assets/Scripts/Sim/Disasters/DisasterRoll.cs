using System;

[Serializable]
public struct DisasterRoll
{
    public string disasterId;
    [UnityEngine.Range(0f, 1f)] public float severity01;

    public bool IsNone => string.IsNullOrEmpty(disasterId);
    public static DisasterRoll None => default;
}
