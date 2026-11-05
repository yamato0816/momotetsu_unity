using System.Collections.Generic;
using UnityEngine;

namespace RailTrip3D
{
    // All tracks occupy grid cells. Route stops remain intentionally easy to edit.
    public static class SampleMap
    {
        public static RailMap Create()
        {
            var m = new RailMap { title = "NIPPON EXPRESS / Sample Japan" };
            var byGrid = new Dictionary<Vector2Int, int>();
            var named = new Dictionary<string, int>();
            int Add(int x, int z, string label = "", TileKind kind = TileKind.Rail, int price = 200, int income = 40)
            {
                var point = new Vector2Int(x, z);
                if (byGrid.TryGetValue(point, out int existing))
                {
                    if (!string.IsNullOrEmpty(label))
                    {
                        var n = m.nodes[existing];
                        n.name = label; n.kind = kind; n.price = price; n.income = income;
                        named[label] = existing;
                    }
                    return existing;
                }
                int id = m.nodes.Count;
                m.nodes.Add(new RailNode { id = id, x = x, z = z, name = label, kind = kind, price = price, income = income });
                byGrid[point] = id;
                if (!string.IsNullOrEmpty(label)) named[label] = id;
                return id;
            }
            void Link(int a, int b)
            {
                if (a == b) return;
                foreach (var e in m.edges) if ((e.a == a && e.b == b) || (e.a == b && e.b == a)) return;
                m.edges.Add(new RailEdge(a, b));
            }
            void Track(int from, int to, bool alongXFirst = true)
            {
                var a = m.nodes[from]; var b = m.nodes[to];
                int x = a.x, z = a.z, last = from;
                void StepX() { while (x != b.x) { x += x < b.x ? 1 : -1; int next = Add(x, z); Link(last, next); last = next; } }
                void StepZ() { while (z != b.z) { z += z < b.z ? 1 : -1; int next = Add(x, z); Link(last, next); last = next; } }
                if (alongXFirst) { StepX(); StepZ(); } else { StepZ(); StepX(); }
            }
            // Approximate positions, not geographical coordinates; focus on readable rails.
            Add(30, 41, "Sapporo", TileKind.Property, 480, 100);
            Add(28, 38, "Hakodate", TileKind.Station);
            Add(28, 35, "Aomori", TileKind.Card);
            Add(29, 31, "Morioka", TileKind.Property, 260, 65);
            Add(30, 28, "Sendai", TileKind.Property, 350, 85);
            Add(30, 25, "Fukushima", TileKind.Bonus);
            Add(30, 21, "Tokyo", TileKind.Property, 850, 180);
            Add(32, 21, "Chiba", TileKind.Card);
            Add(30, 19, "Yokohama", TileKind.Property, 600, 120);
            Add(28, 18, "Shizuoka", TileKind.Property, 280, 65);
            Add(25, 17, "Nagoya", TileKind.Property, 520, 110);
            Add(23, 17, "Kyoto", TileKind.Card);
            Add(22, 16, "Osaka", TileKind.Property, 780, 160);
            Add(21, 16, "Kobe", TileKind.Property, 400, 85);
            Add(19, 16, "Himeji", TileKind.Station);
            Add(17, 16, "Okayama", TileKind.Property, 280, 60);
            Add(14, 16, "Hiroshima", TileKind.Property, 400, 90);
            Add(11, 15, "Yamaguchi", TileKind.Penalty);
            Add(10, 15, "Shimonoseki", TileKind.Station);
            Add(9, 14, "Fukuoka", TileKind.Property, 520, 110);
            Add(8, 11, "Kumamoto", TileKind.Bonus);
            Add(7, 7, "Kagoshima", TileKind.Property, 360, 80);
            Add(6, 13, "Nagasaki", TileKind.Card);
            Add(27, 24, "Niigata", TileKind.Property, 300, 75);
            Add(26, 22, "Nagano", TileKind.Station);
            Add(25, 20, "Toyama", TileKind.Card);
            Add(23, 20, "Kanazawa", TileKind.Property, 340, 75);
            Add(23, 18, "Fukui", TileKind.Bonus);
            Add(20, 19, "Tottori", TileKind.Card);
            Add(16, 19, "Matsue", TileKind.Property, 230, 60);
            Add(18, 13, "Takamatsu", TileKind.Property, 300, 70);
            Add(20, 12, "Tokushima", TileKind.Station);
            Add(17, 10, "Kochi", TileKind.Card);
            Add(14, 12, "Matsuyama", TileKind.Property, 270, 70);
            m.startNode = named["Tokyo"];
            void Chain(params string[] labels)
            {
                for (int i = 1; i < labels.Length; i++) Track(named[labels[i - 1]], named[labels[i]], i % 2 == 0);
            }
            Chain("Sapporo", "Hakodate", "Aomori", "Morioka", "Sendai", "Fukushima", "Tokyo", "Yokohama", "Shizuoka", "Nagoya", "Kyoto", "Osaka", "Kobe", "Himeji", "Okayama", "Hiroshima", "Yamaguchi", "Shimonoseki", "Fukuoka", "Kumamoto", "Kagoshima");
            Chain("Tokyo", "Chiba");
            Chain("Fukushima", "Niigata", "Nagano", "Toyama", "Kanazawa", "Fukui", "Kyoto");
            Chain("Fukui", "Tottori", "Matsue", "Hiroshima");
            Chain("Fukuoka", "Nagasaki");
            Chain("Okayama", "Takamatsu", "Tokushima");
            Chain("Takamatsu", "Matsuyama", "Kochi", "Tokushima");
            // Mark non-city squares with game events so the route is lively.
            for (int i = 0; i < m.nodes.Count; i++)
            {
                var n = m.nodes[i];
                if (n.kind != TileKind.Rail) continue;
                int roll = (n.x * 17 + n.z * 31) & 31;
                if (roll == 4) n.kind = TileKind.Card;
                else if (roll == 9) n.kind = TileKind.Bonus;
                else if (roll == 19) n.kind = TileKind.Penalty;
            }
            return m;
        }
    }
}
