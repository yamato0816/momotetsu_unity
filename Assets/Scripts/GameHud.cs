using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RailTrip3D
{
    /// <summary>
    /// Minimal close-up HUD: coral cash strip, cream destination placard,
    /// floating context dialogue and a few large teal action buttons.
    /// A full-map overlay replaces the permanently visible minimap.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        public GameApp app;
        public bool onlineMenu, showMiniMap;
        public string playerName = "PLAYER", roomInput = "", endpoint = "";
        private bool showCards, showPlayers;
        private string lastMessage = "";
        private float messageUntil;
        private bool built;
        private GUIStyle coral, cream, dark, glass, modal, btn, primary, danger;
        private GUIStyle heading, body, small, tiny, accent, money, blackText, darkHeading, value, centered;
        private GUIStyle textField;
        private Texture2D pix;
        private const float Width = 1280, Height = 720;
        private Color coralColor = new Color(.94f,.40f,.49f);
        private Color tealColor = new Color(.13f,.67f,.68f);
        private Color nightColor = new Color(.08f,.18f,.22f);
        private Font displayFont;

        private static Texture2D Texture(Color c)
        {
            const int width = 48, radius = 12;
            var tex = new Texture2D(width, width, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int y=0;y<width;y++) for (int x=0;x<width;x++)
            {
                float nx = Mathf.Clamp(x,radius,width-radius-1);
                float ny = Mathf.Clamp(y,radius,width-radius-1);
                float edge = Vector2.Distance(new Vector2(x,y),new Vector2(nx,ny));
                tex.SetPixel(x,y,new Color(c.r,c.g,c.b,c.a*Mathf.Clamp01(radius-edge+1)));
            }
            tex.Apply(); return tex;
        }
        private GUIStyle Panel(Color c, int pad=12)
        {
            return new GUIStyle(GUI.skin.box) {
                normal = { background = Texture(c) },
                border = new RectOffset(12,12,12,12),
                padding = new RectOffset(pad,pad,pad,pad)
            };
        }
        private GUIStyle Txt(int size, Color color, FontStyle style=FontStyle.Normal,
            TextAnchor align=TextAnchor.MiddleLeft)
        {
            var result = new GUIStyle(GUI.skin.label) {
                fontSize=size, fontStyle=style, alignment=align,
                normal={textColor=color}, wordWrap=false
            };
            if (displayFont != null) result.font = displayFont;
            return result;
        }
        private GUIStyle Button(Color bg, Color fg)
        {
            var texture = Texture(bg);
            var style = new GUIStyle(GUI.skin.button) {
                normal = {background=texture,textColor=fg},
                hover = {background=texture,textColor=fg},
                active = {background=texture,textColor=fg},
                focused = {background=texture,textColor=fg},
                border = new RectOffset(12,12,12,12),
                alignment = TextAnchor.MiddleCenter,
                fontSize=21, fontStyle=FontStyle.Bold,
                padding = new RectOffset(12,12,6,6)
            };
            if (displayFont != null) style.font = displayFont;
            return style;
        }
        private void PrepareStyles()
        {
            if (built) return;
            displayFont = Resources.Load<Font>("Fonts/GameUIFont"); // optional Japanese-capable font
            coral = Panel(coralColor);
            cream = Panel(new Color(.99f,.97f,.85f,.96f));
            dark = Panel(new Color(.06f,.18f,.23f,.94f));
            glass = Panel(new Color(.96f,.98f,.94f,.89f));
            modal = Panel(new Color(.99f,.97f,.89f,.98f),24);
            btn = Button(tealColor,Color.white);
            primary = Button(new Color(1f,.80f,.25f),new Color(.16f,.22f,.26f));
            danger = Button(new Color(.85f,.37f,.43f),Color.white);
            heading = Txt(30,Color.white,FontStyle.Bold);
            body = Txt(22,Color.white);
            small = Txt(16,new Color(.88f,.96f,.97f));
            tiny = Txt(14,new Color(.62f,.75f,.77f));
            accent = Txt(28,new Color(1f,.79f,.22f),FontStyle.Bold);
            money = Txt(34,Color.white,FontStyle.Bold,TextAnchor.MiddleRight);
            blackText = Txt(23,new Color(.15f,.20f,.21f));
            darkHeading = Txt(29,new Color(.18f,.25f,.28f),FontStyle.Bold);
            value = Txt(40,new Color(.22f,.29f,.30f),FontStyle.Bold,TextAnchor.MiddleCenter);
            centered = Txt(19,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);
            textField = new GUIStyle(GUI.skin.textField) {
                normal={background=Texture(new Color(.92f,.96f,.93f)),textColor=nightColor},
                focused={background=Texture(Color.white),textColor=nightColor},
                border=new RectOffset(12,12,12,12),fontSize=20,
                padding=new RectOffset(12,12,7,7)
            };
            if (displayFont != null) textField.font = displayFont;
            built = true;
        }
        private void Update()
        {
            if (app == null || app.match == null) return;
            string message = app.match.message ?? "";
            if (message != lastMessage)
            {
                lastMessage = message;
                messageUntil = Time.unscaledTime + 3.8f;
            }
        }
        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color; GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color = previous;
        }
        private void Text(Rect rect, string text, GUIStyle style) => GUI.Label(rect,text ?? "",style);
        private bool B(Rect rect, string label, GUIStyle style) => GUI.Button(rect,label,style);
        private void OnGUI()
        {
            if (app == null || app.match == null || app.boardView == null) return;
            PrepareStyles();
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width/Width,Screen.height/Height);
            GUI.matrix = Matrix4x4.TRS(
                new Vector3((Screen.width-Width*scale)/2f,(Screen.height-Height*scale)/2f,0),
                Quaternion.identity,Vector3.one*scale);
            DrawTop();
            DrawContext();
            DrawCommands();
            if (showCards) DrawCards();
            if (showPlayers) DrawPlayers();
            if (showMiniMap) DrawFullMap();
            if (onlineMenu) DrawOnline();
            GUI.matrix = previous;
        }
        private void DrawTop()
        {
            var s=app.match;
            int seat = Mathf.Clamp(app.MySeat,0,s.players.Count-1);
            var me = s.players[seat];
            // The top of the world is deliberately left mostly unobstructed.
            GUI.Box(new Rect(22,17,605,83),"",coral);
            Fill(new Rect(38,31,7,54),BoardView.PlayerColors[seat]);
            Text(new Rect(60,23,320,28),me.name.ToUpperInvariant()+"  /  PRESIDENT",heading);
            Text(new Rect(60,62,250,26),"ROUND "+(s.turnCount/Mathf.Max(1,s.players.Count)+1),small);
            Text(new Rect(331,30,274,55),me.cash.ToString("N0")+" M",money);
            if (B(new Rect(486,102,138,37),"PLAYERS",btn)) showPlayers=!showPlayers;

            GUI.Box(new Rect(894,16,363,113),"",cream);
            Text(new Rect(912,25,328,22),"NEXT DESTINATION",Txt(16,new Color(.40f,.42f,.36f),FontStyle.Bold));
            string dest = s.started && app.boardView.Graph.nodes.ContainsKey(s.destination)
                ? app.boardView.Graph.nodes[s.destination].name.ToUpperInvariant() : "WAITING";
            Text(new Rect(913,50,314,40),dest,value);
            int distance = s.started
                ? app.boardView.Graph.Distance(s.players[s.currentSeat].nodeId,s.destination) : 0;
            Text(new Rect(925,100,308,23),
                s.started && distance < int.MaxValue ? distance+" TILES BY SHORTEST RAIL" : "READY",
                Txt(16,new Color(.32f,.37f,.35f),FontStyle.Bold,TextAnchor.MiddleCenter));

            if (s.phase=="moving")
            {
                GUI.Box(new Rect(966,142,280,64),"",cream);
                Text(new Rect(984,155,244,40),"STEPS LEFT  "+s.remaining,
                    Txt(27,new Color(.24f,.32f,.32f),FontStyle.Bold,TextAnchor.MiddleCenter));
            }
            else if (app.dice != null && app.dice.IsRolling)
            {
                GUI.Box(new Rect(971,144,271,54),"",cream);
                Text(new Rect(985,154,242,32),"ROLLING...",darkHeading);
            }
            if (!string.IsNullOrEmpty(app.network.lastError))
            {
                GUI.Box(new Rect(22,149,597,58),"",dark);
                Text(new Rect(38,160,559,35),app.network.lastError,small);
            }
        }
        private void DrawContext()
        {
            var s=app.match;
            if (s.phase=="moving" || app.dice.IsRolling) return;
            bool wait = s.phase == "buy" || s.phase=="finished" || !s.started;
            bool transient = Time.unscaledTime < messageUntil;
            if (!wait && !transient) return;
            float y = wait ? 469 : 534;
            float h = wait ? 163 : 80;
            GUI.Box(new Rect(203,y,876,h),"",modal);
            if (wait)
            {
                string title = s.phase=="buy" ? "PROPERTY STATION" :
                    s.phase=="finished" ? "MATCH COMPLETE" : "WAITING ROOM";
                Text(new Rect(229,y+8,803,34),title,darkHeading);
                Text(new Rect(229,y+51,807,37),s.message,blackText);
                if (s.phase=="buy" && app.MyTurn)
                {
                    bool lockInput = app.ControlsLocked;
                    GUI.enabled=!lockInput;
                    if(B(new Rect(246,y+98,348,52),"PURCHASE",primary)) app.Request("buy");
                    if(B(new Rect(616,y+98,348,52),"PASS",btn)) app.Request("skip");
                    GUI.enabled=true;
                }
                if (s.phase=="finished" &&
                    B(new Rect(720,y+99,295,50),"NEW LOCAL GAME",primary)) app.StartOffline();
                if (!s.started && app.isOnline && app.network.seat==0 && 
                    B(new Rect(680,y+99,330,50),"START MATCH",primary)) app.Request("start");
            }
            else
            {
                Text(new Rect(226,y+13,822,53),s.message,
                    Txt(24,new Color(.22f,.27f,.28f),FontStyle.Bold));
            }
        }
        private void DrawCommands()
        {
            var s=app.match;
            GUI.Box(new Rect(17,640,1246,66),"",dark);
            // Four primary actions are enough; no permanent scorecard or giant map panel.
            bool usable = app.MyTurn && !app.ControlsLocked && !showMiniMap && !onlineMenu;
            GUI.enabled=!onlineMenu && !showMiniMap;
            if(B(new Rect(33,649,195,49),showCards ? "CLOSE CARDS" : "CARDS",btn))
            { showCards=!showCards; showPlayers=false; }
            GUI.enabled=usable;
            if(s.started && s.phase=="ready")
            {
                if(B(new Rect(246,648,493,51),"ROLL THE DICE  ●",primary))
                { showCards=false; app.Request("roll"); }
            }
            else if(s.started && s.phase=="moving")
            {
                Direction(new Rect(249,649,116,49),"LEFT",-1,0);
                Direction(new Rect(371,649,116,49),"UP",0,1);
                Direction(new Rect(493,649,116,49),"DOWN",0,-1);
                Direction(new Rect(615,649,116,49),"RIGHT",1,0);
            }
            else
            {
                GUI.enabled=false;
                B(new Rect(246,649,493,49),
                    app.dice.IsRolling ? "ROLLING..." : s.phase=="finished" ? "GAME OVER" : "WAIT YOUR TURN",btn);
            }
            GUI.enabled=!onlineMenu;
            if(B(new Rect(759,649,239,49),showMiniMap ? "CLOSE MAP" : "FULL MAP  +",btn))
            { showMiniMap=!showMiniMap; showCards=false; showPlayers=false; }
            if(B(new Rect(1010,649,235,49),"GAME MENU",btn))
                onlineMenu = true;
            GUI.enabled=true;
        }
        private void Direction(Rect rect, string label, int dx, int dz)
        {
            var s = app.match; var graph=app.boardView.Graph;
            int at=s.players[app.MySeat].nodeId;
            bool possible = graph.Neighbors(at).Any(next =>
                graph.nodes[next].x-graph.nodes[at].x==dx &&
                graph.nodes[next].z-graph.nodes[at].z==dz &&
                (next!=s.previousNode || graph.Neighbors(at).Count==1));
            bool original=GUI.enabled;
            GUI.enabled=original && possible;
            if(B(rect,label,possible ? primary : btn)) app.MoveDirection(dx,dz);
            GUI.enabled=original;
        }
        private void DrawCards()
        {
            var s = app.match;
            var player=s.players[Mathf.Clamp(app.MySeat,0,s.players.Count-1)];
            GUI.Box(new Rect(26,382,368,248),"",dark);
            Text(new Rect(48,393,320,38),"YOUR CARDS",heading);
            Text(new Rect(48,434,315,23),"Use a card before rolling.",small);
            for(int i=0;i<3;i++)
            {
                string name=i<player.cards.Count ? CardLabel(player.cards[i]) : "-- EMPTY --";
                bool enabled=i<player.cards.Count && s.phase=="ready" && app.MyTurn &&
                    !app.ControlsLocked;
                GUI.enabled=enabled;
                if(B(new Rect(45,466+i*51,325,43),(i+1)+".  "+name,btn))
                { app.Request("card",-1,i); showCards=false; }
                GUI.enabled=true;
            }
        }
        private static string CardLabel(string id)
        {
            switch(id)
            {
                case "EXPRESS": return "EXPRESS  +2 STEPS";
                case "CASH": return "CASH BONUS  +350M";
                case "SHIELD": return "PROTECTION  1 TIME";
                default: return id;
            }
        }
        private void DrawPlayers()
        {
            var s=app.match;
            float h=72+s.players.Count*63;
            GUI.Box(new Rect(23,146,410,h),"",dark);
            Text(new Rect(43,160,367,32),"PLAYER RANKING",heading);
            for(int i=0;i<s.players.Count;i++)
            {
                int seat=i;
                Text(new Rect(47,207+i*63,355,48),
                    (i==s.currentSeat?"● ":"   ")+s.players[seat].name+
                    "   "+GameRules.Wealth(s,seat).ToString("N0")+"M",
                    Txt(18,BoardView.PlayerColors[i],FontStyle.Bold));
            }
        }
        private static void Line(Vector2 from, Vector2 to, Color color, float width)
        {
            Color c=GUI.color; Matrix4x4 m=GUI.matrix;
            Vector2 delta=to-from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,from);
            GUI.color=color;
            GUI.DrawTexture(new Rect(from.x,from.y-width*.5f,delta.magnitude,width),Texture2D.whiteTexture);
            GUI.matrix=m; GUI.color=c;
        }
        private static long Edge(int a,int b)=>
            ((long)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
        private void DrawFullMap()
        {
            var s=app.match;
            var graph=app.boardView.Graph;
            Fill(new Rect(0,0,1280,720),new Color(.02f,.07f,.09f,.87f));
            GUI.Box(new Rect(80,35,1120,596),"",cream);
            Text(new Rect(116,54,700,48),"RAIL NETWORK  /  ROUTE OVERVIEW",darkHeading);
            if(B(new Rect(1017,53,142,48),"CLOSE",btn)) { showMiniMap=false; return; }
            Rect area=new Rect(110,116,1048,442);
            Fill(area,new Color(.09f,.48f,.58f,.94f));
            int minX=graph.map.nodes.Min(n=>n.x), maxX=graph.map.nodes.Max(n=>n.x);
            int minZ=graph.map.nodes.Min(n=>n.z), maxZ=graph.map.nodes.Max(n=>n.z);
            float spanX=Mathf.Max(1,maxX-minX),spanZ=Mathf.Max(1,maxZ-minZ);
            float unit=Mathf.Min((area.width-60)/spanX,(area.height-46)/spanZ);
            Vector2 center=new Vector2(area.center.x,area.center.y);
            Vector2 Pt(int id)
            {
                var n=graph.nodes[id];
                return center+new Vector2((n.x-(minX+maxX)*.5f)*unit,
                    -(n.z-(minZ+maxZ)*.5f)*unit);
            }
            var path=s.started ? graph.ShortestPath(s.players[s.currentSeat].nodeId,s.destination):new List<int>();
            var marked=new HashSet<long>();
            for(int i=1;i<path.Count;i++) marked.Add(Edge(path[i-1],path[i]));
            foreach(var e in graph.map.edges)
                Line(Pt(e.a),Pt(e.b),marked.Contains(Edge(e.a,e.b))
                    ?new Color(1f,.82f,.17f):new Color(.93f,.94f,.87f),
                    marked.Contains(Edge(e.a,e.b))?4.8f:2.5f);
            foreach(var n in graph.map.nodes)
            {
                if(n.kind==TileKind.Rail)continue;
                Vector2 p=Pt(n.id);
                Fill(new Rect(p.x-4,p.y-4,8,8),n.kind==TileKind.Property?
                    new Color(.99f,.66f,.25f):new Color(.74f,.40f,.94f));
                if(!string.IsNullOrEmpty(n.name) &&
                    (n.id==s.destination || n.id==s.players[s.currentSeat].nodeId ||
                     (n.id%4==0 && unit>9)))
                    Text(new Rect(p.x+6,p.y-12,125,20),n.name,small);
            }
            if(s.started)
            {
                Vector2 dest=Pt(s.destination);
                Fill(new Rect(dest.x-9,dest.y-9,18,18),new Color(1f,.85f,.11f));
            }
            for(int i=0;i<s.players.Count;i++)
            {
                Vector2 p=Pt(s.players[i].nodeId);
                Fill(new Rect(p.x-8+i*3,p.y-8+i*3,16,16),BoardView.PlayerColors[i]);
            }
            Text(new Rect(123,573,1002,30),
                "GOLD = SHORTEST RAIL  /  STATION DOTS = PROPERTY & EVENTS  /  M = CLOSE",
                Txt(18,new Color(.23f,.31f,.32f),FontStyle.Bold));
        }
        private void DrawOnline()
        {
            Fill(new Rect(0,0,1280,720),new Color(.02f,.07f,.10f,.85f));
            GUI.Box(new Rect(252,83,776,562),"",modal);
            Text(new Rect(292,111,676,52),"ONLINE / GAME MENU",darkHeading);
            Text(new Rect(292,171,677,24),"CLOUDFLARE WORKER HTTPS ADDRESS",blackText);
            if (endpoint=="" && app.network.serverUrl!="") endpoint=app.network.serverUrl;
            endpoint=GUI.TextField(new Rect(292,204,692,49),endpoint,200,textField);
            if(B(new Rect(292,266,692,48),"SAVE SERVER",btn)) app.SaveEndpoint(endpoint);
            Text(new Rect(292,331,490,24),"DISPLAY NAME",blackText);
            playerName=GUI.TextField(new Rect(292,360,692,48),playerName,16,textField);
            if(B(new Rect(292,432,281,52),"CREATE ROOM",primary))
            { app.SaveEndpoint(endpoint); app.CreateOnline(playerName); }
            roomInput=GUI.TextField(new Rect(593,432,170,52),roomInput.ToUpperInvariant(),6,textField);
            if(B(new Rect(781,432,203,52),"JOIN ROOM",btn))
            { app.SaveEndpoint(endpoint); app.JoinOnline(roomInput,playerName); }
            Text(new Rect(292,495,684,46),!string.IsNullOrEmpty(app.network.lastError)
                ? app.network.lastError : app.status,Txt(16,new Color(.31f,.35f,.35f)));
            if(B(new Rect(292,561,335,53),"BACK TO THE BOARD",btn))
                onlineMenu=false;
            if(B(new Rect(645,561,339,53),"NEW LOCAL GAME",danger))
            { app.StartOffline(); showCards=false; showPlayers=false; showMiniMap=false; }
        }
    }
}
