using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The text of the Credits tab: sections of name / detail lines. Edit it in the Inspector.</summary>
[CreateAssetMenu(fileName = "Credits", menuName = "MoFarmSand/Credits")]
public class CreditsData : ScriptableObject
{
    [Serializable]
    public class Line
    {
        public string name;
        [TextArea(1, 3)] public string detail;
    }

    [Serializable]
    public class Section
    {
        public string heading;
        public List<Line> lines = new List<Line>();
    }

    public List<Section> sections = new List<Section>();
}
