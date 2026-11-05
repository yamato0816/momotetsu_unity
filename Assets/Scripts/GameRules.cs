using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RailTrip3D
{
    [Serializable] public class PlayerData
    {
        public string name = "Player";
        public int nodeId, cash = 1200, diceBoost, shield;
        public List<string> cards = new List<string>();
    }
    [Serializable] public class MatchData
    {
        public RailMap board;
        public List<PlayerData> players = new List<PlayerData>();
        public string phase = "lobby", message = "Waiting for players", room = "";
        public int currentSeat, destination, ghostSeat = -1, remaining, rolled, previousNode = -1;
        public int turnCount, reachedGoals, winnerSeat = -1;
        public bool started;
    }
    public static class GameRules
    {
        public const int MaxTurnsPerPlayer = 18;
        public static readonly string[] Deck = { "EXPRESS", "CASH", "SHIELD" };
        public static MatchData NewMatch(RailMap map, params string[] names)
        {
            var match = new MatchData { board = map.Copy() };
            foreach (string name in names) Join(match, name);
            return match;
        }
        public static bool Join(MatchData s, string name)
        {
            if (s.started || s.players.Count >= 4) return false;
            s.players.Add(new PlayerData { name = string.IsNullOrWhiteSpace(name) ? "Player " + (s.players.Count + 1) : name.Trim(), nodeId = s.board.startNode });
            return true;
        }
        public static bool Apply(MatchData s, int seat, string action, int node = -1, int card = -1)
        {
            if (s == null || s.board == null || seat < 0 || seat >= s.players.Count) return false;
            var g = new RailGraph(s.board);
            if (action == "start" && !s.started && seat == 0 && s.players.Count >= 1)
            {
                if (g.Validate() != "") return false;
                s.started = true; s.phase = "ready";
                ChooseDestination(s, g, -1);
                s.message = "Reach " + g.nodes[s.destination].name + "!"; return true;
            }
            if (!s.started || s.phase == "finished" || seat != s.currentSeat) return false;
            var p = s.players[seat];
            switch (action)
            {
                case "roll":
                    if (s.phase != "ready") return false;
                    s.rolled = UnityEngine.Random.Range(1, 7) + p.diceBoost;
                    p.diceBoost = 0; s.remaining = s.rolled; s.previousNode = -1;
                    s.phase = "moving"; s.message = p.name + " rolled " + s.rolled; return true;
                case "move":
                    if (s.phase != "moving" || s.remaining <= 0 || !g.Linked(p.nodeId, node)) return false;
                    var choices = g.Neighbors(p.nodeId);
                    if (node == s.previousNode && choices.Count > 1) return false;
                    s.previousNode = p.nodeId; p.nodeId = node; s.remaining--;
                    s.message = p.name + " is travelling... " + s.remaining + " left";
                    if (s.remaining == 0) ResolveArrival(s, g);
                    return true;
                case "buy":
                case "skip":
                    if (s.phase != "buy") return false;
                    var stop = g.nodes[p.nodeId];
                    if (action == "buy" && stop.kind == TileKind.Property && stop.owner < 0 && p.cash >= stop.price)
                    { p.cash -= stop.price; stop.owner = seat; s.message = p.name + " bought " + stop.name; }
                    EndTurn(s, g); return true;
                case "card":
                    if (s.phase != "ready" || card < 0 || card >= p.cards.Count) return false;
                    string used = p.cards[card]; p.cards.RemoveAt(card);
                    if (used == "EXPRESS") p.diceBoost += 2;
                    else if (used == "CASH") p.cash += 350;
                    else if (used == "SHIELD") p.shield++;
                    else return false;
                    s.message = p.name + " used " + used; return true;
            }
            return false;
        }
        private static void ResolveArrival(MatchData s, RailGraph g)
        {
            var p = s.players[s.currentSeat];
            var n = g.nodes[p.nodeId];
            if (p.nodeId == s.destination)
            {
                p.cash += 1600; s.reachedGoals++;
                s.message = p.name + " reached the GOAL! +1600";
                ChooseDestination(s, g, s.destination);
                EndTurn(s, g); return;
            }
            switch (n.kind)
            {
                case TileKind.Property:
                    if (n.owner == -1 && p.cash >= n.price) { s.phase = "buy"; s.message = n.name + ": buy for " + n.price + "?"; return; }
                    if (n.owner >= 0 && n.owner != s.currentSeat)
                    { int toll = Mathf.Max(30, n.income / 2); p.cash -= toll; s.players[n.owner].cash += toll; s.message = "Paid " + toll + " toll at " + n.name; }
                    break;
                case TileKind.Card:
                    if (p.cards.Count < 3) { string draw = Deck[UnityEngine.Random.Range(0, Deck.Length)]; p.cards.Add(draw); s.message = p.name + " got " + draw; }
                    break;
                case TileKind.Bonus: p.cash += 180; s.message = "Bonus station! +180"; break;
                case TileKind.Penalty: p.cash -= 140; s.message = "Unlucky station! -140"; break;
            }
            EndTurn(s, g);
        }
        private static void EndTurn(MatchData s, RailGraph g)
        {
            int current = s.currentSeat;
            var p = s.players[current];
            if (s.ghostSeat == current)
            {
                if (p.shield > 0) { p.shield--; s.message += " / Shield blocked the ghost"; }
                else { int fee = 80 + (s.turnCount / Mathf.Max(1, s.players.Count)) * 6; p.cash -= fee; s.message += " / Ghost -" + fee; }
            }
            s.turnCount++;
            if (s.turnCount % s.players.Count == 0)
            {
                foreach (var n in s.board.nodes)
                    if (n.kind == TileKind.Property && n.owner >= 0 && n.owner < s.players.Count)
                        s.players[n.owner].cash += n.income;
            }
            if (s.turnCount >= s.players.Count * MaxTurnsPerPlayer)
            {
                s.phase = "finished"; s.winnerSeat = Enumerable.Range(0, s.players.Count).OrderByDescending(i => Wealth(s, i)).First();
                s.message = s.players[s.winnerSeat].name + " WINS!"; return;
            }
            s.currentSeat = (current + 1) % s.players.Count; s.phase = "ready"; s.remaining = 0; s.previousNode = -1;
        }
        public static int Wealth(MatchData s, int seat) => s.players[seat].cash +
            s.board.nodes.Where(n => n.owner == seat).Sum(n => n.price);
        public static void ChooseDestination(MatchData s, RailGraph g, int previous)
        {
            var options = s.board.nodes.Where(n => n.kind != TileKind.Rail && !string.IsNullOrWhiteSpace(n.name) && n.id != previous &&
                g.Distance(s.players[s.currentSeat].nodeId, n.id) >= 5 &&
                g.Distance(s.players[s.currentSeat].nodeId, n.id) < int.MaxValue).ToList();
            if (options.Count == 0) options = s.board.nodes.Where(n => n.kind != TileKind.Rail && !string.IsNullOrWhiteSpace(n.name) && n.id != previous).ToList();
            if (options.Count == 0) return;
            s.destination = options[UnityEngine.Random.Range(0, options.Count)].id;
            // The ghost appears after the first goal, never unfairly on turn one.
            s.ghostSeat = s.reachedGoals == 0 ? -1 : Enumerable.Range(0, s.players.Count)
                .OrderByDescending(i => g.Distance(s.players[i].nodeId, s.destination)).First();
        }
    }
}
