using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RailTrip3D
{
    // Editable MapAsset => 3D primitives + 2D pixel sprites. No prefabs to wire.
    public class BoardView : MonoBehaviour
    {
        public Camera mapCamera;
        private RailGraph graph;
        private GameObject root;
        private readonly Dictionary<long, Renderer> railRenderers = new Dictionary<long, Renderer>();
        private readonly Dictionary<int, Renderer> nodeRenderers = new Dictionary<int, Renderer>();
        private readonly List<Transform> trainTokens = new List<Transform>();
        private readonly List<SpriteRenderer> trainIcons = new List<SpriteRenderer>();
        private Transform ghostIcon;
        private Transform goalIcon;
        private MatchData snapshot;
        private Vector3 focus;
        public static readonly Color[] PlayerColors = {
            new Color(.18f,.79f,1f), new Color(1f,.41f,.44f),
            new Color(1f,.81f,.25f), new Color(.67f,.45f,1f)
        };
        private static readonly Color Land = new Color(.18f, .32f, .33f);
        private static readonly Color Track = new Color(.48f,.65f,.68f);
        private static readonly Color Highlight = new Color(1f,.78f,.24f);
        private static readonly Color Goal = new Color(1f,.78f,.24f);
        private readonly Dictionary<int, TextMesh> stationLabels = new Dictionary<int, TextMesh>();
        private readonly Dictionary<int, Vector3> positions = new Dictionary<int, Vector3>();
        private bool built;
        public RailGraph Graph => graph;
        public void Build(RailMap map)
        {
            if (root != null) Destroy(root);
            root = new GameObject("Generated Map — source: RailMapAsset");
            root.transform.SetParent(transform, false);
            railRenderers.Clear(); nodeRenderers.Clear(); stationLabels.Clear(); trainTokens.Clear(); trainIcons.Clear(); positions.Clear();
            graph = new RailGraph(map);
            foreach (var node in map.nodes) positions.Add(node.id, RailGraph.Position(node));
            // Dark water under the map.
            var water = Primitive(PrimitiveType.Cube, "Ocean", new Vector3(22, -.8f, 34), new Vector3(65, 1, 78), new Color(.07f,.16f,.23f));
            water.transform.SetParent(root.transform, false);
            foreach (var node in map.nodes)
            {
                Vector3 at = positions[node.id];
                var earth = Primitive(PrimitiveType.Cube, "Island tile", at + Vector3.down * .37f,
                    new Vector3(1.72f, .18f, 1.72f), Land);
                earth.transform.SetParent(root.transform, false);
            }
            foreach (var e in map.edges)
            {
                Vector3 a = positions[e.a], b = positions[e.b], mid = (a + b) / 2 + Vector3.up * .06f;
                bool eastWest = Mathf.Abs(a.x - b.x) > .1f;
                var rail = Primitive(PrimitiveType.Cube, "Rail " + e.a + "-" + e.b, mid,
                    eastWest ? new Vector3(1.72f,.13f,.28f) : new Vector3(.28f,.13f,1.72f), Track);
                rail.transform.SetParent(root.transform, false);
                railRenderers[Key(e.a,e.b)] = rail.GetComponent<Renderer>();
            }
            foreach (var node in map.nodes)
            {
                var at = positions[node.id];
                Color tint = TileColor(node);
                var tile = Primitive(PrimitiveType.Cylinder, node.name == "" ? "Rail stop" : node.name,
                    at + Vector3.up * .18f, new Vector3(.65f,.12f,.65f), tint);
                tile.transform.SetParent(root.transform, false);
                nodeRenderers.Add(node.id, tile.GetComponent<Renderer>());
                if (node.kind == TileKind.Rail || string.IsNullOrEmpty(node.name)) continue;
                var landmark = Primitive(PrimitiveType.Cube, "City landmark " + node.name,
                    at + new Vector3(.42f,.47f,.35f), new Vector3(.43f,.65f,.43f), tint);
                landmark.transform.SetParent(root.transform, false);
                var sign = new GameObject("Name " + node.name);
                sign.transform.SetParent(root.transform, false);
                sign.transform.position = at + new Vector3(0, 1.25f, .2f);
                var text = sign.AddComponent<TextMesh>();
                text.text = node.name; text.characterSize = .37f; text.fontSize = 40; text.fontStyle = FontStyle.Bold;
                text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
                text.color = new Color(.94f,.95f,.94f);
                stationLabels.Add(node.id, text);
                // Actual SpriteRenderer on top of a 3D marker.
                if (node.kind == TileKind.Card)
                    AddBillboard("Card icon", PixelArt.Get("card", tint), at + Vector3.up * 1.15f, .8f, root.transform);
            }
            goalIcon = AddBillboard("Destination", PixelArt.Get("flag", Goal), Vector3.zero, 1.5f, root.transform).transform;
            ghostIcon = AddBillboard("Poor Ghost", PixelArt.Get("ghost", new Color(.98f,.90f,1f)), Vector3.zero, 1.35f, root.transform).transform;
            if (mapCamera == null) CreateCamera();
            SetupLight();
            built = true;
        }
        private static long Key(int a, int b) => ((long)Mathf.Min(a,b) << 32) | (uint)Mathf.Max(a,b);
        private static Color TileColor(RailNode n)
        {
            switch (n.kind)
            {
                case TileKind.Property: return new Color(.99f,.67f,.29f);
                case TileKind.Card: return new Color(.68f,.49f,.98f);
                case TileKind.Bonus: return new Color(.38f,.86f,.54f);
                case TileKind.Penalty: return new Color(.98f,.45f,.53f);
                case TileKind.Station: return new Color(.45f,.75f,.93f);
                default: return new Color(.58f,.76f,.73f);
            }
        }
        private GameObject Primitive(PrimitiveType type, string name, Vector3 p, Vector3 size, Color c)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.position = p; go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = c };
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return go;
        }
        private SpriteRenderer AddBillboard(string name, Sprite sprite, Vector3 point, float size, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false); go.transform.position = point; go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sortingOrder = 10;
            return sr;
        }
        private void CreateCamera()
        {
            var obj = new GameObject("Game Camera");
            mapCamera = obj.AddComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = 16;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color(.07f,.16f,.23f);
            obj.tag = "MainCamera";
            obj.AddComponent<AudioListener>();
        }
        private void SetupLight()
        {
            if (FindFirstObjectByType<Light>() != null) return;
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(50, -25, 0);
        }
        public void Show(MatchData state)
        {
            if (!built || state == null || state.board == null) return;
            snapshot = state;
            while (trainTokens.Count < state.players.Count)
            {
                int index = trainTokens.Count;
                var body = Primitive(PrimitiveType.Cube, "Player Train " + index, Vector3.zero,
                    new Vector3(.85f,.4f,.95f), PlayerColors[index]);
                body.transform.SetParent(root.transform, false);
                trainTokens.Add(body.transform);
                var icon = AddBillboard("2D train sprite", PixelArt.Get("train", PlayerColors[index]), Vector3.up * 1.2f, 1.25f, body.transform);
                trainIcons.Add(icon);
            }
            if (state.players.Count > 0)
            {
                int current = Mathf.Clamp(state.currentSeat, 0, state.players.Count - 1);
                focus = positions[state.players[current].nodeId];
            }
            var shortest = new HashSet<long>();
            if (state.started && state.players.Count > state.currentSeat)
            {
                var path = graph.ShortestPath(state.players[state.currentSeat].nodeId, state.destination);
                for (int i = 1; i < path.Count; i++) shortest.Add(Key(path[i - 1], path[i]));
            }
            foreach (var rail in railRenderers)
                rail.Value.material.color = shortest.Contains(rail.Key) ? Highlight : Track;
            foreach (var n in state.board.nodes)
            {
                if (!nodeRenderers.TryGetValue(n.id, out var r)) continue;
                r.material.color = n.owner < 0 ? TileColor(n) : PlayerColors[Mathf.Clamp(n.owner, 0, 3)];
            }
            if (positions.ContainsKey(state.destination))
                goalIcon.position = positions[state.destination] + Vector3.up * 1.65f;
            if (state.ghostSeat >= 0 && state.ghostSeat < state.players.Count)
                ghostIcon.position = positions[state.players[state.ghostSeat].nodeId] + Vector3.up * 2.05f;
            ghostIcon.gameObject.SetActive(state.ghostSeat >= 0 && state.started);
            goalIcon.gameObject.SetActive(state.started);
        }
        private void LateUpdate()
        {
            if (!built || snapshot == null || mapCamera == null) return;
            for (int i = 0; i < snapshot.players.Count && i < trainTokens.Count; i++)
            {
                Vector3 target = positions[snapshot.players[i].nodeId] +
                    new Vector3((i % 2) * .34f - .17f, .65f, (i / 2) * .32f - .15f);
                trainTokens[i].position = Vector3.Lerp(trainTokens[i].position, target, Mathf.Min(1, Time.deltaTime * 7));
            }
            var at = mapCamera.transform;
            Vector3 aim = focus + new Vector3(0, 26, -24);
            at.position = Vector3.Lerp(at.position, aim, Mathf.Min(1, Time.deltaTime * 3));
            at.LookAt(focus);
            for (int i = 0; i < trainIcons.Count; i++)
                trainIcons[i].transform.rotation = mapCamera.transform.rotation;
            ghostIcon.rotation = mapCamera.transform.rotation;
            goalIcon.rotation = mapCamera.transform.rotation;
        }
        public void Zoom(float delta)
        {
            if (mapCamera != null) mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize - delta, 7, 45);
        }
    }
}
