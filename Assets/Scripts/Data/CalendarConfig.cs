using System.Collections.Generic;
using UnityEngine;

/// <summary>Ordered seasons. Single owner of run length: map layers are derived from <see cref="TotalDays"/>.</summary>
[CreateAssetMenu(fileName = "CalendarConfig", menuName = "MoFarmSand/Calendar Config")]
public class CalendarConfig : ScriptableObject
{
    public List<SeasonData> seasons = new List<SeasonData>();

    public int TotalDays
    {
        get
        {
            int total = 0;
            for (int i = 0; i < seasons.Count; i++) if (seasons[i] != null) total += Mathf.Max(1, seasons[i].lengthInDays);
            return total;
        }
    }
}
