using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tuning for the procedural run map. Single owner of lane/path counts and node-type weights.</summary>
[CreateAssetMenu(fileName = "MapGenConfig", menuName = "MoFarmSand/Map/Map Gen Config")]
public class MapGenConfig : ScriptableObject
{
    [Serializable]
    public struct NodeWeight
    {
        public NodeTypeData type;
        public float weight;
    }

    [Serializable]
    public struct LayerBand
    {
        public int fromLayer;
        public int toLayer;
        public List<NodeWeight> weights;
    }

    [Serializable]
    public struct ForcedLayer
    {
        [Tooltip("If true, counts from the last non-boss layer (0 = the layer right before the boss).")]
        public bool fromEnd;
        public int index;
        public NodeTypeData type;
    }

    [Range(3, 9)] public int lanes = 7;
    [Range(2, 10)] public int pathCount = 6;
    public List<LayerBand> bands = new List<LayerBand>();
    public List<ForcedLayer> forcedLayers = new List<ForcedLayer>();
    public NodeTypeData fallbackType;
    public NodeTypeData bossType;
    [Tooltip("The mandatory boss night on the last day of every season except the final one (which uses bossType).")]
    public NodeTypeData seasonBossType;
    public int maxAssignAttempts = 8;
}
