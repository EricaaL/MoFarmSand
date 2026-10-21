using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Slay-the-Spire style layered map. Pure and seeded: the same (config, layerCount, seed) always
/// gives the same graph. Layer index = calendar day; one extra boss layer is appended at the end.
/// </summary>
public static class MapGenerator
{
    public const int MaxRegenerations = 5;

    /// <summary>Generates a map and re-rolls with seed+1 (up to <see cref="MaxRegenerations"/>) if validation fails.</summary>
    public static MapGraph GenerateValidated(MapGenConfig cfg, int layerCount, NodeTypeRegistry registry, int seed, ICollection<int> seasonBossLayers = null)
    {
        MapGraph last = null;
        for (int attempt = 0; attempt <= MaxRegenerations; attempt++)
        {
            last = Generate(cfg, layerCount, registry, seed + attempt, seasonBossLayers);
            if (MapValidator.Validate(last, cfg, registry).Count == 0) return last;
        }
        Debug.LogWarning($"MapGenerator: no valid map after {MaxRegenerations + 1} attempts (seed {seed}); using the last one.");
        return last;
    }

    /// <param name="seasonBossLayers">Layers (days) that are a mandatory boss night, e.g. the last day of each season but the final one.</param>
    public static MapGraph Generate(MapGenConfig cfg, int layerCount, NodeTypeRegistry registry, int seed, ICollection<int> seasonBossLayers = null)
    {
        SeededRng rng = SeededRng.Derive(seed, 0, RngStreams.Map);
        layerCount = Mathf.Max(2, layerCount);
        int lanes = Mathf.Max(3, cfg.lanes);

        MapGraph g = new MapGraph { seed = seed, lanes = lanes, layerCount = layerCount };
        Dictionary<long, MapNode> byCell = new Dictionary<long, MapNode>();
        // Edges between layer l and l+1 as (fromLane, toLane), used for crossing checks.
        List<List<Vector2Int>> layerEdges = new List<List<Vector2Int>>();
        for (int l = 0; l < layerCount - 1; l++) layerEdges.Add(new List<Vector2Int>());

        MapNode GetOrAdd(int layer, int lane)
        {
            long key = ((long)layer << 16) | (uint)lane;
            if (!byCell.TryGetValue(key, out MapNode n))
            {
                n = new MapNode { id = g.nodes.Count, layer = layer, lane = lane };
                g.nodes.Add(n);
                byCell[key] = n;
            }
            return n;
        }

        // 1. Walk paths from layer 0 to the last non-boss layer.
        int firstLane = -1;
        for (int p = 0; p < Mathf.Max(2, cfg.pathCount); p++)
        {
            int lane = rng.NextInt(0, lanes);
            if (p == 1) { int guard = 0; while (lane == firstLane && guard++ < 32) lane = rng.NextInt(0, lanes); }
            if (p == 0) firstLane = lane;

            MapNode cur = GetOrAdd(0, lane);
            for (int l = 0; l < layerCount - 1; l++)
            {
                int next = ChooseNextLane(cur.lane, lanes, layerEdges[l], rng);
                MapNode child = GetOrAdd(l + 1, next);
                if (!cur.next.Contains(child.id)) { cur.next.Add(child.id); layerEdges[l].Add(new Vector2Int(cur.lane, next)); }
                cur = child;
            }
        }

        // 2. Boss node in its own final layer, linked from every node of the last layer.
        MapNode boss = new MapNode { id = g.nodes.Count, layer = layerCount, lane = lanes / 2, nodeTypeId = cfg.bossType != null ? cfg.bossType.Id : string.Empty };
        g.nodes.Add(boss);
        g.bossId = boss.id;
        foreach (MapNode n in g.NodesInLayer(layerCount - 1)) n.next.Add(boss.id);

        // 3. Node types, layer by layer.
        AssignTypes(g, cfg, rng, seasonBossLayers);
        foreach (MapNode n in g.nodes) n.next.Sort();
        return g;
    }

    private static int ChooseNextLane(int lane, int lanes, List<Vector2Int> edges, SeededRng rng)
    {
        // Existing edges never cross, so staying straight is always a legal fallback.
        List<int> options = new List<int>(3);
        for (int d = -1; d <= 1; d++)
        {
            int to = lane + d;
            if (to < 0 || to >= lanes) continue;
            if (!Crosses(edges, lane, to)) options.Add(to);
        }
        if (options.Count == 0) return lane;
        return options[rng.NextInt(0, options.Count)];
    }

    public static bool Crosses(List<Vector2Int> edges, int from, int to)
    {
        for (int i = 0; i < edges.Count; i++)
        {
            Vector2Int e = edges[i];
            if ((from < e.x && to > e.y) || (from > e.x && to < e.y)) return true;
        }
        return false;
    }

