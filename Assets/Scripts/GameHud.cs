using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RailTrip3D
{
    // Resolution-independent immediate-mode HUD. Intentional original visual design.
    public class GameHud : MonoBehaviour
    {
        public GameApp app;
        public bool onlineMenu, showMiniMap = true;
        public string playerName = "PLAYER", roomInput = "", endpoint = "";
        private Texture2D panelTex, buttonTex, hotTex, redTex, paleTex;
        private GUIStyle panel, button, hotButton, warnButton, lineStyle;
        private GUIStyle header, body, small, muted, big, center, textField;
        private bool stylesReady;
        private const float CanvasW = 1280, CanvasH = 720;

        private static Texture2D RoundTexture(Color c)
        {
            int size = 40, radius = 11;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x, radius, size - radius - 1);
                    float cy = Mathf.Clamp(y, radius, size - radius - 1);
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(radius - d + 1)));
                }
            tex.Apply(); return tex;
        }
        private static GUIStyle Rounded(Texture2D tex)
        {
            return new GUIStyle(GUI.skin.box) { normal = { background = tex },
                border = new RectOffset(11,11,11,11), padding = new RectOffset(12,12,12,12) };
        }
        private static GUIStyle Text(int size, Color color, FontStyle font = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleLeft)
        {
            return new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = font, alignment = align,
                normal = { textColor = color }, wordWrap = true };
        }
        private void Setup()
        {
            if (stylesReady) return;
            panelTex = RoundTexture(new Color(.065f,.11f,.18f,.94f));
            buttonTex = RoundTexture(new Color(.16f,.26f,.34f,1));
            hotTex = RoundTexture(new Color(1f,.72f,.20f,1));
            redTex = RoundTexture(new Color(.72f,.28f,.34f,1));
            paleTex = RoundTexture(new Color(.13f,.19f,.28f,.95f));
            panel = Rounded(panelTex); lineStyle = Rounded(paleTex);
            button = new GUIStyle(GUI.skin.button) { normal = { background = buttonTex, textColor = Color.white },
                hover = { background = buttonTex, textColor = Color.white },
                active = { background = buttonTex, textColor = Color.white },
                border = new RectOffset(11,11,11,11), fontSize = 17, fontStyle = FontStyle.Bold };
            hotButton = new GUIStyle(button) { normal = { background = hotTex, textColor = new Color(.11f,.16f,.22f) },
                hover = { background = hotTex, textColor = new Color(.11f,.16f,.22f) },
                active = { background = hotTex, textColor = new Color(.11f,.16f,.22f) } };
            warnButton = new GUIStyle(button) { normal = { background = redTex, textColor = Color.white } };
            header = Text(24, Color.white, FontStyle.Bold);
            body = Text(17, Color.white);
            small = Text(13, new Color(.88f,.92f,.96f));
            muted = Text(14, new Color(.65f,.75f,.83f));
            big = Text(32, new Color(1f,.8f,.3f), FontStyle.Bold);
            center = Text(18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            textField = new GUIStyle(GUI.skin.textField) { fontSize = 17,
                normal = { background = buttonTex, textColor = Color.white },
                focused = { background = buttonTex, textColor = Color.white },
                border = new RectOffset(10,10,10,10), padding = new RectOffset(12,12,6,6) };
            endpoint = app.network.serverUrl;
            stylesReady = true;
        }
        private static void RectFill(Rect r, Color color)
        {
            Color old = GUI.color; GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
        private void Label(Rect rect, string value, GUIStyle style) => GUI.Label(rect, value ?? "", style);
        private bool Btn(Rect r, string text, bool active = false) => GUI.Button(r, text, active ? hotButton : button);
        private void OnGUI()
        {
            if (app == null || app.match == null || app.boardView == null) return;
            Setup();
            var previousMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / CanvasW, Screen.height / CanvasH);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - CanvasW * scale) / 2, (Screen.height - CanvasH * scale) / 2, 0),
                Quaternion.identity, Vector3.one * scale);
            DrawMain();
            if (onlineMenu) DrawOnline();
            GUI.matrix = previousMatrix;
        }
        private void DrawMain()
        {
            var s = app.match;
            GUI.Box(new Rect(18, 14, 1244, 72), "", panel);
            Label(new Rect(36, 21, 300, 30), "RAIL TRIP 3D", header);
            Label(new Rect(36, 54, 280, 19), app.isOnline ? "ONLINE  /  " + app.network.roomCode : "LOCAL  /  VS CPU", muted);
            string goal = s.started && app.boardView.Graph.nodes.ContainsKey(s.destination)
                ? app.boardView.Graph.nodes[s.destination].name : "—";
            Label(new Rect(370, 21, 100, 26), "DESTINATION", muted);
            Label(new Rect(470, 18, 300, 42), goal.ToUpperInvariant(), big);
            Label(new Rect(790, 21, 160, 25), s.started ? "TURN " + (s.turnCount + 1) : "LOBBY", body);
            if (Btn(new Rect(1006, 28, 116, 43), "ONLINE")) onlineMenu = true;
            if (Btn(new Rect(1132, 28, 110, 43), app.isOnline ? "LEAVE" : "RESTART"))
                app.StartOffline();

            GUI.Box(new Rect(18, 99, 266, 392), "", panel);
            Label(new Rect(35, 114, 235, 29), "PLAYERS", header);
            for (int i = 0; i < s.players.Count; i++)
            {
                PlayerData p = s.players[i];
                int y = 157 + i * 93;
                GUI.Box(new Rect(31, y, 240, 81), "", lineStyle);
                RectFill(new Rect(42, y + 12, 8, 56), BoardView.PlayerColors[i]);
                Label(new Rect(59, y + 10, 164, 23), (i == s.currentSeat ? "● " : "") + p.name, body);
                Label(new Rect(59, y + 34, 170, 20), "¥ " + p.cash.ToString("N0") + " M", body);
                Label(new Rect(59, y + 56, 202, 18), "ASSETS: " + GameRules.Wealth(s, i).ToString("N0"), small);
                if (i == s.ghostSeat && s.started)
                    Label(new Rect(222, y + 4, 32, 27), "👻", body);
            }
            GUI.Box(new Rect(18, 504, 266, 198), "", panel);
            Label(new Rect(35, 517, 230, 25), "HOW TO PLAY", header);
            Label(new Rect(35, 553, 235, 130),
                "SPACE: roll the dice\n↑ ↓ ← →: travel the rails\n1 / 2 / 3: play cards\nM: toggle overview\nMouse wheel: zoom camera",
                small);

            GUI.Box(new Rect(294, 504, 668, 198), "", panel);
            DrawControls(new Rect(307, 512, 638, 181));

            if (showMiniMap) DrawMiniMap(new Rect(972, 99, 290, 603));
            else
            {
                GUI.Box(new Rect(972, 99, 290, 603), "", panel);
                Label(new Rect(988, 119, 260, 32), "OVERVIEW HIDDEN", header);
                if (Btn(new Rect(995, 175, 244, 46), "SHOW MAP (M)")) showMiniMap = true;
            }
            // transient event banner
            GUI.Box(new Rect(294, 419, 668, 72), "", panel);
            Label(new Rect(313, 429, 632, 53), s.message, body);
            if (!string.IsNullOrEmpty(app.network.lastError))
                Label(new Rect(305, 387, 656, 25), app.network.lastError, Text(14, new Color(1,.47f,.47f)));
            else if (!string.IsNullOrEmpty(app.status))
                Label(new Rect(305, 392, 656, 22), app.status, small);
        }
        private void DrawControls(Rect area)
        {
            var s = app.match;
            if (s.phase == "finished")
            {
                Label(new Rect(329, 529, 560, 52), s.players[s.winnerSeat].name + " WINS THE MATCH!", big);
                if (Btn(new Rect(331, 610, 300, 60), "PLAY AGAIN", true)) app.StartOffline();
                return;
            }
            if (!s.started)
            {
                Label(new Rect(323, 520, 610, 35), "INVITE FRIENDS TO THE ROOM", header);
                Label(new Rect(323, 563, 610, 28), "Players joined: " + s.players.Count + " / 4", body);
                if (app.isOnline && app.network.seat == 0 && Btn(new Rect(330, 610, 295, 62), "START GAME", true))
                    app.Request("start");
                else Label(new Rect(330, 611, 500, 40), "The host can start the game.", body);
                return;
            }
            bool ownTurn = app.MyTurn;
            if (!ownTurn)
            {
                Label(new Rect(325, 535, 610, 42), "WAITING FOR " + s.players[s.currentSeat].name, header);
                Label(new Rect(325, 585, 610, 29), "Follow the highlighted route to the gold flag.", body);
                return;
            }
            var p = s.players[app.MySeat];
            if (s.phase == "ready")
            {
                Label(new Rect(322, 515, 260, 30), "YOUR TURN  •  " + p.name, header);
                if (Btn(new Rect(326, 553, 220, 56), "ROLL DICE", true)) app.Request("roll");
                Label(new Rect(565, 552, 335, 22), "CARDS  •  click or press 1 / 2 / 3", small);
                for (int i = 0; i < p.cards.Count; i++)
                    if (Btn(new Rect(563 + i * 118, 581, 108, 45), CardName(p.cards[i])))
                        app.Request("card", -1, i);
                Label(new Rect(326, 625, 610, 40), "Extra dice steps: +" + p.diceBoost + "   |   Shield: " + p.shield, muted);
            }
            else if (s.phase == "moving")
            {
                Label(new Rect(318, 515, 280, 37), "STEPS LEFT  " + s.remaining + " / " + s.rolled, header);
                int baseX = 646, baseY = 523;
                DirectionButton(new Rect(baseX + 56, baseY, 72, 50), "↑", 0, 1);
                DirectionButton(new Rect(baseX - 18, baseY + 53, 72, 50), "←", -1, 0);
                DirectionButton(new Rect(baseX + 56, baseY + 53, 72, 50), "↓", 0, -1);
                DirectionButton(new Rect(baseX + 130, baseY + 53, 72, 50), "→", 1, 0);
                Label(new Rect(322, 570, 288, 92), "Choose a connected direction.\nGolden rails show the\nshortest route to the goal.", body);
            }
            else if (s.phase == "buy")
            {
                var stop = app.boardView.Graph.nodes[p.nodeId];
                Label(new Rect(324, 521, 580, 29), "BUY PROPERTY  •  " + stop.name, header);
                Label(new Rect(324, 551, 550, 27), "Cost " + stop.price + "  /  Income " + stop.income + " per round", body);
                if (Btn(new Rect(324, 592, 244, 69), "BUY PROPERTY", true)) app.Request("buy");
                if (Btn(new Rect(589, 592, 244, 69), "SKIP")) app.Request("skip");
            }
        }
        private static string CardName(string id)
        {
            if (id == "EXPRESS") return "EXPRESS";
            if (id == "CASH") return "+350";
            if (id == "SHIELD") return "SHIELD";
            return id;
        }
        private void DirectionButton(Rect r, string label, int dx, int dz)
        {
            var s = app.match; var graph = app.boardView.Graph;
            int at = s.players[app.MySeat].nodeId;
            bool allowed = graph.Neighbors(at).Any(next =>
                graph.nodes[next].x - graph.nodes[at].x == dx &&
                graph.nodes[next].z - graph.nodes[at].z == dz &&
                (next != s.previousNode || graph.Neighbors(at).Count == 1));
            GUI.enabled = allowed;
            if (Btn(r, label, allowed)) app.MoveDirection(dx, dz);
            GUI.enabled = true;
        }
        private void DrawMiniMap(Rect rect)
        {
            var s = app.match; var graph = app.boardView.Graph;
            GUI.Box(rect, "", panel);
            Label(new Rect(rect.x + 17, rect.y + 14, rect.width - 30, 28), "ROUTE OVERVIEW", header);
            if (Btn(new Rect(rect.x + rect.width - 56, rect.y + 13, 38, 32), "−")) showMiniMap = false;
            Rect plot = new Rect(rect.x + 17, rect.y + 56, rect.width - 34, rect.height - 136);
            RectFill(plot, new Color(.045f,.13f,.18f,.97f));
            int minX = graph.map.nodes.Min(n => n.x), maxX = graph.map.nodes.Max(n => n.x);
            int minZ = graph.map.nodes.Min(n => n.z), maxZ = graph.map.nodes.Max(n => n.z);
            Vector2 Point(int id)
            {
                var n = graph.nodes[id];
                return new Vector2(plot.x + 11 + (n.x - minX) / (float)Mathf.Max(1,maxX - minX) * (plot.width - 22),
                    plot.yMax - 13 - (n.z - minZ) / (float)Mathf.Max(1,maxZ - minZ) * (plot.height - 26));
            }
            var path = graph.ShortestPath(s.players[s.currentSeat].nodeId, s.destination);
            var highlighted = new HashSet<long>();
            for (int i = 1; i < path.Count; i++) highlighted.Add(Key(path[i - 1],path[i]));
            foreach (var e in graph.map.edges)
                Stroke(Point(e.a), Point(e.b), highlighted.Contains(Key(e.a,e.b)) ? new Color(1,.80f,.30f) : new Color(.31f,.49f,.52f),
                    highlighted.Contains(Key(e.a,e.b)) ? 3.5f : 1.5f);
            foreach (var n in graph.map.nodes)
            {
                if (n.kind == TileKind.Rail) continue;
                Vector2 pos = Point(n.id);
                RectFill(new Rect(pos.x - 3.5f, pos.y - 3.5f, 7, 7),
                    n.kind == TileKind.Property ? new Color(.95f,.63f,.29f) : new Color(.62f,.70f,.82f));
            }
            if (s.started)
            {
                Vector2 goal = Point(s.destination);
                RectFill(new Rect(goal.x - 7, goal.y - 7, 14, 14), new Color(1,.78f,.2f));
            }
            for (int i = 0; i < s.players.Count; i++)
            {
                Vector2 pos = Point(s.players[i].nodeId);
                RectFill(new Rect(pos.x - 6 + i * 2, pos.y - 6 + i * 2, 12, 12), BoardView.PlayerColors[i]);
            }
            Label(new Rect(rect.x + 19, rect.yMax - 71, rect.width - 39, 22), "GOLD  =  SHORTEST RAIL PATH", small);
            Label(new Rect(rect.x + 19, rect.yMax - 43, rect.width - 39, 22),
                "GOAL DISTANCE: " + (path.Count > 0 ? (path.Count - 1).ToString() : "N/A") + " steps", body);
        }
        private static long Key(int a,int b) => ((long)Mathf.Min(a,b) << 32) | (uint)Mathf.Max(a,b);
        private static void Stroke(Vector2 start, Vector2 end, Color color, float width)
        {
            Color old = GUI.color; Matrix4x4 original = GUI.matrix;
            Vector2 delta = end - start;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.color = color;
            GUI.DrawTexture(new Rect(start.x, start.y - width / 2, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = original; GUI.color = old;
        }
        private void DrawOnline()
        {
            RectFill(new Rect(0,0,1280,720), new Color(0,.02f,.06f,.79f));
            GUI.Box(new Rect(330, 105, 620, 507), "", panel);
            Label(new Rect(362, 127, 550, 45), "ONLINE MATCHMAKING", header);
            Label(new Rect(362, 182, 550, 26), "Cloudflare Worker URL (deploy the backend first)", small);
            endpoint = GUI.TextField(new Rect(362, 213, 545, 43), endpoint, textField);
            if (Btn(new Rect(362, 266, 545, 41), "SAVE SERVER ADDRESS")) app.SaveEndpoint(endpoint);
            Label(new Rect(362, 314, 245, 24), "DISPLAY NAME", small);
            playerName = GUI.TextField(new Rect(362, 345, 545, 44), playerName, 14, textField);
            if (Btn(new Rect(362, 404, 258, 58), "CREATE ROOM", true))
            { app.SaveEndpoint(endpoint); app.CreateOnline(playerName); }
            roomInput = GUI.TextField(new Rect(637, 405, 128, 54), roomInput.ToUpperInvariant(), 6, textField);
            if (Btn(new Rect(775, 405, 132, 54), "JOIN"))
            { app.SaveEndpoint(endpoint); app.JoinOnline(roomInput, playerName); }
            string notice = !string.IsNullOrEmpty(app.network.lastError) ? app.network.lastError : app.status;
            Label(new Rect(362, 477, 545, 51), notice, small);
            if (Btn(new Rect(362, 543, 545, 44), "BACK TO GAME")) onlineMenu = false;
        }
    }
}
