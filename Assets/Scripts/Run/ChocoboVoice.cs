using UnityEngine;

/// <summary>
/// Makes the wild chocobo call out now and then: one "kweh" every <see cref="minGap"/> to <see cref="maxGap"/> seconds.
/// Stops while the game is paused (time scale 0), including the event screen that freezes the world. Volume follows the Options SFX slider.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ChocoboVoice : MonoBehaviour
{
    [SerializeField] private AudioClip kweh;
    [SerializeField] private float minGap = 10f;
    [SerializeField] private float maxGap = 20f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    private AudioSource source;
    private float timeLeft;
    private bool paused;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 4f;
        source.maxDistance = 40f;
        timeLeft = NextGap();
    }

    private void Update()
    {
        if (kweh == null) return;
        // Paused (menus, shop, events freeze the world): hold the timer and silence a call already in the air.
        if (Time.timeScale <= 0f)
        {
            if (!paused) { source.Pause(); paused = true; }
            return;
        }
        if (paused) { source.UnPause(); paused = false; }
        timeLeft -= Time.deltaTime;
        if (timeLeft > 0f) return;
        timeLeft = NextGap();
        source.volume = volume * GameSettings.OutputSfxVolume;
        source.PlayOneShot(kweh);
    }

    private float NextGap() { return Random.Range(Mathf.Min(minGap, maxGap), Mathf.Max(minGap, maxGap)); }
}
