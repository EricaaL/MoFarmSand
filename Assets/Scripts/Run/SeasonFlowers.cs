using UnityEngine;

/// <summary>
/// Shows the scattered wildflowers only while the season asks for them (spring). The group is built in the editor by
/// <c>MoFarmSand ▸ Scatter Spring Flowers</c>; this just toggles it. Presentation only.
/// </summary>
public class SeasonFlowers : MonoBehaviour
{
    [SerializeField] private GameObject flowers;

    private bool? visible;

    public void SetVisible(bool value)
    {
        if (flowers == null || visible == value) return;
        visible = value;
        flowers.SetActive(value);
    }
}
