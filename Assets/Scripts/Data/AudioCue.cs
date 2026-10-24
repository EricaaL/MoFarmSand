using UnityEngine;

/// <summary>One sound: a few clips to pick from, with volume and pitch variation. An empty cue is silent, so it is safe to leave unassigned.</summary>
[CreateAssetMenu(fileName = "Cue_New", menuName = "MoFarmSand/Audio Cue")]
public class AudioCue : ScriptableObject
{
    [Tooltip("One is picked at random each time. Leave empty for silence.")]
    public AudioClip[] clips = new AudioClip[0];
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Random pitch range per play, so repeated sounds don't sound identical.")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [Tooltip("The cue can't replay faster than this (stops rapid-fire peas from turning into a buzz).")]
    public float minInterval = 0.05f;
    [Tooltip("Plays only the first N seconds of the clip (0 = the whole clip).")]
    public float maxSeconds;
    [Tooltip("Looping cues are music beds.")]
    public bool loop;

    public bool IsEmpty
    {
        get
        {
            if (clips == null) return true;
            for (int i = 0; i < clips.Length; i++) if (clips[i] != null) return false;
            return true;
        }
    }

    public bool Contains(AudioClip clip)
    {
        if (clip == null || clips == null) return false;
        for (int i = 0; i < clips.Length; i++) if (clips[i] == clip) return true;
        return false;
    }

    public AudioClip Pick()
    {
        if (IsEmpty) return null;
        AudioClip clip;
        do { clip = clips[Random.Range(0, clips.Length)]; } while (clip == null);
        return clip;
    }

    public float RandomPitch() => Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));

    private void OnValidate()
    {
        minInterval = Mathf.Max(0f, minInterval);
        maxSeconds = Mathf.Max(0f, maxSeconds);
        pitchRange = new Vector2(Mathf.Max(0.1f, pitchRange.x), Mathf.Max(0.1f, pitchRange.y));
    }
}
