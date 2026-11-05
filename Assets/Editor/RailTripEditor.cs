#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RailTrip3D.Editor
{
    // Creates a playable scene and the editor-friendly active map on first project import.
    [InitializeOnLoad]
    internal static class FirstOpenSetup
    {
        private const string MapPath = "Assets/Resources/Maps/ActiveMap.asset";
        private const string ScenePath = "Assets/Scenes/Main.unity";
        static FirstOpenSetup() { EditorApplication.delayCall += EnsureProject; }
        private static void EnsureProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            bool created = false;
            Directory.CreateDirectory("Assets/Resources/Maps");
            Directory.CreateDirectory("Assets/Scenes");
            if (AssetDatabase.LoadAssetAtPath<RailMapAsset>(MapPath) == null)
            {
                var map = ScriptableObject.CreateInstance<RailMapAsset>();
                map.map = SampleMap.Create();
                AssetDatabase.CreateAsset(map, MapPath);
                created = true;
            }
            // Bake once into a real .unity scene on first import. Existing authored
            // scene objects are NEVER overwritten by Play or script recompilation.
            bool noScene = !File.Exists(ScenePath);
            bool legacyBlankScene = !noScene && !File.ReadAllText(ScenePath).Contains("--- !u!1 &");
            if (noScene || legacyBlankScene)
            {
                var asset = AssetDatabase.LoadAssetAtPath<RailMapAsset>(MapPath);
                EditableSceneBaker.Bake(asset != null ? asset.RuntimeCopy() : SampleMap.Create());
                created = true;
            }
            bool listed = EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled);
            if (!listed)
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                created = true;
            }
            if (created)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Rail Trip 3D ready: Scene is saved and editable. Open Assets/Scenes/Main.unity and move stations in the Scene view.");
            }
        }
    }

    public class RailMapEditorWindow : EditorWindow
    {
        private const string MapPath = "Assets/Resources/Maps/ActiveMap.asset";
        private RailMapAsset asset;
        private Vector2 scroll;
        private int selected = -1, connectingFrom = -1;
        private int addX, addZ;
        private string validation = "";
        private const int Cell = 21;
        // Ctrl/Cmd+Shift+M now opens the editable 3D Scene, not the legacy grid view.
        [MenuItem("Rail Trip/Edit Map %#m")]
        public static void OpenSceneEditor()
        {
            EditableSceneBaker.OpenScene();
            var board = UnityEngine.Object.FindFirstObjectByType<SceneRailBoard>();
            if (board != null)
            {
                Selection.activeGameObject = board.gameObject;
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();
            }
        }
        [MenuItem("Rail Trip/Legacy 2D Asset Grid Editor")]
        public static void Open()
        {
            var w = GetWindow<RailMapEditorWindow>("Legacy Map Asset (not live Scene)");
            w.minSize = new Vector2(800, 600); w.Show();
        }
        [MenuItem("Rail Trip/Reset to Sample Japan Map")]
        public static void Reset()
        {
            if (!EditorUtility.DisplayDialog("Reset Rail Map", "Replace the active map? Make a backup first.", "Replace", "Cancel")) return;
            Directory.CreateDirectory("Assets/Resources/Maps");
            var asset = AssetDatabase.LoadAssetAtPath<RailMapAsset>(MapPath);
            if (asset == null)
            {
                asset = CreateInstance<RailMapAsset>(); asset.map = SampleMap.Create();
                AssetDatabase.CreateAsset(asset, MapPath);
            }
            else
            {
                Undo.RecordObject(asset, "Reset Rail Map");
                asset.map = SampleMap.Create();
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Rail Trip/Export Active Map JSON")]
        public static void Export()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RailMapAsset>(MapPath);
            if (asset == null) { Debug.LogError("No active map asset"); return; }
            Directory.CreateDirectory("MapExports");
            File.WriteAllText("MapExports/ActiveMap.json", JsonUtility.ToJson(asset.map, true));
            Debug.Log("Wrote MapExports/ActiveMap.json");
        }
        [MenuItem("Rail Trip/Build WebGL")]
        public static void BuildWebGL()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            Directory.CreateDirectory("Builds/WebGL");
            BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
        }
        [MenuItem("Rail Trip/Run Map & Rules Sanity Test")]
        public static void RunSanity()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RailMapAsset>(MapPath);
            var map = asset != null ? asset.RuntimeCopy() : SampleMap.Create();
            var graph = new RailGraph(map);
            string problem = graph.Validate();
            if (problem != "") { Debug.LogError("Map validation failed: " + problem); return; }
            var s = GameRules.NewMatch(map, "A", "B");
            if (!GameRules.Apply(s, 0, "start") || !GameRules.Apply(s, 0, "roll"))
                throw new Exception("Turn state machine failed");
            int to = graph.Neighbors(s.players[0].nodeId)[0];
            if (!GameRules.Apply(s, 0, "move", to))
                throw new Exception("Movement failed");
            if (GameRules.Apply(s, 1, "roll")) throw new Exception("Wrong player could roll");
            Debug.Log("Rail Trip validation passed: " + map.nodes.Count + " nodes, " + map.edges.Count + " rails");
        }
        private void OnEnable() { asset = AssetDatabase.LoadAssetAtPath<RailMapAsset>(MapPath); }
        private void OnGUI()
        {
            asset = (RailMapAsset)EditorGUILayout.ObjectField("Editable map asset", asset, typeof(RailMapAsset), false);
            if (asset == null)
            {
                EditorGUILayout.HelpBox("Create the editable sample map from Rail Trip > Reset to Sample Japan Map.", MessageType.Info);
                return;
            }
            var map = asset.map;
            EditorGUI.BeginChangeCheck();
            float visualSpacing = EditorGUILayout.Slider("Station / rail spacing", map.VisualCellSize, 1.8f, 3.4f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(asset, "Adjust railway grid spacing");
                map.tileSpacing = visualSpacing;
                EditorUtility.SetDirty(asset);
            }
            EditorGUILayout.LabelField("Click: select | SHIFT+click empty: add | CONNECT: click adjacent node to add/remove rail", EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate", GUILayout.Width(85)))
                {
                    try { validation = new RailGraph(map).Validate(); if (validation == "") validation = "Map is valid"; }
                    catch (Exception ex) { validation = ex.Message; }
                }
                if (GUILayout.Button("Save Asset", GUILayout.Width(105))) AssetDatabase.SaveAssets();
                if (GUILayout.Button("Export JSON", GUILayout.Width(105))) Export();
                if (GUILayout.Button("Add Node @ X,Z", GUILayout.Width(125))) AddNode(map, addX, addZ);
                addX = EditorGUILayout.IntField("X", addX, GUILayout.Width(100));
                addZ = EditorGUILayout.IntField("Z", addZ, GUILayout.Width(100));
            }
            if (!string.IsNullOrEmpty(validation)) EditorGUILayout.HelpBox(validation, MessageType.Info);
            // Grid is laid out in world X/Z coordinates. Scroll to reach outlying lines.
            var bounds = new Rect(0,0, 50 * Cell, 53 * Cell);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Max(260, position.height * .56f)));
            Rect area = GUILayoutUtility.GetRect(bounds.width, bounds.height);
            EditorGUI.DrawRect(area, new Color(.11f,.19f,.24f));
            for (int x = 0; x <= 50; x++)
                EditorGUI.DrawRect(new Rect(area.x + x * Cell, area.y, 1, area.height), new Color(.2f,.3f,.35f,.5f));
            for (int z = 0; z <= 53; z++)
                EditorGUI.DrawRect(new Rect(area.x, area.y + z * Cell, area.width, 1), new Color(.2f,.3f,.35f,.5f));
            Vector2 P(RailNode n) => new Vector2(area.x + n.x * Cell + Cell / 2f, area.y + (51 - n.z) * Cell + Cell / 2f);
            Handles.BeginGUI();
            foreach (var edge in map.edges)
            {
                if (edge.a < 0 || edge.b < 0 || edge.a >= map.nodes.Count || edge.b >= map.nodes.Count) continue;
                Handles.color = Color.gray;
                Handles.DrawAAPolyLine(3, P(map.nodes[edge.a]), P(map.nodes[edge.b]));
            }
            Handles.EndGUI();
            foreach (var node in map.nodes)
            {
                var p = P(node);
                Rect cell = new Rect(p.x - 6, p.y - 6, 12, 12);
                EditorGUI.DrawRect(cell, node.id == selected ? new Color(1,.8f,.2f) :
                    node.kind == TileKind.Rail ? new Color(.4f,.65f,.73f) : new Color(.95f,.55f,.3f));
                if (node.kind != TileKind.Rail && !string.IsNullOrEmpty(node.name))
                    GUI.Label(new Rect(p.x+8,p.y-9,120,20), node.name, EditorStyles.miniLabel);
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && area.Contains(Event.current.mousePosition))
            {
                Vector2 point = Event.current.mousePosition - area.position;
                int x = Mathf.RoundToInt((point.x - Cell/2f) / Cell);
                int z = 51 - Mathf.RoundToInt((point.y - Cell/2f) / Cell);
                int found = map.nodes.FindIndex(n => n.x == x && n.z == z);
                if (found >= 0)
                {
                    if (connectingFrom >= 0 && connectingFrom != found)
                    {
                        Connect(map, connectingFrom, found);
                        connectingFrom = -1;
                    }
                    selected = found;
                }
                else if (Event.current.shift) AddNode(map, x, z);
                Event.current.Use();
                Repaint();
            }
            EditorGUILayout.EndScrollView();
            if (selected < 0 || selected >= map.nodes.Count)
            {
                EditorGUILayout.HelpBox("Select any node to edit its tile type, label, cost, income and connections.", MessageType.None);
                return;
            }
            var chosen = map.nodes[selected];
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("SELECTED NODE  #" + chosen.id + "   (" + chosen.x + ", " + chosen.z + ")", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("Station name", chosen.name);
            TileKind kind = (TileKind)EditorGUILayout.EnumPopup("Tile kind", chosen.kind);
            int price = EditorGUILayout.IntField("Property price", chosen.price);
            int income = EditorGUILayout.IntField("Income / round", chosen.income);
            bool waterRail = EditorGUILayout.Toggle("Water / bridge square",chosen.overWater);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(asset, "Edit node");
                chosen.name = name; chosen.kind = kind;
                chosen.price = Mathf.Max(0, price); chosen.income = Mathf.Max(0, income);
                chosen.overWater = waterRail;
                EditorUtility.SetDirty(asset);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(connectingFrom == selected ? "Choose second node to TOGGLE rail" : "TOGGLE RAIL FROM THIS NODE"))
                    connectingFrom = selected;
                if (GUILayout.Button("Set as start")) { Undo.RecordObject(asset,"New Start"); map.startNode = chosen.id; EditorUtility.SetDirty(asset); }
                if (GUILayout.Button("Delete node")) { RemoveNode(map, selected); selected = -1; connectingFrom = -1; }
            }
            EditorGUILayout.LabelField("CONNECTED RAILS",EditorStyles.boldLabel);
            foreach(var neighbor in map.edges.Where(e=>e.a==selected || e.b==selected).ToList())
            {
                int other=neighbor.a==selected ? neighbor.b : neighbor.a;
                if(other<0 || other>=map.nodes.Count)continue;
                using(new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("→ #"+other+"  "+map.nodes[other].name, GUILayout.Width(300));
                    if(GUILayout.Button("REMOVE TRACK",GUILayout.Width(150)))
                    {
                        Undo.RecordObject(asset,"Remove rail");
                        map.edges.Remove(neighbor);
                        EditorUtility.SetDirty(asset);
                        break;
                    }
                }
            }
        }
        private void AddNode(RailMap map, int x, int z)
        {
            if (map.nodes.Any(n => n.x == x && n.z == z)) { validation = "A node already occupies that grid cell"; return; }
            Undo.RecordObject(asset,"Add node");
            map.nodes.Add(new RailNode { id = map.nodes.Count, x = x, z = z, name = "", kind = TileKind.Rail });
            selected = map.nodes.Count - 1;
            EditorUtility.SetDirty(asset);
        }
        private void Connect(RailMap map, int a, int b)
        {
            var x = map.nodes[a]; var y = map.nodes[b];
            if (Mathf.Abs(x.x - y.x) + Mathf.Abs(x.z - y.z) != 1)
            { validation = "Connect adjacent grid cells only. Add intermediate rail nodes."; return; }
            var existing=map.edges.FirstOrDefault(e => e.a == a && e.b == b || e.a == b && e.b == a);
            Undo.RecordObject(asset,existing==null ? "Add rail" : "Remove rail");
            if(existing!=null) map.edges.Remove(existing);
            else map.edges.Add(new RailEdge(a,b));
            EditorUtility.SetDirty(asset);
        }
        private void RemoveNode(RailMap map, int deleted)
        {
            Undo.RecordObject(asset,"Remove map node");
            map.nodes.RemoveAt(deleted);
            map.edges.RemoveAll(e => e.a == deleted || e.b == deleted);
            foreach (var e in map.edges)
            {
                if (e.a > deleted) e.a--;
                if (e.b > deleted) e.b--;
            }
            for (int i = 0; i < map.nodes.Count; i++) map.nodes[i].id = i;
            if (map.startNode == deleted) map.startNode = 0;
            else if (map.startNode > deleted) map.startNode--;
            EditorUtility.SetDirty(asset);
        }
    }
}
#endif
