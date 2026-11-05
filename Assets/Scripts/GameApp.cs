using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RailTrip3D
{
    public class GameApp : MonoBehaviour
    {
        public RailMapAsset mapAsset;
        public BoardView boardView;
        public OnlineClient network;
        public GameHud hud;
        public MatchData match;
        public bool isOnline;
        public string status = "";
        private RailMap configuredMap;
        private float nextAiMove;
        private bool pendingCreateOrJoin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureApp()
        {
            if (FindFirstObjectByType<GameApp>() == null)
                new GameObject("Rail Trip 3D / Game").AddComponent<GameApp>();
        }
        private void Awake()
        {
            Application.targetFrameRate = 60;
            if (mapAsset == null) mapAsset = Resources.Load<RailMapAsset>("Maps/ActiveMap");
            configuredMap = mapAsset != null ? mapAsset.RuntimeCopy() : SampleMap.Create();
            try
            {
                string validation = new RailGraph(configuredMap).Validate();
                if (validation != "") { Debug.LogError("Map invalid: " + validation + ", using sample"); configuredMap = SampleMap.Create(); }
            }
            catch (System.Exception ex) { Debug.LogError(ex); configuredMap = SampleMap.Create(); }
            boardView = gameObject.AddComponent<BoardView>();
            network = gameObject.AddComponent<OnlineClient>();
            hud = gameObject.AddComponent<GameHud>();
            hud.app = this;
            network.serverUrl = PlayerPrefs.GetString("railtrip_server", "");
            network.OnSnapshot += ReceivedNetworkState;
            StartOffline();
        }
        public void StartOffline()
        {
            network.Disconnect();
            isOnline = false; pendingCreateOrJoin = false;
            match = GameRules.NewMatch(configuredMap, "YOU", "CPU RIN", "CPU KAI");
            GameRules.Apply(match, 0, "start");
            boardView.Build(match.board);
            boardView.Show(match);
            status = "Local game: YOU vs two computer players";
            hud.onlineMenu = false;
            nextAiMove = Time.time + 1;
        }
        public void SaveEndpoint(string endpoint)
        {
            network.serverUrl = endpoint.Trim();
            PlayerPrefs.SetString("railtrip_server", network.serverUrl);
            PlayerPrefs.Save();
        }
        public void CreateOnline(string name)
        {
            if (string.IsNullOrWhiteSpace(network.serverUrl)) { status = "Set the Worker URL first."; return; }
            pendingCreateOrJoin = true;
            network.CreateRoom(name, configuredMap);
            status = "Creating room...";
        }
        public void JoinOnline(string code, string name)
        {
            if (string.IsNullOrWhiteSpace(network.serverUrl)) { status = "Set the Worker URL first."; return; }
            pendingCreateOrJoin = true;
            network.JoinRoom(code, name);
            status = "Joining " + code + "...";
        }
        private void ReceivedNetworkState(MatchData state)
        {
            if (state == null || state.board == null) return;
            if (!isOnline || match == null || match.board.nodes.Count != state.board.nodes.Count)
                boardView.Build(state.board);
            isOnline = true;
            pendingCreateOrJoin = false;
            match = state;
            boardView.Show(match);
            hud.onlineMenu = false;
            status = "Online room " + network.roomCode;
        }
        public int MySeat => isOnline ? network.seat : 0;
        public bool MyTurn => match != null && match.started && match.currentSeat == MySeat && match.phase != "finished";
        public void Request(string action, int node = -1, int card = -1)
        {
            if (match == null) return;
            if (isOnline) { network.Act(action, node, card); return; }
            bool changed = GameRules.Apply(match, 0, action, node, card);
            if (changed) boardView.Show(match);
        }
        public void MoveDirection(int dx, int dz)
        {
            if (!MyTurn || match.phase != "moving") return;
            var graph = boardView.Graph;
            int at = match.players[MySeat].nodeId;
            foreach (int dest in graph.Neighbors(at))
            {
                var a = graph.nodes[at]; var b = graph.nodes[dest];
                if (b.x - a.x != dx || b.z - a.z != dz) continue;
                if (dest == match.previousNode && graph.Neighbors(at).Count > 1) return;
                Request("move", dest); return;
            }
        }
        private void Update()
        {
            if (match == null) return;
            if (!isOnline && !MyTurn && match.phase != "finished" && Time.time >= nextAiMove)
            { nextAiMove = Time.time + .32f; RunAi(); }
            if (Input.GetKeyDown(KeyCode.M)) hud.showMiniMap = !hud.showMiniMap;
            if (Input.mouseScrollDelta.y != 0) boardView.Zoom(Input.mouseScrollDelta.y * 1.6f);
            if (hud.onlineMenu || !MyTurn) return;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MoveDirection(0, 1);
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MoveDirection(0, -1);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveDirection(-1, 0);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveDirection(1, 0);
            if (Input.GetKeyDown(KeyCode.Space) && match.phase == "ready") Request("roll");
            if (Input.GetKeyDown(KeyCode.Alpha1)) Request("card", -1, 0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Request("card", -1, 1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Request("card", -1, 2);
        }
        private void RunAi()
        {
            var graph = boardView.Graph;
            int seat = match.currentSeat;
            if (seat == 0) return;
            var p = match.players[seat];
            if (match.phase == "ready")
            {
                if (p.cards.Count > 0 && p.cards[0] == "CASH") GameRules.Apply(match, seat, "card", -1, 0);
                else GameRules.Apply(match, seat, "roll");
            }
            else if (match.phase == "moving")
            {
                var options = graph.Neighbors(p.nodeId).Where(id =>
                    id != match.previousNode || graph.Neighbors(p.nodeId).Count <= 1).ToList();
                if (options.Count > 0)
                {
                    int chosen = options.OrderBy(n => graph.Distance(n, match.destination)).ThenBy(n => n).First();
                    GameRules.Apply(match, seat, "move", chosen);
                }
            }
            else if (match.phase == "buy")
            {
                var node = graph.nodes[p.nodeId];
                GameRules.Apply(match, seat, p.cash > node.price + 180 ? "buy" : "skip");
            }
            boardView.Show(match);
        }
    }
}
