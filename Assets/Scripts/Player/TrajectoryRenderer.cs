using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class TrajectoryRenderer : MonoBehaviour
{
    [SerializeField] private int points = 30;
    [SerializeField] private Transform landingMarker;

    private LineRenderer line;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        Hide();
    }

    public void Show(Vector3 p0, Vector3 v0, Vector3 gravity, float flightTime)
    {
        line.enabled = true;
        line.positionCount = points;
        for (int i = 0; i < points; i++)
        {
            float t = flightTime * i / (points - 1);
            line.SetPosition(i, Ballistics.Evaluate(p0, v0, gravity, t));
        }
        if (landingMarker != null)
        {
            landingMarker.gameObject.SetActive(true);
            landingMarker.position = line.GetPosition(points - 1) + Vector3.up * 0.05f;
        }
    }

    public void Hide()
    {
        if (line != null) line.enabled = false;
        if (landingMarker != null) landingMarker.gameObject.SetActive(false);
    }
}
