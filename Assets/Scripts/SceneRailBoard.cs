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

    // Select this object in the Scene/Hierarchy to move the station or change properties.
    [DisallowMultipleComponent]
    public sealed class SceneRailNode : MonoBehaviour
    {
        [Min(0)] public int nodeId;
        public string stationName;
        public TileKind kind;
        [Min(0)] public int price = 200;
        [Min(0)] public int income = 40;
        public bool overWater;
        public Renderer tileRenderer;
        public Transform flatNamePlate;
        public TextMesh labelText;

        public void RefreshLabel()
        {
            if (flatNamePlate != null)
                flatNamePlate.gameObject.SetActive(!string.IsNullOrWhiteSpace(stationName));
            if (labelText != null)
            {
                labelText.text = GameLanguage.City(stationName);
                if (GameLanguage.JapaneseFont != null)
                {
                    labelText.font = GameLanguage.JapaneseFont;
                    var renderer = labelText.GetComponent<MeshRenderer>();
                    if (renderer != null) renderer.sharedMaterial = GameLanguage.JapaneseFont.material;
                }
            }
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += SafeRefresh;
        }
        private void SafeRefresh()
        {
            if (this == null || Application.isPlaying) return;
            RefreshLabel();
        }
#endif
    }

    // A complete, editable track object. Move its endpoints in the Scene and
    // use Rail Trip > Refresh Selected Tracks to align its authored meshes.
    [DisallowMultipleComponent]
    public sealed class SceneRailSegment : MonoBehaviour
    {
        public SceneRailNode from;
        public SceneRailNode to;
        public GameObject routeHighlight;

        public void RefreshTransform()
        {
            if (from == null || to == null) return;
            Vector3 a = from.transform.position, b = to.transform.position;
            Vector3 difference = b-a;
            transform.position = (a+b)*.5f;
            transform.rotation = Quaternion.LookRotation(
                Mathf.Abs(difference.z) >= Mathf.Abs(difference.x) ? Vector3.forward : Vector3.right,
                Vector3.up);
            // Mesh children use local X width / local Z length. The track section has
            // a scaled parent so rail length changes when its endpoints are moved.
            transform.localScale = new Vector3(1f,1f,Mathf.Max(.2f,difference.magnitude/2.2f));
        }
    }
}
