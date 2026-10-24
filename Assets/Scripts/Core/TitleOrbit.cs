using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// The title screen camera: circles a pivot on the farm like the Minecraft menu panorama. Sits on a CinemachineCamera whose
/// priority is raised above the gameplay camera while a title screen is up (<see cref="SetActive"/>). Presentation only.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class TitleOrbit : MonoBehaviour
{
    [SerializeField] private Transform pivot;
    [SerializeField] private float degreesPerSecond = 4f;
    [SerializeField] private float radius = 16f;
    [Tooltip("Above the barn roof and the treetops, so the circle never passes through them.")]
    [SerializeField] private float height = 13f;
    [Tooltip("How far above the pivot the camera looks, so the horizon sits low in frame.")]
    [SerializeField] private float lookHeight = 1.5f;
    [SerializeField] private int activePriority = 30;

    private CinemachineCamera vcam;
    private float angle;
    private bool active;

    private void Awake()
    {
        vcam = GetComponent<CinemachineCamera>();
        angle = Random.Range(0f, 360f);   // a different side each launch
        vcam.Priority.Enabled = true;
        vcam.Priority.Value = 0;
        Place();
    }

    /// <summary>
    /// Raises the camera above the gameplay one (true) or hands the view back (false, which blends).
    /// <paramref name="cut"/> jumps straight to the orbit instead of blending from the gameplay camera (the first title frame).
    /// </summary>
    public void SetActive(bool on, bool cut = false)
    {
        active = on;
        if (vcam != null) vcam.Priority.Value = on ? activePriority : 0;
        if (!on || !cut) return;
        Place();
        Camera main = Camera.main;
        CinemachineBrain brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
        if (brain != null) brain.ResetState();   // forget the current camera: the next update cuts to the top priority one
    }

    private void LateUpdate()
    {
        if (!active) return;
        angle = Mathf.Repeat(angle + degreesPerSecond * Time.unscaledDeltaTime, 360f);
        Place();
    }

    private void Place()
    {
        Vector3 center = pivot != null ? pivot.position : Vector3.zero;
        float rad = angle * Mathf.Deg2Rad;
        transform.position = center + new Vector3(Mathf.Sin(rad) * radius, height, Mathf.Cos(rad) * radius);
        transform.LookAt(center + Vector3.up * lookHeight);
    }
}
