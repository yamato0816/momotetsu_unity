using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RailTrip3D
{
    // This component and its children are serialized in Main.unity.
    // Editing their Transforms/Inspector values changes the board used by the game.
    [DisallowMultipleComponent]
    public sealed class SceneRailBoard : MonoBehaviour
    {
        public string boardTitle = "Rail Trip / Editable Scene";
        [Min(0)] public int startNodeId;
        [Min(.1f)] public float gridSize = 2.2f;
        public Camera followCamera;
        public Transform[] playerTrains = new Transform[4];
        public Transform ghostMarker;
        public Transform goalMarker;
        public Transform diceVisual;

        public SceneRailNode[] Nodes => GetComponentsInChildren<SceneRailNode>(true);
        public SceneRailSegment[] Segments => GetComponentsInChildren<SceneRailSegment>(true);

        public RailMap Export()
        {
            var result = new RailMap { title = boardTitle, startNode = startNodeId };
            var ordered = Nodes.OrderBy(n => n.nodeId).ToArray();
            if (ordered.Length == 0)
                throw new InvalidOperationException("The editable scene has no station/rail nodes.");
            for (int i = 0; i < ordered.Length; i++)
            {
                var node = ordered[i];
                if (node.nodeId != i)
                    throw new InvalidOperationException("Node IDs must be unique and sequential, from 0. Invalid ID: " + node.nodeId);
                result.nodes.Add(new RailNode {
                    id = i,
                    x = Mathf.RoundToInt(node.transform.position.x / gridSize),
                    z = Mathf.RoundToInt(node.transform.position.z / gridSize),
                    name = node.stationName ?? "",
                    kind = node.kind,
                    price = Mathf.Max(0,node.price),
                    income = Mathf.Max(0,node.income),
                    owner = -1,
                    overWater = node.overWater
                });
            }
            var seen = new HashSet<long>();
            foreach (var segment in Segments)
            {
                if (segment.from == null || segment.to == null)
                    throw new InvalidOperationException("An authored track has an empty endpoint: " + segment.name);
                int a = segment.from.nodeId, b = segment.to.nodeId;
                long key = ((long)Mathf.Min(a,b) << 32) | (uint)Mathf.Max(a,b);
                if (!seen.Add(key)) throw new InvalidOperationException("Duplicate track: " + a + " / " + b);
                result.edges.Add(new RailEdge(a,b));
            }
            var graph = new RailGraph(result);
            string validation = graph.Validate();
            if (!string.IsNullOrEmpty(validation))
                throw new InvalidOperationException("Scene map invalid: " + validation);
            return result;
        }
    }

}
