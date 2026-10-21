using System;
using System.Collections.Generic;

[Serializable]
public class MapNode
{
    public int id;
    public int layer;
    public int lane;
    public string nodeTypeId;
    public List<int> next = new List<int>();
}

/// <summary>The run map. Directly JsonUtility-serializable: this is also the saved form.</summary>
[Serializable]
public class MapGraph
{
    public int seed;
    public int lanes;
    public int layerCount;
    public int bossId = -1;
    public List<MapNode> nodes = new List<MapNode>();

    public MapNode Get(int id)
    {
        for (int i = 0; i < nodes.Count; i++) if (nodes[i].id == id) return nodes[i];
        return null;
    }

    public List<MapNode> NodesInLayer(int layer)
    {
        List<MapNode> list = new List<MapNode>();
        for (int i = 0; i < nodes.Count; i++) if (nodes[i].layer == layer) list.Add(nodes[i]);
        list.Sort((a, b) => a.lane.CompareTo(b.lane));
        return list;
    }

    public bool IsReachable(int fromId, int toId)
    {
        if (fromId == toId) return true;
        HashSet<int> seen = new HashSet<int>();
        Stack<int> stack = new Stack<int>();
        stack.Push(fromId);
        while (stack.Count > 0)
        {
            MapNode n = Get(stack.Pop());
            if (n == null) continue;
            for (int i = 0; i < n.next.Count; i++)
            {
                int c = n.next[i];
                if (c == toId) return true;
                if (seen.Add(c)) stack.Push(c);
            }
        }
        return false;
    }
}
