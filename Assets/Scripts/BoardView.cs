using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RailTrip3D
{
    /// <summary>
    /// Runtime view ONLY binds existing serialized Scene objects.
    /// It never creates scenery, rails, station labels, train meshes or the camera.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public Camera mapCamera;
        private SceneRailBoard authored;
        private RailGraph graph;
        private MatchData snapshot;
        private readonly Dictionary<int,SceneRailNode> nodes = new Dictionary<int,SceneRailNode>();
        private readonly Dictionary<long,SceneRailSegment> segments = new Dictionary<long,SceneRailSegment>();
        private readonly List<Transform> trains = new List<Transform>();
        private readonly List<Vector3> headings = new List<Vector3>();
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private Vector3 cameraFocus;
        private Vector3 cameraOffset;
        private bool hasCameraOffset;
        private int activeSeat;
        private bool initialized;
        public static readonly Color[] PlayerColors = {
            new Color(.19f,.75f,.95f), new Color(1f,.43f,.55f),
            new Color(1f,.76f,.19f), new Color(.63f,.43f,.92f)
        };
        public RailGraph Graph => graph;
        public Vector3 NodePosition(int id) => nodes.TryGetValue(id,out var n)
            ? n.transform.position : Vector3.zero;
        private static long EdgeKey(int a,int b) =>
            ((long)Mathf.Min(a,b)<<32) | (uint)Mathf.Max(a,b);

        public void Build(RailMap map)
        {
            initialized = false;
            authored = FindFirstObjectByType<SceneRailBoard>();
            if (authored == null) throw new InvalidOperationException(
                "Main.unity has no editable scene. Choose Rail Trip > Rebuild Editable Scene from Asset in the Unity Editor.");
            var local = authored.Export();
            if (local.nodes.Count != map.nodes.Count || local.edges.Count != map.edges.Count ||
                local.nodes.Any(n => map.nodes[n.id].x != n.x || map.nodes[n.id].z != n.z))
                throw new InvalidOperationException(
                    "Online map differs from this saved Scene. Use the same authored Scene on every client.");
            graph = new RailGraph(map);
            nodes.Clear(); segments.Clear(); trains.Clear(); headings.Clear();
            foreach(var n in authored.Nodes)
            {
                nodes.Add(n.nodeId,n);
                n.RefreshLabel();
            }
            foreach(var e in authored.Segments)
                segments.Add(EdgeKey(e.from.nodeId,e.to.nodeId),e);
            for(int i=0;i<authored.playerTrains.Length;i++)
            {
                if(authored.playerTrains[i] == null) throw new InvalidOperationException(
                    "Player train "+i+" is missing from the authored Scene");
                trains.Add(authored.playerTrains[i]);
                headings.Add(Vector3.forward);
                trains[i].gameObject.SetActive(false);
            }
            mapCamera = authored.followCamera;
            if(mapCamera == null) throw new InvalidOperationException("Follow Camera is missing from the Scene");
            // The saved Camera Transform in Main.unity is editable by the developer.
            // Read its authored offset only once, so restarting a match doesn't drift.
            if(!hasCameraOffset)
            {
                cameraOffset=mapCamera.transform.position-NodePosition(authored.startNodeId);
                hasCameraOffset=true;
            }
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize,4.7f,10f);
            if(authored.ghostMarker!=null) authored.ghostMarker.gameObject.SetActive(false);
            if(authored.goalMarker!=null) authored.goalMarker.gameObject.SetActive(false);
            cameraFocus = NodePosition(map.startNode);
            initialized = true;
        }
        private static void SetColor(Renderer r,Color color,MaterialPropertyBlock b)
        {
            if(r == null)return;
            r.GetPropertyBlock(b);
            b.SetColor("_BaseColor",color);
            b.SetColor("_Color",color);
            r.SetPropertyBlock(b);
        }
        public void Show(MatchData state)
        {
            if(!initialized || state == null || state.players.Count == 0)return;
            snapshot=state;
            activeSeat=Mathf.Clamp(state.currentSeat,0,state.players.Count-1);
            for(int i=0;i<trains.Count;i++)
            {
                bool present=i<state.players.Count;
                trains[i].gameObject.SetActive(present);
                if(!present)continue;
                if(!trains[i].gameObject.activeSelf)continue;
                if(Vector3.Distance(trains[i].position,NodePosition(state.players[i].nodeId))>25f)
                    trains[i].position=NodePosition(state.players[i].nodeId)+Vector3.up*.43f;
            }
            foreach(var n in state.board.nodes)
                if(nodes.TryGetValue(n.id,out var marker))
                    SetColor(marker.tileRenderer,n.owner < 0
                        ? ColorFor(n.kind) : PlayerColors[Mathf.Clamp(n.owner,0,3)],block);
            var shortest=new HashSet<long>();
            if(state.started)
            {
                var path=graph.ShortestPath(state.players[activeSeat].nodeId,state.destination);
                for(int i=1;i<path.Count;i++) shortest.Add(EdgeKey(path[i-1],path[i]));
            }
            foreach(var e in segments)
                if(e.Value.routeHighlight!=null)
                    e.Value.routeHighlight.SetActive(shortest.Contains(e.Key));
            if(authored.goalMarker != null)
            {
                authored.goalMarker.gameObject.SetActive(state.started);
                if(state.started) authored.goalMarker.position=NodePosition(state.destination)+Vector3.up*1.25f;
            }
            bool ghostVisible=state.started && state.ghostSeat>=0 && state.ghostSeat<state.players.Count;
            if(authored.ghostMarker!=null)
            {
                authored.ghostMarker.gameObject.SetActive(ghostVisible);
                if(ghostVisible)authored.ghostMarker.position=
                    NodePosition(state.players[state.ghostSeat].nodeId)+Vector3.up*1.3f;
            }
        }
        private static Color ColorFor(TileKind kind)
        {
            switch(kind)
            {
                case TileKind.Property: return new Color(.72f,.41f,.92f);
                case TileKind.Card: return new Color(.99f,.72f,.19f);
                case TileKind.Bonus: return new Color(.23f,.68f,.88f);
                case TileKind.Penalty: return new Color(.94f,.36f,.38f);
                case TileKind.Station: return new Color(.31f,.70f,.91f);
                default: return new Color(.25f,.53f,.89f);
            }
        }
        private void LateUpdate()
        {
            if(!initialized || snapshot==null || mapCamera==null) return;
            for(int i=0;i<snapshot.players.Count && i<trains.Count;i++)
            {
                Vector3 goal=NodePosition(snapshot.players[i].nodeId)+Vector3.up*.43f;
                Vector3 old=trains[i].position;
                trains[i].position=Vector3.MoveTowards(old,goal,Time.deltaTime*7.9f);
                Vector3 heading=goal-old; heading.y=0;
                if(heading.sqrMagnitude>.0002f)
                {
                    headings[i]=heading.normalized;
                    trains[i].rotation=Quaternion.Slerp(trains[i].rotation,
                        Quaternion.LookRotation(headings[i],Vector3.up),
                        1f-Mathf.Exp(-14f*Time.deltaTime));
                }
            }
            Transform current=trains[Mathf.Clamp(activeSeat,0,trains.Count-1)];
            Vector3 ahead=headings[activeSeat]*.82f;
            Vector3 wanted=current.position+ahead+new Vector3(0,.13f,.6f);
            cameraFocus=Vector3.Lerp(cameraFocus,wanted,1f-Mathf.Exp(-6.5f*Time.deltaTime));
            Vector3 eye=cameraFocus+cameraOffset;
            mapCamera.transform.position=Vector3.Lerp(mapCamera.transform.position,
                eye,1f-Mathf.Exp(-7.0f*Time.deltaTime));
            mapCamera.transform.LookAt(cameraFocus);
            // Distance culling is only for signage, not for scene model placement.
            foreach(var n in nodes.Values)
                if(n.flatNamePlate != null)
                {
                    bool visible=!string.IsNullOrWhiteSpace(n.stationName) &&
                        Vector2.Distance(new Vector2(n.transform.position.x,n.transform.position.z),
                            new Vector2(cameraFocus.x,cameraFocus.z))<11f;
                    if(n.flatNamePlate.gameObject.activeSelf!=visible)
                        n.flatNamePlate.gameObject.SetActive(visible);
                }
        }
        public void Zoom(float delta)
        {
            if(mapCamera!=null)
                mapCamera.orthographicSize=Mathf.Clamp(
                    mapCamera.orthographicSize-delta*.48f,4.7f,9.8f);
        }
    }
}
