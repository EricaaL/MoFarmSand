using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Capture-only helper: plays an ordered list of Cinemachine shots. Shots are data, set in the Inspector.</summary>
public class TrailerDirector : MonoBehaviour
{
    [Serializable]
    public struct Shot
    {
        public CinemachineCamera camera;
        public float seconds;
    }

    [SerializeField] private Shot[] sequence;
    [SerializeField] private CinemachineCamera burstCamera;
    [SerializeField] private float orbitDegreesPerSecond = 12f;
    [SerializeField] private int liveShotPriority = 20;

    private Coroutine running;
    public bool IsPlaying => running != null;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame && !IsPlaying) PlaySequence();
    }

    [ContextMenu("Play Sequence")]
    public void PlaySequence()
    {
        if (running == null) running = StartCoroutine(Run());
    }

    public void CutToBurst(float seconds)
    {
        if (burstCamera == null) return;
        StartCoroutine(Hold(burstCamera, seconds));
    }

    private IEnumerator Run()
    {
        foreach (Shot s in sequence)
        {
            if (s.camera != null) yield return Hold(s.camera, s.seconds);
        }
        running = null;
    }

    private IEnumerator Hold(CinemachineCamera cam, float seconds)
    {
        cam.Priority.Enabled = true;
        cam.Priority.Value = liveShotPriority;
        CinemachineOrbitalFollow orbit = cam.GetComponent<CinemachineOrbitalFollow>();
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            if (orbit != null) orbit.HorizontalAxis.Value += orbitDegreesPerSecond * Time.unscaledDeltaTime;
            yield return null;
        }
        cam.Priority.Value = 0;
    }
}
