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
    }

    public string eventId;
    public string title;
    [TextArea] public string body;
    public List<EventOption> options = new List<EventOption>();

    public string Id => eventId;
}
