using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RailTrip3D
{
    public enum TileKind { Rail = 0, Station = 1, Property = 2, Card = 3, Bonus = 4, Penalty = 5 }

    [Serializable] public class RailNode
    {
        public int id, x, z;
        public string name = "";
        public TileKind kind;
        public int price = 200, income = 40, owner = -1;
    }
    [Serializable] public class RailEdge { public int a, b; public RailEdge(int aa, int bb) { a = aa; b = bb; } }
    [Serializable] public class RailMap
    {
        public string title = "Japan Rail Trip";
        public int startNode;
        public List<RailNode> nodes = new List<RailNode>();
        public List<RailEdge> edges = new List<RailEdge>();
        public RailMap Copy() => JsonUtility.FromJson<RailMap>(JsonUtility.ToJson(this));
    }

    [CreateAssetMenu(fileName = "NewRailMap", menuName = "Rail Trip/Editable Map")]
    public class RailMapAsset : ScriptableObject
    {
        public RailMap map = new RailMap();
        public RailMap RuntimeCopy() => map.Copy();
    }

    public sealed class RailGraph
    {
        public readonly RailMap map;
        public readonly Dictionary<int, RailNode> nodes = new Dictionary<int, RailNode>();
        private readonly Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        public RailGraph(RailMap data)
        {
            map = data;
            foreach (var node in map.nodes)
            {
                if (nodes.ContainsKey(node.id)) throw new ArgumentException("Duplicate node ID: " + node.id);
                nodes.Add(node.id, node);
                adjacency.Add(node.id, new List<int>());
            }
            foreach (var edge in map.edges)
            {
                if (!nodes.ContainsKey(edge.a) || !nodes.ContainsKey(edge.b) || edge.a == edge.b)
                    throw new ArgumentException("Invalid rail edge");
                var a = nodes[edge.a]; var b = nodes[edge.b];
                if (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.z - b.z) != 1)
                    throw new ArgumentException("Rails must connect adjacent grid cells: " + edge.a + " - " + edge.b);
                if (!adjacency[edge.a].Contains(edge.b)) adjacency[edge.a].Add(edge.b);
                if (!adjacency[edge.b].Contains(edge.a)) adjacency[edge.b].Add(edge.a);
            }
        }
        public IReadOnlyList<int> Neighbors(int id) => adjacency[id];
        public bool Linked(int a, int b) => adjacency.ContainsKey(a) && adjacency[a].Contains(b);
        public List<int> ShortestPath(int from, int to)
        {
            if (!nodes.ContainsKey(from) || !nodes.ContainsKey(to)) return new List<int>();
            var queue = new Queue<int>(); var parent = new Dictionary<int, int> { [from] = -1 };
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (at == to) break;
                foreach (int next in adjacency[at])
                    if (!parent.ContainsKey(next)) { parent[next] = at; queue.Enqueue(next); }
            }
            if (!parent.ContainsKey(to)) return new List<int>();
            var path = new List<int>();
            for (int at = to; at != -1; at = parent[at]) path.Add(at);
            path.Reverse(); return path;
        }
        public int Distance(int from, int to)
        {
            var route = ShortestPath(from, to);
            return route.Count == 0 ? int.MaxValue : route.Count - 1;
        }
        public string Validate()
        {
            if (map.nodes.Count < 3) return "At least 3 map nodes are required";
            if (!nodes.ContainsKey(map.startNode)) return "Start node is missing";
            if (map.nodes.Count(n => n.kind != TileKind.Rail && !string.IsNullOrWhiteSpace(n.name)) < 3) return "Add three named destination stations";
            foreach (var node in map.nodes)
                if (Distance(map.startNode, node.id) == int.MaxValue) return "Disconnected node: " + node.name + " #" + node.id;
            return "";
        }
        public static Vector3 Position(RailNode n) => new Vector3(n.x * 1.65f, 0, n.z * 1.65f);
    }
}
