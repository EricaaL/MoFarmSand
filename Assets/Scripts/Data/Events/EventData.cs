using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A choice event shown at the start of an Event day.</summary>
[CreateAssetMenu(fileName = "Event_New", menuName = "MoFarmSand/Events/Event Data")]
public class EventData : ScriptableObject, IHasId
{
    [Serializable]
    public struct EventOption
    {
        public string label;
        [Tooltip("Coins the player must pay to pick this option.")]
        public int coinCost;
        public List<RunEffect> effects;
        [Tooltip("Leave the effects' descriptions off the button, for labels that already say it (\"Take 10 coins\").")]
        public bool hideEffects;
    }

    public string eventId;
    public string title;
    [TextArea] public string body;
    public List<EventOption> options = new List<EventOption>();
    [Tooltip("Seasons this event can appear in. Empty = any season.")]
    public List<SeasonData> seasons = new List<SeasonData>();

    public string Id => eventId;

    public bool AllowedIn(SeasonData season) => seasons == null || seasons.Count == 0 || seasons.Contains(season);
}
