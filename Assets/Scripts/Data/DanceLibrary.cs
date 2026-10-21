using System.Collections.Generic;
using UnityEngine;

/// <summary>The dances on the emote wheel, in slice order (first slice at the top, then clockwise).</summary>
[CreateAssetMenu(fileName = "DanceLibrary", menuName = "MoFarmSand/Dance Library")]
public class DanceLibrary : ScriptableObject
{
    public List<DanceData> dances = new List<DanceData>();
}
