using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A procedural disaster: when it can roll, how strong, and which effects it composes.</summary>
[CreateAssetMenu(fileName = "Disaster_New", menuName = "MoFarmSand/Disaster Data")]
public class DisasterData : ScriptableObject, IHasId
{
    [Serializable]
    public struct SeasonWeight
    {
        public SeasonData season;
        public float weight;
    }

    public string disasterId;
    public string displayName;
    public string bannerText = "A disaster strikes!";
    public Color uiColor = Color.red;

    [Header("Rolling")]
    public List<SeasonWeight> seasonWeights = new List<SeasonWeight>();
    [Tooltip("Earliest run day this can appear.")]
    public int minDay;
    public Vector2 severityRange = new Vector2(0.4f, 1f);

    [Header("Behavior")]
    [Tooltip("Keep ticking during the night boss fight.")]
    public bool activeAtNight;
    public List<DisasterEffect> effects = new List<DisasterEffect>();

    public string Id => disasterId;

    public float WeightFor(SeasonData season)
    {
        for (int i = 0; i < seasonWeights.Count; i++)
            if (seasonWeights[i].season == season) return Mathf.Max(0f, seasonWeights[i].weight);
        return 0f;
    }
}
