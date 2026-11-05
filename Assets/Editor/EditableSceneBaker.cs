#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RailTrip3D.Editor
{
    /// <summary>
    /// Editor-only one-time bake: every rail, sign, station, house and tree is a
    /// normal serialized Unity Scene object, editable with Move/Rotate/Scale.
    /// Nothing is procedurally rebuilt during Play.
    /// </summary>
    public static class EditableSceneBaker
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        private const string MapPath = "Assets/Resources/Maps/ActiveMap.asset";
        private const string MaterialsPath = "Assets/Art/Materials";
        private static readonly Dictionary<string,Material> palette = new Dictionary<string,Material>();

        [MenuItem("Rail Trip/01 Open Editable Scene")]
        public static void OpenScene()
        {
            if (File.Exists(ScenePath))
                EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            else RebuildScene();
        }
        [MenuItem("Rail Trip/02 Rebuild Editable Scene from Map Asset")]
        public static void RebuildScene()
        {
            if(!EditorUtility.DisplayDialog("Rebuild Scene?",
                "This replaces Main.unity and ALL manual object placement. Export/back up your scene first. Continue?",
                "Rebuild Scene","Cancel"))return;
            var source = AssetDatabase.LoadAssetAtPath<RailMapAsset>(MapPath);
            Bake(source!=null?source.RuntimeCopy():SampleMap.Create());
        }
        [MenuItem("Rail Trip/03 Validate Scene Map")]
        public static void ValidateScene()
        {
            var board=UnityEngine.Object.FindFirstObjectByType<SceneRailBoard>();
            if(board==null)
            {
                EditorUtility.DisplayDialog("Scene not found","Open the saved Main.unity first.","OK");
                return;
            }
            try
            {
                var map=board.Export();
                EditorUtility.DisplayDialog("Scene valid",
                    map.nodes.Count+" railway squares and "+map.edges.Count+" links.\n"+
                    "All tracks are orthogonal and the board is connected.","OK");
            }
            catch(Exception e)
            {
                Debug.LogError(e);
                EditorUtility.DisplayDialog("Scene map invalid",e.Message,"OK");
            }
        }
        [MenuItem("Rail Trip/04 Save Scene Layout to Map Asset")]
        public static void ExportSceneToAsset()
        {
            var board=UnityEngine.Object.FindFirstObjectByType<SceneRailBoard>();
            if(board==null){ Debug.LogError("Open the authored map scene first.");return; }
            RailMap map;
            try { map=board.Export(); }
            catch(Exception e) { Debug.LogError(e);return; }
            Directory.CreateDirectory("Assets/Resources/Maps");
            var source=AssetDatabase.LoadAssetAtPath<RailMapAsset>(MapPath);
            if(source==null)
            {
                source=ScriptableObject.CreateInstance<RailMapAsset>();
                AssetDatabase.CreateAsset(source,MapPath);
            }
            Undo.RecordObject(source,"Export authored Scene Map");
            source.map=map;
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();
            Debug.Log("Scene exported to "+MapPath);
        }
        [MenuItem("Rail Trip/05 Refresh Saved Rail Geometry")]
        public static void RefreshRailGeometry()
        {
            var board=UnityEngine.Object.FindFirstObjectByType<SceneRailBoard>();
            if(board==null){Debug.LogError("SceneRailBoard missing");return;}
            foreach(var edge in board.Segments)
            {
                Undo.RecordObject(edge.transform,"Refresh scene track");
                edge.RefreshTransform();
            }
            foreach(var node in board.Nodes) node.RefreshLabel();
            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            Debug.Log("Saved track positions refreshed; now save the Scene.");
        }
        [MenuItem("Rail Trip/06 Link Selected Stations")]
        public static void LinkSelectedStations()
        {
            var chosen=Selection.gameObjects.Select(x=>x.GetComponent<SceneRailNode>())
                .Where(x=>x!=null).ToArray();
            if(chosen.Length!=2)
            {
                EditorUtility.DisplayDialog("Select two stations",
                    "Ctrl/Cmd-select exactly two station square root objects in the Hierarchy.","OK");
                return;
            }
            var board=chosen[0].GetComponentInParent<SceneRailBoard>();
            if(board==null || chosen[1].GetComponentInParent<SceneRailBoard>()!=board)
            {
                Debug.LogError("Selected stations must belong to the same board.");return;
            }
            var a=chosen[0];var b=chosen[1];
            float d=board.gridSize;
            if(Mathf.Abs(a.transform.position.x-b.transform.position.x)+
                Mathf.Abs(a.transform.position.z-b.transform.position.z) > d+.1f)
            {
                Debug.LogError("Only adjacent grid cells can connect. Add intermediate rail squares.");return;
            }
            var existing=board.Segments.FirstOrDefault(e=>
                e.from==a&&e.to==b || e.from==b&&e.to==a);
            if(existing!=null){Debug.LogWarning("These stations already have a track.");return;}
            var parent=board.transform.Find("02_TRACKS");
            if(parent==null){Debug.LogError("02_TRACKS scene group is missing.");return;}
            MakeTrack(a,b,parent,board.gridSize);
            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
        }

        private static Material Mat(string name,Color color)
        {
            if(palette.TryGetValue(name,out var m))return m;
            Directory.CreateDirectory(MaterialsPath);
            string path=MaterialsPath+"/"+name+".mat";
            m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null)
            {
                Shader shader=Shader.Find("Universal Render Pipeline/Lit");
                if(shader==null)shader=Shader.Find("Standard");
                m=new Material(shader) { name=name,color=color };
                AssetDatabase.CreateAsset(m,path);
            }
            palette.Add(name,m);
            return m;
        }
        private static Transform Group(string name,Transform parent)
        {
            var go=new GameObject(name);
            go.transform.SetParent(parent,false);
            return go.transform;
        }
        private static GameObject Shape(PrimitiveType type,string name,Transform parent,
            Vector3 point,Vector3 size,string material,Color color)
        {
            var go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=point;
            go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=Mat(material,color);
            var collider=go.GetComponent<Collider>();
            if(collider!=null)UnityEngine.Object.DestroyImmediate(collider);
            return go;
        }
        private static uint Hash(int x,int z,int seed)
        {
            unchecked
            {
                uint v=(uint)(x*374761393+z*668265263+seed*1442695041);
                v=(v^(v>>13))*1274126177;return v^(v>>16);
            }
        }
        private static readonly Color White=new Color(.99f,.97f,.89f);
        private static readonly Color Dark=new Color(.18f,.23f,.28f);
        private static readonly Color RailGray=new Color(.69f,.72f,.70f);
        private static readonly Color Sand=new Color(.91f,.81f,.56f);
        private static readonly Color Grass=new Color(.38f,.75f,.34f);
        private static readonly Color[] TeamColors=BoardView.PlayerColors;

        public static void Bake(RailMap map)
        {
            var graph=new RailGraph(map);
            string issue=graph.Validate();
            if(!string.IsNullOrEmpty(issue))throw new InvalidOperationException(issue);
            palette.Clear();
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(MaterialsPath);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=Group("RAIL TRIP - EDITABLE SCENE",null);
            var board=root.gameObject.AddComponent<SceneRailBoard>();
            board.boardTitle=map.title;
            board.startNodeId=map.startNode;
            board.gridSize=2.2f;
            var terrain=Group("01_LAND_AND_WATER - edit models here",root);
            var rails=Group("02_TRACKS",root);rails.name="02_TRACKS";
            var stops=Group("03_STATIONS - move these on the grid",root);
            var towns=Group("04_TREES_AND_BUILDINGS - edit freely",root);
            var pieces=Group("05_TRAINS_DICE_AND_MARKERS",root);
            var nodeMap=new Dictionary<int,SceneRailNode>();
            int xMin=map.nodes.Min(n=>n.x),xMax=map.nodes.Max(n=>n.x);
            int zMin=map.nodes.Min(n=>n.z),zMax=map.nodes.Max(n=>n.z);
            float size=board.gridSize;
            Shape(PrimitiveType.Cube,"Sea / water base",terrain,
                new Vector3((xMin+xMax)*size*.5f,-1.02f,(zMin+zMax)*size*.5f),
                new Vector3((xMax-xMin+10)*size,1.65f,(zMax-zMin+10)*size),
                "Ocean_Turquoise",new Color(.12f,.69f,.79f));

            // Terrain uses a modest radius; the big scene is not a single flat green rectangle.
            var land=new HashSet<Vector2Int>();
            foreach(var n in map.nodes)
            {
                if(n.overWater) continue;
                land.Add(new Vector2Int(n.x,n.z));
                for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)
                    if(Hash(n.x+dx,n.z+dz,7)%5!=0)
                        land.Add(new Vector2Int(n.x+dx,n.z+dz));
            }
            foreach(var cell in land)
            {
                var center=new Vector3(cell.x*size,-.26f,cell.y*size);
                bool coast=!land.Contains(cell+Vector2Int.up) ||
                    !land.Contains(cell+Vector2Int.right) ||
                    !land.Contains(cell+Vector2Int.down) ||
                    !land.Contains(cell+Vector2Int.left);
                Shape(PrimitiveType.Cube,"Land "+cell.x+","+cell.y,terrain,center,
                    new Vector3(size*.995f,.55f,size*.995f),
                    coast?"Beach_Sand":"Island_Grass",coast?Sand:Grass);
            }

            foreach(var n in map.nodes)
            {
                var anchor=Group("STOP "+n.id.ToString("D3")+" / "+
                    (string.IsNullOrEmpty(n.name)?"Rail":n.name),stops);
                anchor.position=RailGraph.Position(n);
                var node=anchor.gameObject.AddComponent<SceneRailNode>();
                node.nodeId=n.id;node.stationName=n.name;node.kind=n.kind;
                node.price=n.price;node.income=n.income;node.overWater=n.overWater;
                Shape(PrimitiveType.Cube,"Ivory station rim",anchor,
                    new Vector3(0,.21f,0),new Vector3(1.32f,.12f,1.32f),
                    "Track_Ivory",White);
                string tone=n.kind==TileKind.Property?"Station_Purple" :
                    n.kind==TileKind.Card?"Card_Yellow" :
                    n.kind==TileKind.Bonus?"Bonus_Blue" :
                    n.kind==TileKind.Penalty?"Penalty_Red":"Rail_Blue";
                Color color=n.kind==TileKind.Property?new Color(.73f,.43f,.90f) :
                    n.kind==TileKind.Card?new Color(.98f,.70f,.20f) :
                    n.kind==TileKind.Bonus?new Color(.24f,.70f,.88f) :
                    n.kind==TileKind.Penalty?new Color(.93f,.31f,.37f):
                    new Color(.22f,.53f,.93f);
                node.tileRenderer=Shape(PrimitiveType.Cube,"EDIT - colored square",anchor,
                    new Vector3(0,.30f,0),new Vector3(1.12f,.09f,1.12f),
                    tone,color).GetComponent<Renderer>();
                if(n.kind==TileKind.Property)
                {
                    Shape(PrimitiveType.Cube,"Station small building",anchor,
                        new Vector3(0,.54f,0),new Vector3(.43f,.35f,.43f),
                        "House_Ivory",White);
                    Shape(PrimitiveType.Cube,"Station roof",anchor,
                        new Vector3(0,.76f,0),new Vector3(.59f,.12f,.59f),
                        "Roof_Red",new Color(.83f,.35f,.34f));
                }
                if(!string.IsNullOrEmpty(n.name))
                {
                    bool northern=graph.Neighbors(n.id).Any(i=>graph.nodes[i].z>n.z);
                    bool southern=graph.Neighbors(n.id).Any(i=>graph.nodes[i].z<n.z);
                    float direction=!northern?1f:!southern?-1f:(n.id%2==0?1f:-1f);
                    var label=Group("FLAT station name / drag on board",anchor);
                    label.localPosition=new Vector3(0,.32f,direction*1.22f);
                    node.flatNamePlate=label;
                    Shape(PrimitiveType.Cube,"White label plaque",label,
                        Vector3.zero,new Vector3(Mathf.Max(1.42f,n.name.Length*.23f),.055f,.57f),
                        "Label_White",new Color(.99f,.99f,.95f));
                    var txt=Group("Station name (flat)",label);
                    txt.localPosition=new Vector3(0,.049f,0);
                    txt.localRotation=Quaternion.Euler(-90,0,0); // visible from above
                    var text=txt.gameObject.AddComponent<TextMesh>();
                    text.text=GameLanguage.City(n.name);
                    text.anchor=TextAnchor.MiddleCenter;
                    text.alignment=TextAlignment.Center;
                    text.characterSize=.18f;text.fontSize=55;
                    text.fontStyle=FontStyle.Bold;
                    text.color=new Color(.10f,.13f,.17f);
                    var font=GameLanguage.JapaneseFont;
                    if(font!=null)
                    {
                        text.font=font;
                        txt.gameObject.GetComponent<MeshRenderer>().sharedMaterial=font.material;
                    }
                    node.labelText=text;
                }
                nodeMap.Add(n.id,node);
            }
            foreach(var e in map.edges)
                MakeTrack(nodeMap[e.a],nodeMap[e.b],rails,size);

            // Lightweight miniature town clusters, all saved in the Scene hierarchy.
            foreach(var n in map.nodes.Where(n=>!string.IsNullOrWhiteSpace(n.name)))
                for(int i=0;i<3;i++)
                {
                    int dx=(int)(Hash(n.x+i,n.z,9)%5)-2;
                    int dz=(int)(Hash(n.x,n.z+i,11)%5)-2;
                    if(dx==0&&dz==0)continue;
                    var place=new Vector2Int(n.x+dx,n.z+dz);
                    if(!land.Contains(place) || map.nodes.Any(item=>item.x==place.x&&item.z==place.y))
                        continue;
                    var decorative=Group("EDIT scenery / "+n.name+" / "+i,towns);
                    decorative.position=new Vector3(place.x*size+.37f,.08f,place.y*size+.33f);
                    if(Hash(place.x,place.y,i)%3==0)
                    {
                        Shape(PrimitiveType.Cylinder,"Tree trunk",decorative,
                            new Vector3(0,.38f,0),new Vector3(.17f,.36f,.17f),
                            "Bark_Brown",new Color(.51f,.35f,.20f));
                        Shape(PrimitiveType.Sphere,"Leaf crown",decorative,
                            new Vector3(0,.94f,0),new Vector3(.86f,.84f,.83f),
                            "Tree_Green",new Color(.20f,.58f,.28f));
                    }
                    else
                    {
                        Shape(PrimitiveType.Cube,"House body",decorative,
                            new Vector3(0,.34f,0),new Vector3(.75f,.68f,.72f),
                            "House_Ivory",White);
                        Shape(PrimitiveType.Cube,"Roof",decorative,
                            new Vector3(0,.75f,0),new Vector3(.90f,.16f,.85f),
                            i%2==0?"Roof_Red":"Roof_Blue",
                            i%2==0?new Color(.83f,.35f,.34f):new Color(.29f,.52f,.81f));
                    }
                }

            for(int i=0;i<4;i++)
            {
                var train=Group("PLAYER "+(i+1)+" / EDIT MODEL",pieces);
                train.position=RailGraph.Position(map.nodes[map.startNode])+Vector3.up*.43f;
                board.playerTrains[i]=train;
                Shape(PrimitiveType.Cube,"Engine body",train,new Vector3(0,.33f,0),
                    new Vector3(.87f,.61f,1.1f),"Train_"+i,TeamColors[i]);
                Shape(PrimitiveType.Cube,"Cab roof",train,new Vector3(0,.72f,-.04f),
                    new Vector3(.84f,.17f,1.08f),"Track_Ivory",White);
                Shape(PrimitiveType.Cube,"Front window",train,new Vector3(0,.47f,.565f),
                    new Vector3(.59f,.26f,.04f),"Window_Cyan",new Color(.19f,.64f,.81f));
                Shape(PrimitiveType.Cube,"Headlight",train,new Vector3(0,.16f,.59f),
                    new Vector3(.22f,.16f,.06f),"Card_Yellow",new Color(.98f,.70f,.20f));
                train.gameObject.SetActive(false);
            }
            var flag=Group("GOAL FLAG / EDIT MODEL",pieces);
            Shape(PrimitiveType.Cylinder,"Flag pole",flag,new Vector3(0,.44f,0),
                new Vector3(.08f,.43f,.08f),"Bark_Brown",new Color(.51f,.35f,.20f));
            Shape(PrimitiveType.Cube,"Goal marker",flag,new Vector3(.26f,.73f,0),
                new Vector3(.58f,.27f,.08f),"Card_Yellow",new Color(.98f,.70f,.20f));
            board.goalMarker=flag;flag.gameObject.SetActive(false);
            var ghost=Group("POOR GHOST / EDIT MODEL",pieces);
            Shape(PrimitiveType.Sphere,"Ghost head",ghost,new Vector3(0,.39f,0),
                new Vector3(.68f,.70f,.68f),"Label_White",new Color(.99f,.99f,.95f));
            for(int side=-1;side<=1;side+=2)
                Shape(PrimitiveType.Sphere,"Ghost eye",ghost,new Vector3(side*.14f,.47f,.29f),
                    new Vector3(.10f,.15f,.09f),"Sleeper_Black",Dark);
            board.ghostMarker=ghost;ghost.gameObject.SetActive(false);
            board.diceVisual=MakeDie(pieces);

            var cameraGo=new GameObject("FOLLOW CAMERA / adjust Orthographic Size");
            var camera=cameraGo.AddComponent<Camera>();
            cameraGo.tag="MainCamera";
            cameraGo.AddComponent<AudioListener>();
            camera.orthographic=true;
            camera.orthographicSize=6.1f;
            camera.nearClipPlane=.1f;
            camera.farClipPlane=300f;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.17f,.74f,.83f);
            Vector3 start=RailGraph.Position(map.nodes[map.startNode]);
            cameraGo.transform.position=start+new Vector3(1.4f,10.5f,-10.7f);
            cameraGo.transform.LookAt(start);
            board.followCamera=camera;

            var sun=new GameObject("SUNLIGHT");
            var light=sun.AddComponent<Light>();
            light.type=LightType.Directional;
            light.intensity=1.3f;
            light.color=new Color(1f,.97f,.87f);
            sun.transform.rotation=Quaternion.Euler(55,-35,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.71f,.78f,.78f);

            EditorSceneManager.SaveScene(scene,ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject=root.gameObject;
            SceneView.FrameLastActiveSceneView();
            Debug.Log("Editable Scene saved: "+ScenePath+" | "+map.nodes.Count+
                " stops, "+map.edges.Count+" tracks. Press Play or move any object in Scene view.");
        }

        private static SceneRailSegment MakeTrack(SceneRailNode a,SceneRailNode b,
            Transform parent,float grid)
        {
            var go=Group("TRACK "+a.nodeId.ToString("D3")+" - "+b.nodeId.ToString("D3"),parent);
            var edge=go.gameObject.AddComponent<SceneRailSegment>();
            edge.from=a;edge.to=b;
            Shape(PrimitiveType.Cube,"White track base",go,new Vector3(0,.14f,0),
                new Vector3(.73f,.11f,grid),"Track_Ivory",White);
            for(int i=0;i<7;i++)
                Shape(PrimitiveType.Cube,"Black sleeper "+i,go,new Vector3(0,.205f,(i-3)*(grid/7f)),
                    new Vector3(.70f,.045f,.13f),"Sleeper_Black",Dark);
            for(int side=-1;side<=1;side+=2)
                Shape(PrimitiveType.Cube,"Steel rail "+side,go,
                    new Vector3(side*.23f,.248f,0),
                    new Vector3(.047f,.048f,grid),"Metal_Rail",RailGray);
            var line=Shape(PrimitiveType.Cube,"Hidden shortest path",go,
                new Vector3(0,.286f,0),new Vector3(.08f,.055f,grid*.96f),
                "Card_Yellow",new Color(.98f,.70f,.20f));
            edge.routeHighlight=line;
            line.SetActive(false);
            edge.RefreshTransform();
            return edge;
        }
        private static Transform MakeDie(Transform parent)
        {
            var root=Group("3D DICE / STATIC EDITABLE MODEL",parent);
            Shape(PrimitiveType.Cube,"Ivory die",root,Vector3.zero,
                Vector3.one*1.24f,"Track_Ivory",White);
            Vector3[] faces={Vector3.up,Vector3.forward,Vector3.right,
                Vector3.left,Vector3.back,Vector3.down};
            int[][] patterns={new[]{4},new[]{0,8},new[]{0,4,8},
                new[]{0,2,6,8},new[]{0,2,4,6,8},new[]{0,2,3,5,6,8}};
            for(int i=0;i<6;i++)
            {
                Quaternion turn=Quaternion.FromToRotation(Vector3.forward,faces[i]);
                foreach(int cell in patterns[i])
                {
                    int column=cell%3-1,row=1-cell/3;
                    var dot=Shape(PrimitiveType.Sphere,"Face "+(i+1)+" pip",root,
                        faces[i]*.66f+turn*new Vector3(column*.32f,row*.32f,0),
                        new Vector3(.15f,.15f,.06f),"Sleeper_Black",Dark);
                    dot.transform.localRotation=turn;
                }
            }
            root.gameObject.SetActive(false);
            return root;
        }
    }
}
#endif
