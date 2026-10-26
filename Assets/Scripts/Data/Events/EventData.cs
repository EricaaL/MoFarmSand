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

        /// <summary>Coins this option takes: the up-front cost plus any coin-losing effect (a toll, a plate at the feast). The player needs this many to pick it.</summary>
        public int TotalCoinCost
        {
            get
            {
                int total = coinCost;
                if (effects != null)
                    foreach (RunEffect e in effects)
                        if (e is AddCoinsEffect coins && coins.amount < 0) total -= coins.amount;
                return total;
            }
        }
    }

    public string eventId;
    public string title;
    [TextArea] public string body;
    public List<EventOption> options = new List<EventOption>();
    [Tooltip("A bad event: every choice costs something. Trinkets like the Scarecrow make these come up less often.")]
    public bool harmful;
    [Tooltip("Seasons this event can appear in. Empty = any season.")]
    public List<SeasonData> seasons = new List<SeasonData>();

    [Header("Stage prop")]
    [Tooltip("Optional prefab spawned on the farm for this event: it stands behind the event card and stays through the day.")]
    public GameObject stagePrefab;
    public Vector3 stagePosition;
    [Tooltip("Euler angles of the spawned prop.")]
    public Vector3 stageEuler;

    public string Id => eventId;

    public bool AllowedIn(SeasonData season) => seasons == null || seasons.Count == 0 || seasons.Contains(season);
}
