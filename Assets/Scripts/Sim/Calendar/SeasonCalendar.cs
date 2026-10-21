using System.Collections.Generic;
using UnityEngine;

public struct CalendarDate
{
    public int dayIndex;
    public int seasonIndex;
    public SeasonData season;
    public int dayInSeason;
    public int daysInSeason;
    public float seasonProgress01;
    public float year01;

    public bool IsLastDayOfSeason => dayInSeason == daysInSeason - 1;
}

/// <summary>Pure mapping from a run day index to season and date, built from <see cref="CalendarConfig"/>.</summary>
public sealed class SeasonCalendar
{
    private readonly CalendarConfig config;

    public SeasonCalendar(CalendarConfig config) { this.config = config; }

    public int TotalDays => config.TotalDays;

    /// <summary>Day indexes beyond the end clamp to the last day.</summary>
    public CalendarDate DateOf(int dayIndex)
    {
        int total = TotalDays;
        dayIndex = Mathf.Clamp(dayIndex, 0, Mathf.Max(0, total - 1));
        int remaining = dayIndex;
        CalendarDate d = new CalendarDate { dayIndex = dayIndex };
        for (int i = 0; i < config.seasons.Count; i++)
        {
            SeasonData s = config.seasons[i];
            if (s == null) continue;
            int len = Mathf.Max(1, s.lengthInDays);
            if (remaining < len)
            {
                d.seasonIndex = i;
                d.season = s;
                d.dayInSeason = remaining;
                d.daysInSeason = len;
                d.seasonProgress01 = len <= 1 ? 0f : remaining / (float)(len - 1);
                d.year01 = total <= 0 ? 0f : dayIndex / (float)total;
                return d;
            }
            remaining -= len;
        }
        return d;
    }

    /// <summary>Layers of the run map: every day but the last, because the final boss is appended as the last day.</summary>
    public int MapLayerCount => Mathf.Max(2, TotalDays - 1);

    /// <summary>Days that are a mandatory season boss: the last day of every season except the final one (that is the final boss).</summary>
    public List<int> SeasonBossDays()
    {
        List<int> days = new List<int>();
        for (int d = 0; d < TotalDays - 1; d++) if (IsLastDayOfSeason(d)) days.Add(d);
        return days;
    }

    public bool IsLastDayOfSeason(int dayIndex) => DateOf(dayIndex).IsLastDayOfSeason;

    /// <summary>True when the day is the final day of the whole calendar (the boss follows).</summary>
    public bool IsLastDayOfYear(int dayIndex) => dayIndex >= TotalDays - 1;
}
