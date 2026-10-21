using System.Collections.Generic;
using UnityEngine;

/// <summary>Structural checks shared by tests, the generator's retry loop and save sanitizing.</summary>
public static class MapValidator
{
    public static List<string> Validate(MapGraph g, MapGenConfig cfg, NodeTypeRegistry registry)
    {
        List<string> errors = new List<string>();
        if (g == null || g.nodes.Count == 0) { errors.Add("map is empty"); return errors; }

        HashSet<int> ids = new HashSet<int>();
        foreach (MapNode n in g.nodes) if (!ids.Add(n.id)) errors.Add($"duplicate node id {n.id}");

        MapNode boss = g.Get(g.bossId);
        if (boss == null) { errors.Add("missing boss node"); return errors; }
        if (boss.layer != g.layerCount) errors.Add("boss is not in the last layer");

        int bossCount = 0;
        foreach (MapNode n in g.nodes) if (n.layer == g.layerCount) bossCount++;
        if (bossCount != 1) errors.Add($"expected 1 node in the boss layer, found {bossCount}");

        // Every node (besides layer 0) needs a parent, and every node (besides the boss) needs a child.
        HashSet<int> hasParent = new HashSet<int>();
        foreach (MapNode n in g.nodes)
            foreach (int c in n.next)
            {
                MapNode child = g.Get(c);
                if (child == null) { errors.Add($"node {n.id} links to missing node {c}"); continue; }
                if (child.layer != n.layer + 1) errors.Add($"edge {n.id}->{c} skips layers");
                hasParent.Add(c);
                if (n.layer < g.layerCount - 1 && Mathf.Abs(child.lane - n.lane) > 1) errors.Add($"edge {n.id}->{c} jumps more than one lane");
            }
        foreach (MapNode n in g.nodes)
        {
            if (n.layer > 0 && !hasParent.Contains(n.id)) errors.Add($"node {n.id} is unreachable");
            if (n.id != g.bossId && n.next.Count == 0) errors.Add($"node {n.id} is a dead end");
        }

        // No crossing edges between adjacent layers.
        for (int l = 0; l < g.layerCount - 1; l++)
        {
            List<Vector2Int> edges = new List<Vector2Int>();
            foreach (MapNode n in g.NodesInLayer(l))
                foreach (int c in n.next)
                {
                    MapNode child = g.Get(c);
                    if (child == null) continue;
                    if (MapGenerator.Crosses(edges, n.lane, child.lane)) errors.Add($"crossing edge at layer {l}");
                    edges.Add(new Vector2Int(n.lane, child.lane));
                }
        }

        // Node type rules.
        if (cfg != null && registry != null)
        {
            foreach (MapNode n in g.nodes)
            {
                if (!registry.TryGet(n.nodeTypeId, out NodeTypeData t)) { errors.Add($"node {n.id} has unknown type '{n.nodeTypeId}'"); continue; }
                if (n.layer != g.layerCount && n.layer < t.minLayer) errors.Add($"node {n.id} ({t.Id}) is before its minLayer");
                if (t.noConsecutive)
                    foreach (int c in n.next)
                    {
                        MapNode child = g.Get(c);
                        if (child != null && child.nodeTypeId == t.Id) errors.Add($"consecutive {t.Id} at {n.id}->{c}");
                    }
            }
        }
        return errors;
    }
}