    private static void AssignTypes(MapGraph g, MapGenConfig cfg, SeededRng rng, ICollection<int> seasonBossLayers)
    {
        // Parents lookup and per-node counts of each type along the heaviest path leading to it.
        Dictionary<int, List<MapNode>> parents = new Dictionary<int, List<MapNode>>();
        foreach (MapNode n in g.nodes)
            foreach (int c in n.next)
            {
                if (!parents.TryGetValue(c, out List<MapNode> list)) parents[c] = list = new List<MapNode>();
                list.Add(n);
            }
        Dictionary<int, Dictionary<string, int>> pathCounts = new Dictionary<int, Dictionary<string, int>>();

        for (int layer = 0; layer < g.layerCount; layer++)
        {
            List<MapNode> row = g.NodesInLayer(layer);
            NodeTypeData forced = ForcedFor(cfg, layer, g.layerCount, seasonBossLayers);

            foreach (MapNode node in row)
            {
                List<MapNode> myParents = parents.TryGetValue(node.id, out List<MapNode> ps) ? ps : new List<MapNode>();
                NodeTypeData chosen = forced;

                if (chosen == null)
                {
                    List<MapGenConfig.NodeWeight> pool = BandFor(cfg, layer);
                    for (int attempt = 0; attempt < Mathf.Max(1, cfg.maxAssignAttempts) && chosen == null; attempt++)
                    {
                        MapGenConfig.NodeWeight pick = rng.PickWeighted(pool, w => w.type != null ? w.weight : 0f);
                        NodeTypeData t = pick.type;
                        if (t != null && IsAllowed(t, layer, myParents, pathCounts, g, row, node, attempt < cfg.maxAssignAttempts / 2))
                            chosen = t;
                    }
                    if (chosen == null) chosen = cfg.fallbackType;
                }

                node.nodeTypeId = chosen != null ? chosen.Id : string.Empty;

                Dictionary<string, int> counts = new Dictionary<string, int>();
                foreach (MapNode p in myParents)
                {
                    if (!pathCounts.TryGetValue(p.id, out Dictionary<string, int> pc)) continue;
                    foreach (KeyValuePair<string, int> kv in pc)
                        if (!counts.TryGetValue(kv.Key, out int cur) || kv.Value > cur) counts[kv.Key] = kv.Value;
                }
                if (chosen != null) counts[chosen.Id] = (counts.TryGetValue(chosen.Id, out int c0) ? c0 : 0) + 1;
                pathCounts[node.id] = counts;
            }
        }
    }

    private static bool IsAllowed(NodeTypeData t, int layer, List<MapNode> parents,
        Dictionary<int, Dictionary<string, int>> pathCounts, MapGraph g, List<MapNode> row, MapNode self, bool strictSiblings)
    {
        if (layer < t.minLayer) return false;

        if (t.noConsecutive)
            foreach (MapNode p in parents) if (p.nodeTypeId == t.Id) return false;

        if (t.maxPerPath > 0)
            foreach (MapNode p in parents)
                if (pathCounts.TryGetValue(p.id, out Dictionary<string, int> pc) && pc.TryGetValue(t.Id, out int n) && n >= t.maxPerPath)
                    return false;

        if (strictSiblings)
        {
            // Children of a common parent should differ where possible.
            foreach (MapNode other in row)
            {
                if (other == self || string.IsNullOrEmpty(other.nodeTypeId) || other.nodeTypeId != t.Id) continue;
                foreach (MapNode p in parents) if (p.next.Contains(other.id)) return false;
            }
        }
        return true;
    }

    private static NodeTypeData ForcedFor(MapGenConfig cfg, int layer, int layerCount, ICollection<int> seasonBossLayers)
    {
        if (layer == layerCount) return cfg.bossType;
        if (cfg.seasonBossType != null && seasonBossLayers != null && seasonBossLayers.Contains(layer)) return cfg.seasonBossType;
        for (int i = 0; i < cfg.forcedLayers.Count; i++)
        {
            MapGenConfig.ForcedLayer f = cfg.forcedLayers[i];
            int target = f.fromEnd ? (layerCount - 1) - f.index : f.index;
            if (target == layer) return f.type;
        }
        return null;
    }

    private static List<MapGenConfig.NodeWeight> BandFor(MapGenConfig cfg, int layer)
    {
        for (int i = 0; i < cfg.bands.Count; i++)
            if (layer >= cfg.bands[i].fromLayer && layer <= cfg.bands[i].toLayer && cfg.bands[i].weights != null)
                return cfg.bands[i].weights;
        return new List<MapGenConfig.NodeWeight>();
    }
}
