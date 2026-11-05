using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RailTrip3D
{
    /// <summary>
    /// Bright close-up toy diorama: scenic coast, checker railway, raised station tiles,
    /// miniature towns and a follow camera. Geometry is generated from the editable RailMap.
    /// No copyrighted textures/models are required.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public Camera mapCamera;
        private RailGraph graph;
        private GameObject root;
        private MatchData snapshot;
        private readonly Dictionary<int, Vector3> positions = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Renderer> stopTiles = new Dictionary<int, Renderer>();
        private readonly Dictionary<long, GameObject> routeOverlays = new Dictionary<long, GameObject>();
        private readonly Dictionary<int, GameObject> nameLabels = new Dictionary<int, GameObject>();
        private readonly HashSet<Vector2Int> occupiedLabelSlots = new HashSet<Vector2Int>();
        private float cellSize = RailGraph.DefaultCellSpacing;
        private readonly List<Transform> trains = new List<Transform>();
        private readonly List<Vector3> priorTrainPositions = new List<Vector3>();
        private readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private Transform ghost, flag;
        private Vector3 cameraFocus;
        private bool initialized;
        private int activeSeat;

        public static readonly Color[] PlayerColors = {
            new Color(.19f,.75f,.95f), new Color(1f,.43f,.55f),
            new Color(1f,.76f,.19f), new Color(.63f,.43f,.92f)
        };
        private static readonly Color Grass = new Color(.43f,.78f,.38f);
        private static readonly Color GrassAlt = new Color(.49f,.82f,.42f);
        private static readonly Color Sea = new Color(.17f,.75f,.83f);
        private static readonly Color Sand = new Color(.92f,.81f,.55f);
        private static readonly Color White = new Color(.97f,.98f,.91f);
        private static readonly Color Slate = new Color(.23f,.28f,.34f);
        private static readonly Color Route = new Color(1f,.82f,.21f);
        public RailGraph Graph => graph;
        public bool ShowShortestRoute { get; private set; }
        public void ToggleShortestRoute()
        {
            ShowShortestRoute = !ShowShortestRoute;
            if (snapshot != null) Show(snapshot);
        }

        private Material Mat(Color color)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color);
            if (materials.TryGetValue(key, out var result)) return result;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            result = new Material(shader) { color = color };
            materials[key] = result;
            return result;
        }
        private GameObject Part(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color, Transform parent)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = Mat(color);
            var collider = obj.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return obj;
        }
        private static long Key(int a, int b) =>
            ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
        public Vector3 NodePosition(int id) =>
            positions.TryGetValue(id, out var p) ? p : Vector3.zero;

        public void Build(RailMap map)
        {
            initialized = false;
            if (root != null) Destroy(root);
            root = new GameObject("Generated coastal rail diorama");
            root.transform.SetParent(transform, false);
            stopTiles.Clear(); routeOverlays.Clear(); positions.Clear(); nameLabels.Clear(); occupiedLabelSlots.Clear();
            trains.Clear(); priorTrainPositions.Clear(); sprites.Clear();
            graph = new RailGraph(map);
            cellSize = map.VisualCellSize;
            foreach (var n in map.nodes) positions.Add(n.id, RailGraph.Position(n, cellSize));

            var minX = map.nodes.Min(n => n.x);
            var maxX = map.nodes.Max(n => n.x);
            var minZ = map.nodes.Min(n => n.z);
            var maxZ = map.nodes.Max(n => n.z);
            float spanX = (maxX - minX + 13) * cellSize;
            float spanZ = (maxZ - minZ + 13) * cellSize;
            Vector3 center = new Vector3((minX + maxX) * cellSize * .5f, -1.0f,
                (minZ + maxZ) * cellSize * .5f);
            Part(PrimitiveType.Cube, "Clear turquoise sea", center,
                new Vector3(spanX, 1.45f, spanZ), Sea, root.transform);

            // Add shore/terrain around towns; rail corridors remain connected while
            // exposed overseas tracks become bridges. No external map texture.
            var land = new HashSet<Vector2Int>();
            foreach (var n in map.nodes)
            {
                if (n.overWater) continue;
                land.Add(new Vector2Int(n.x, n.z));
                // Widen ordinary mainland railway corridors into a coherent
                // green landscape instead of a one-tile-wide strip of islands.
                for(int dx=-1;dx<=1;dx++) for(int dz=-1;dz<=1;dz++)
                    if(Hash(n.x+dx,n.z+dz,7)%9!=0)
                        land.Add(new Vector2Int(n.x+dx,n.z+dz));
            }
            foreach (var n in map.nodes.Where(n => !string.IsNullOrWhiteSpace(n.name)))
                for (int dx = -2; dx <= 2; dx++)
                    for (int dz = -2; dz <= 2; dz++)
                        if (Mathf.Abs(dx) + Mathf.Abs(dz) <= 3 &&
                            Hash(n.x + dx, n.z + dz, 2) % 7 != 0)
                            land.Add(new Vector2Int(n.x + dx, n.z + dz));
            foreach (var cell in land)
            {
                bool edge = !land.Contains(cell + Vector2Int.up) || !land.Contains(cell + Vector2Int.right)
                    || !land.Contains(cell + Vector2Int.down) || !land.Contains(cell + Vector2Int.left);
                Vector3 p = new Vector3(cell.x * cellSize, 0, cell.y * cellSize);
                Part(PrimitiveType.Cube, "Island side", p + Vector3.down * .34f,
                    new Vector3(cellSize, .64f, cellSize), edge ? Sand : Grass, root.transform);
                Part(PrimitiveType.Cube, "Green terrain", p + Vector3.down * .095f,
                    new Vector3(cellSize + .01f, .16f, cellSize + .01f),
                    Hash(cell.x,cell.y,3) % 3 == 0 ? GrassAlt : Grass, root.transform);
            }

            // Sparse scenery in countryside, kept away from the rail tiles.
            foreach(var cell in land)
            {
                if(Hash(cell.x,cell.y,17)%24!=0) continue;
                if(map.nodes.Any(n=>n.x==cell.x && n.z==cell.y)) continue;
                Tree(new Vector3(cell.x*cellSize+.19f,.15f,cell.y*cellSize-.13f),
                    (int)(Hash(cell.x,cell.y,8)%7),root.transform);
            }
            foreach (var e in map.edges) DrawRail(e);
            foreach (var node in map.nodes) DrawStop(node);
            foreach (var node in map.nodes.Where(n => !string.IsNullOrWhiteSpace(n.name)))
                DecorateStation(node, land);

            flag = Billboard("Golden destination flag", PixelArt.Get("flag", Route), Vector3.zero, 1.65f, root.transform).transform;
            ghost = Billboard("Poor ghost sprite", PixelArt.Get("ghost", new Color(1f,.82f,.98f)), Vector3.zero, 1.4f, root.transform).transform;
            if (mapCamera == null) CreateCamera();
            SetupLight();
            cameraFocus = NodePosition(map.startNode);
            mapCamera.transform.position = cameraFocus + new Vector3(0, 12, -13);
            mapCamera.transform.LookAt(cameraFocus + Vector3.up * .5f);
            initialized = true;
        }
        private static uint Hash(int x, int z, int seed)
        {
            unchecked
            {
                uint v = (uint)(x * 374761393 + z * 668265263 + seed * 1442695041);
                v = (v ^ (v >> 13)) * 1274126177;
                return v ^ (v >> 16);
            }
        }
        private static Color StopColor(RailNode n)
        {
            switch (n.kind)
            {
                case TileKind.Property: return new Color(.96f,.64f,.26f);
                case TileKind.Card: return new Color(.75f,.40f,.89f);
                case TileKind.Bonus: return new Color(.27f,.78f,.51f);
                case TileKind.Penalty: return new Color(.95f,.32f,.39f);
                case TileKind.Station: return new Color(.36f,.69f,.97f);
                default: return new Color(.26f,.53f,.92f);
            }
        }
        private void DrawRail(RailEdge edge)
        {
            Vector3 a = NodePosition(edge.a), b = NodePosition(edge.b);
            Vector3 mid = (a + b) * .5f;
            float segmentLength = Vector3.Distance(a, b);
            bool horizontal = Mathf.Abs(a.x-b.x) > .01f;
            // Each section spans exactly one graph edge. Changing tileSpacing keeps
            // terrain, stations, sleepers and rails aligned without gaps.
            float bedWidth = .68f;
            float run = segmentLength + .075f;
            Part(PrimitiveType.Cube, "Cream railway ballast", mid + Vector3.up*.12f,
                horizontal ? new Vector3(run,.11f,bedWidth) : new Vector3(bedWidth,.11f,run),
                White, root.transform);
            const int sleepers = 7;
            for (int i=0; i<sleepers; i++)
            {
                float offset = ((i+.5f)/sleepers-.5f)*segmentLength;
                Vector3 at = mid+Vector3.up*.193f +
                    (horizontal ? Vector3.right : Vector3.forward)*offset;
                Part(PrimitiveType.Cube, "Black sleeper", at,
                    horizontal ? new Vector3(.15f,.045f,.58f) : new Vector3(.58f,.045f,.15f),
                    Slate, root.transform);
            }
            for (int side=-1; side<=1; side+=2)
            {
                Vector3 at = mid + Vector3.up*.245f +
                    (horizontal ? Vector3.forward : Vector3.right)*(side*.20f);
                Part(PrimitiveType.Cube, "Steel rail",at,
                    horizontal ? new Vector3(run,.047f,.042f) : new Vector3(.042f,.047f,run),
                    new Color(.61f,.67f,.68f),root.transform);
            }
            var overlay = Part(PrimitiveType.Cube, "Shortest route highlight",
                mid+Vector3.up*.29f,
                horizontal ? new Vector3(segmentLength,.04f,.055f) :
                    new Vector3(.055f,.04f,segmentLength),
                Route,root.transform);
            overlay.SetActive(false);
            routeOverlays[Key(edge.a, edge.b)] = overlay;
        }
        private void DrawStop(RailNode n)
        {
            Vector3 p = NodePosition(n.id);
            var bottom = Part(PrimitiveType.Cube, "Square base " + n.id, p + Vector3.up * .13f,
                new Vector3(1.26f,.18f,1.26f), White, root.transform);
            var tile = Part(PrimitiveType.Cube, "Colored station square " + n.id, p + Vector3.up * .24f,
                new Vector3(1.08f,.10f,1.08f), StopColor(n), root.transform);
            stopTiles.Add(n.id, tile.GetComponent<Renderer>());
            if (n.kind == TileKind.Rail) return;
            // Small embossed symbol/roof on property & card tiles.
            if (n.kind == TileKind.Property)
            {
                Part(PrimitiveType.Cube, "Tiny station house", p + new Vector3(0,.51f,0),
                    new Vector3(.51f,.38f,.44f), new Color(.97f,.95f,.82f), root.transform);
                Part(PrimitiveType.Cube, "Terracotta station roof", p + new Vector3(0,.73f,0),
                    new Vector3(.66f,.13f,.58f), new Color(.85f,.30f,.24f), root.transform);
            }
            else if (n.kind == TileKind.Card)
                Billboard("Pixel card at station", PixelArt.Get("card", Color.white),
                    p + Vector3.up * 1.0f, .73f, root.transform);
            if (string.IsNullOrWhiteSpace(n.name)) return;
            // Flat signs lie on the XZ board plane: never billboard them to the camera.
            // We pick north/south according to connected tracks and reserve slots
            // so neighboring stations do not print signs in the same space.
            int side = ChooseLabelSide(n);
            string label = GameLanguage.City(n.name);
            float offset = cellSize*.44f;
            Vector3 at = p + new Vector3(0,.39f,side*offset);
            var holder = new GameObject("Flat station name: " + n.name);
            holder.transform.SetParent(root.transform,false);
            holder.transform.position = at;
            float textWidth = label.Length * (GameLanguage.IsJapanese ? .31f : .205f) + .38f;
            float width = Mathf.Clamp(textWidth,1.3f,cellSize*1.87f);
            var plaque = Part(PrimitiveType.Cube,"Ground-mounted ivory label",
                at,new Vector3(width,.075f,.56f),
                new Color(1f,.99f,.92f),holder.transform);
            plaque.transform.localPosition = Vector3.zero;
            var textGo = new GameObject("Text on flat plaque");
            textGo.transform.SetParent(holder.transform,false);
            textGo.transform.localPosition = new Vector3(0,.061f,0);
            // TextMesh forward (+Z) becomes world +Y, facing an overhead camera.
            textGo.transform.localRotation = Quaternion.Euler(-90f,0,0);
            var text = textGo.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = .155f;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(.13f,.17f,.18f);
            Font font = GameLanguage.JapaneseFont;
            if (font != null)
            {
                text.font = font;
                var r = textGo.GetComponent<MeshRenderer>();
                if (r != null) r.sharedMaterial = font.material;
            }
            nameLabels.Add(n.id,holder);
        }
        private int ChooseLabelSide(RailNode n)
        {
            bool northRail = graph.Neighbors(n.id).Any(id=>graph.nodes[id].z>n.z);
            bool southRail = graph.Neighbors(n.id).Any(id=>graph.nodes[id].z<n.z);
            Vector2Int north = new Vector2Int(n.x, n.z*2+1);
            Vector2Int south = new Vector2Int(n.x, n.z*2-1);
            bool freeNorth = !occupiedLabelSlots.Contains(north);
            bool freeSouth = !occupiedLabelSlots.Contains(south);
            int side;
            if (freeNorth && !northRail) side=1;
            else if (freeSouth && !southRail) side=-1;
            else if (freeNorth) side=1;
            else if (freeSouth) side=-1;
            else side=(n.id%2==0)?1:-1;
            occupiedLabelSlots.Add(side>0 ? north : south);
            return side;
        }
        private void DecorateStation(RailNode node, HashSet<Vector2Int> land)
        {
            int x = node.x, z = node.z;
            for (int i = 0; i < 3; i++)
            {
                int dx = (int)(Hash(x+i,z,9) % 5) - 2;
                int dz = (int)(Hash(x,z+i,11) % 5) - 2;
                if (dx == 0 && dz == 0) continue;
                var place = new Vector2Int(x + dx, z + dz);
                if (!land.Contains(place) || graph.map.nodes.Any(n=>n.x==place.x && n.z==place.y)) continue;
                Vector3 p = new Vector3(place.x * cellSize + .28f, .15f, place.y * cellSize + .25f);
                if (Hash(place.x,place.y,i) % 3 == 0) Tree(p, i, root.transform);
                else House(p, i, root.transform);
            }
        }
        private void House(Vector3 p, int seed, Transform parent)
        {
            float h = .54f + .17f * (seed % 3);
            Color wall = seed % 3 == 0 ? new Color(.99f,.92f,.75f)
                : seed % 3 == 1 ? new Color(.79f,.90f,.92f) : new Color(.95f,.71f,.67f);
            Part(PrimitiveType.Cube, "Miniature town house", p + Vector3.up * h / 2,
                new Vector3(.67f,h,.64f), wall, parent);
            Part(PrimitiveType.Cube, "Colorful roof", p + Vector3.up * (h + .11f),
                new Vector3(.85f,.17f,.82f),
                seed % 2 == 0 ? new Color(.88f,.32f,.31f) : new Color(.26f,.45f,.73f), parent);
            Part(PrimitiveType.Cube, "Window", p + new Vector3(0,h * .56f,-.329f),
                new Vector3(.19f,.20f,.028f), new Color(.26f,.61f,.77f), parent);
        }
        private void Tree(Vector3 p, int seed, Transform parent)
        {
            Part(PrimitiveType.Cylinder, "Tree trunk", p + Vector3.up * .23f,
                new Vector3(.12f,.26f,.12f), new Color(.44f,.28f,.19f), parent);
            Color crown = seed % 4 == 0 ? new Color(1f,.66f,.77f) : new Color(.20f,.66f,.31f);
            Part(PrimitiveType.Sphere, "Tree canopy", p + Vector3.up * .74f,
                new Vector3(.68f,.72f,.68f), crown, parent);
            Part(PrimitiveType.Sphere, "Tree top", p + new Vector3(.11f,.95f,.07f),
                new Vector3(.45f,.41f,.47f), crown, parent);
        }
        private SpriteRenderer Billboard(string label, Sprite sprite, Vector3 point, float scale, Transform parent)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false); go.transform.position = point; go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sortingOrder = 15;
            sprites.Add(renderer); return renderer;
        }
        private void CreateCamera()
        {
            var go = new GameObject("Local isometric follow camera");
            mapCamera = go.AddComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = 6.1f; // Close-up: only a few adjacent stations visible.
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = Sea;
            if (Camera.main == null) go.tag = "MainCamera";
            if (FindFirstObjectByType<AudioListener>() == null) go.AddComponent<AudioListener>();
        }
        private void SetupLight()
        {
            if (FindFirstObjectByType<Light>() != null) return;
            var l = new GameObject("Sunny daylight").AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f,.97f,.87f);
            l.intensity = 1.3f;
            l.transform.rotation = Quaternion.Euler(57, -27, 0);
            RenderSettings.ambientLight = new Color(.72f,.82f,.84f);
        }
        private Transform CreateTrain(int index)
        {
            var go = new GameObject("Miniature train " + index);
            go.transform.SetParent(root.transform, false);
            var t = go.transform;
            var main = PlayerColors[index % PlayerColors.Length];
            // Parts in local space. Engine points toward +Z.
            Vector3 p = Vector3.zero;
            var body = Part(PrimitiveType.Cube,"Engine body",p+new Vector3(0,.34f,0),
                new Vector3(.88f,.6f,1.24f),main,t);
            Part(PrimitiveType.Cube,"Round roof",p+new Vector3(0,.68f,-.04f),
                new Vector3(.84f,.19f,1.24f),new Color(.97f,.94f,.85f),t);
            Part(PrimitiveType.Cube,"Windshield",p+new Vector3(0,.42f,.64f),
                new Vector3(.63f,.36f,.055f),new Color(.20f,.63f,.76f),t);
            Part(PrimitiveType.Cube,"Headlight",p+new Vector3(0,.14f,.67f),
                new Vector3(.22f,.15f,.08f),new Color(1f,.92f,.41f),t);
            for(int side=-1; side<=1; side+=2)
            {
                Part(PrimitiveType.Cube,"Side window",p+new Vector3(side*.455f,.44f,-.15f),
                    new Vector3(.045f,.30f,.5f),new Color(.82f,.96f,.98f),t);
                for(int i=0;i<2;i++)
                    Part(PrimitiveType.Cylinder,"Wheel",p+new Vector3(side*.48f,.03f,i*.60f-.27f),
                        new Vector3(.24f,.12f,.24f),Slate,t).transform.rotation=Quaternion.Euler(0,0,90);
            }
            var sprite = Billboard("2D train marker",PixelArt.Get("train", main),
                go.transform.position + Vector3.up * 1.7f,.72f,t);
            sprite.transform.localPosition = new Vector3(0,1.54f,0);
            return t;
        }
        public void Show(MatchData state)
        {
            if (!initialized || state == null || state.players.Count == 0) return;
            snapshot = state;
            activeSeat = Mathf.Clamp(state.currentSeat,0,state.players.Count-1);
            while (trains.Count < state.players.Count)
            {
                int i = trains.Count;
                var train = CreateTrain(i);
                train.position = NodePosition(state.players[i].nodeId)+Vector3.up*.26f;
                trains.Add(train); priorTrainPositions.Add(train.position);
            }
            foreach (var tile in state.board.nodes)
                if (stopTiles.TryGetValue(tile.id,out Renderer target))
                    target.sharedMaterial = Mat(tile.owner >= 0
                        ? PlayerColors[Mathf.Clamp(tile.owner,0,PlayerColors.Length-1)]
                        : StopColor(tile));
            var shortest = new HashSet<long>();
            if (ShowShortestRoute && state.started && state.players.Count > activeSeat)
            {
                var path = graph.ShortestPath(state.players[activeSeat].nodeId,state.destination);
                for(int i=1;i<path.Count;i++) shortest.Add(Key(path[i-1],path[i]));
            }
            foreach(var entry in routeOverlays) entry.Value.SetActive(shortest.Contains(entry.Key));
            if (positions.ContainsKey(state.destination))
                flag.position = NodePosition(state.destination)+Vector3.up*2.1f;
            flag.gameObject.SetActive(state.started);
            bool hasGhost = state.ghostSeat >= 0 && state.ghostSeat < state.players.Count && state.started;
            ghost.gameObject.SetActive(hasGhost);
            if(hasGhost)
                ghost.position = NodePosition(state.players[state.ghostSeat].nodeId)+Vector3.up*2.25f;
        }
        private void LateUpdate()
        {
            if (!initialized || snapshot == null || mapCamera == null) return;
            for (int i=0;i<snapshot.players.Count && i<trains.Count;i++)
            {
                Vector3 target = NodePosition(snapshot.players[i].nodeId)
                    + new Vector3((i%2)*.24f-.12f,.26f,(i/2)*.23f-.11f);
                Vector3 previous = trains[i].position;
                trains[i].position = Vector3.MoveTowards(previous,target,Time.deltaTime*6.7f);
                Vector3 movement = target-previous; movement.y = 0;
                if (movement.sqrMagnitude > .005f)
                    trains[i].rotation = Quaternion.Slerp(trains[i].rotation,
                        Quaternion.LookRotation(movement,Vector3.up),Time.deltaTime*14);
            }
            // Follow the interpolating train instead of snapping to its node.
            // Look a little ahead in the train's current heading.
            Transform activeTrain = trains[Mathf.Clamp(activeSeat,0,trains.Count-1)];
            Vector3 heading = activeTrain.forward; heading.y = 0;
            heading = heading.sqrMagnitude < .01f ? Vector3.forward : heading.normalized;
            Vector3 desiredFocus = activeTrain.position + heading*.70f + Vector3.up*.2f;
            float follow = 1f-Mathf.Exp(-7f*Time.deltaTime);
            cameraFocus = Vector3.Lerp(cameraFocus,desiredFocus,follow);
            Vector3 cameraPos = cameraFocus+new Vector3(0,12,-13);
            mapCamera.transform.position = Vector3.Lerp(mapCamera.transform.position,
                cameraPos,follow);
            mapCamera.transform.LookAt(cameraFocus);
            // Hide the complete plaque, not just the lettering, outside the local area.
            foreach (var label in nameLabels)
            {
                Vector3 pos=NodePosition(label.Key);
                bool nearby = (new Vector2(pos.x-cameraFocus.x,pos.z-cameraFocus.z)).sqrMagnitude
                    < cellSize*cellSize*14f;
                if (label.Value.activeSelf != nearby) label.Value.SetActive(nearby);
            }
            foreach (var sprite in sprites)
                if (sprite != null && sprite.gameObject.activeInHierarchy)
                    sprite.transform.rotation =
                        Quaternion.LookRotation(mapCamera.transform.position-sprite.transform.position,Vector3.up);
        }
        public void Zoom(float delta)
        {
            if (mapCamera!=null) mapCamera.orthographicSize =
                Mathf.Clamp(mapCamera.orthographicSize - delta*.4f,5.3f,9.3f);
        }
    }
}
