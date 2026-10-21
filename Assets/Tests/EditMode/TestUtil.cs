using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Shared helpers for EditMode tests.</summary>
internal static class TestUtil
{
    public static GameDatabase LoadDb()
    {
        GameDatabase db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
        Assert.IsNotNull(db, "Assets/Data/GameDatabase.asset is missing. Run MoFarmSand > Add Run Systems.");
        return db;
    }

    public static T Make<T>() where T : ScriptableObject => ScriptableObject.CreateInstance<T>();

    public static SeasonData Season(string id, int days = 3)
    {
        SeasonData s = Make<SeasonData>();
        s.seasonId = id; s.displayName = id; s.lengthInDays = days;
        return s;
    }

    public static CalendarConfig Calendar(params SeasonData[] seasons)
    {
        CalendarConfig c = Make<CalendarConfig>();
        c.seasons = new List<SeasonData>(seasons);
        return c;
    }
}
