// RWC Formations: the game-free half. Saved layouts, the layouts file, and the
// plan that turns a layout into moves (which unit goes to which saved spot,
// mirrored or not). No game references, so formation-test/ runs it offline.
//
// Coordinates (0.6.0+): the VIEW frame, the board as seen from your seat (+x right
// on screen, +y toward the enemy), x relative to your deployment area's centre, so a
// layout fits either seat and either 2v2 partner. Layouts saved by 0.1 to 0.5 are in
// the game's standard space (Frame absent); GameBoard.InView converts them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RwcFormations
{
    public class Slot
    {
        [JsonPropertyName("unit")] public int Unit { get; set; }    // unit id (CardElement.GetID)
        [JsonPropertyName("x")] public int X { get; set; }          // standard x minus the territory centre
        [JsonPropertyName("y")] public int Y { get; set; }          // standard y
        [JsonPropertyName("rot")] public bool Rot { get; set; }     // footprint turned 90 degrees
        [JsonPropertyName("px")] public int Px { get; set; }        // standard x mod 10 when saved (5 or 0: odd / even footprints)
        [JsonPropertyName("level")] public int Level { get; set; }  // informational only, matching ignores it
        [JsonPropertyName("w")] public int W { get; set; }          // footprint as placed (0.3.0+; 0 in older saves), for the preview
        [JsonPropertyName("h")] public int H { get; set; }
    }

    public class Layout
    {
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("saved")] public string Saved { get; set; }   // local time, yyyy-MM-dd HH:mm
        [JsonPropertyName("slots")] public List<Slot> Slots { get; set; } = new List<Slot>();
        // The territory when saved, standard space, x relative to its centre:
        // [xMin, yMin, xMax, yMax] (0.3.0+; null in older saves), for the preview.
        [JsonPropertyName("area")] public int[] Area { get; set; }
        // "view" (0.6.0+): the board as seen from your seat, +x right on screen,
        // x relative to your deployment area's centre. Absent = "std", the game's
        // standard space (0.1 to 0.5; mirrored for one seat, see GameBoard.cs).
        [JsonPropertyName("frame")] public string Frame { get; set; }
        public const string ViewFrame = "view";
    }

    public class LayoutFile
    {
        [JsonPropertyName("version")] public int Version { get; set; } = 1;
        [JsonPropertyName("layouts")] public List<Layout> Layouts { get; set; } = new List<Layout>();

        static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { WriteIndented = true };

        public static LayoutFile Load(string path)
        {
            if (!File.Exists(path)) return new LayoutFile();
            var f = JsonSerializer.Deserialize<LayoutFile>(File.ReadAllText(path), Opts) ?? new LayoutFile();
            f.Layouts ??= new List<Layout>();
            foreach (var l in f.Layouts) l.Slots ??= new List<Slot>();
            return f;
        }

        // Write to a temp file first so a crash mid-write can't eat the saved layouts.
        public void Save(string path)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Opts));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }

    // One of the player's units as the planner sees it (standard space, absolute x).
    public struct BoardUnit
    {
        public int Key;        // caller's handle (index into its own unit list)
        public int Unit;
        public int X, Y;
        public bool Rot;
        public bool Movable;   // the game lets it move this round (new, or redeployable)
        public int W, H;       // footprint as placed
    }

    public struct Move
    {
        public int Key;
        public int X, Y;       // standard space, absolute
        public bool Rot;
    }

    public class Plan
    {
        public List<Move> Moves = new List<Move>();
        public int AlreadyThere;                                  // units already on their spot
        public int HeldByFixed;                                   // spots taken by units that can't move
        public Dictionary<int, int> MissingUnits = new Dictionary<int, int>();  // unit id -> spots with no unit for them
        public int LeftAlone;                                     // movable units with no spot
    }

    public static class Planner
    {
        // A unit that can't move "holds" its saved spot when it sits this close to it.
        public const int HoldDistance = 20;

        // Where a slot lands for a territory centred on cx (standard x), mirrored or not.
        // Mirroring swaps left and right around the centre. Positions sit on the
        // game's grid by footprint (odd widths on x = 5 mod 10, even on multiples of
        // 10); if the centre would put the spot on the wrong parity it moves 5 toward
        // the centre.
        public static (int x, int y) Target(Slot s, int cx, bool mirror)
        {
            int x = cx + (mirror ? -s.X : s.X);
            if (Mod10(x) != s.Px) x += x > cx ? -5 : 5;
            return (x, s.Y);
        }

        public static int Mod10(int v) => ((v % 10) + 10) % 10;

        public static Plan Make(Layout layout, IList<BoardUnit> units, int cx, bool mirror)
        {
            var plan = new Plan();
            var free = layout.Slots.Select(s => (slot: s, t: Target(s, cx, mirror))).ToList();

            // Units the game won't move claim the spot they already sit on.
            foreach (var u in units.Where(u => !u.Movable))
            {
                int best = -1; long bestD = long.MaxValue;
                for (int i = 0; i < free.Count; i++)
                {
                    if (free[i].slot.Unit != u.Unit) continue;
                    long d = D2(u.X, u.Y, free[i].t.x, free[i].t.y);
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best >= 0 && bestD <= (long)HoldDistance * HoldDistance) { free.RemoveAt(best); plan.HeldByFixed++; }
            }

            // Movable units take the nearest free spot of their own type: every
            // (unit, spot) pair sorted by distance, taken greedily.
            var movable = units.Where(u => u.Movable).ToList();
            var pairs = new List<(long d, int u, int s)>();
            for (int ui = 0; ui < movable.Count; ui++)
                for (int si = 0; si < free.Count; si++)
                    if (free[si].slot.Unit == movable[ui].Unit)
                        pairs.Add((D2(movable[ui].X, movable[ui].Y, free[si].t.x, free[si].t.y), ui, si));
            pairs.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.u != b.u ? a.u.CompareTo(b.u) : a.s.CompareTo(b.s));
            var usedU = new bool[movable.Count];
            var usedS = new bool[free.Count];
            foreach (var p in pairs)
            {
                if (usedU[p.u] || usedS[p.s]) continue;
                usedU[p.u] = usedS[p.s] = true;
                var u = movable[p.u];
                var (slot, t) = free[p.s];
                if (u.X == t.x && u.Y == t.y && u.Rot == slot.Rot) { plan.AlreadyThere++; continue; }
                plan.Moves.Add(new Move { Key = u.Key, X = t.x, Y = t.y, Rot = slot.Rot });
            }
            for (int si = 0; si < free.Count; si++)
                if (!usedS[si])
                {
                    plan.MissingUnits.TryGetValue(free[si].slot.Unit, out int n);
                    plan.MissingUnits[free[si].slot.Unit] = n + 1;
                }
            plan.LeftAlone = usedU.Count(x => !x);
            return plan;
        }

        // A layout of the board as it stands (for Save and Mirror my units).
        // area = the territory, standard space, absolute [xMin, yMin, xMax, yMax] (or null).
        public static Layout Capture(string name, IList<BoardUnit> units, int cx, Func<int, int> levelOf = null, int[] area = null)
        {
            var l = new Layout { Name = name, Saved = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Frame = Layout.ViewFrame };
            foreach (var u in units)
                l.Slots.Add(new Slot { Unit = u.Unit, X = u.X - cx, Y = u.Y, Rot = u.Rot, Px = Mod10(u.X), Level = levelOf?.Invoke(u.Key) ?? 0, W = u.W, H = u.H });
            if (area != null) l.Area = new[] { area[0] - cx, area[1], area[2] - cx, area[3] };
            return l;
        }

        // Unit id -> how many of it are on the board.
        public static Dictionary<int, int> Counts(IEnumerable<BoardUnit> units) =>
            units.GroupBy(u => u.Unit).ToDictionary(g => g.Key, g => g.Count());

        // A layout fits the board when every unit type in it is on the board
        // (the panel lists only those; extra units on the board are fine).
        public static bool Fits(Layout l, IDictionary<int, int> have) =>
            l.Slots.Count > 0 && l.Slots.All(s => have.ContainsKey(s.Unit));

        // How many of the layout's spots the board's units could fill.
        public static int Fillable(Layout l, IDictionary<int, int> have) =>
            l.Slots.GroupBy(s => s.Unit).Sum(g => Math.Min(g.Count(), have.TryGetValue(g.Key, out int n) ? n : 0));

        public const int MaxName = 24;

        // A typed name: control characters out, trimmed, capped. Empty = keep the old one.
        public static string CleanName(string s)
        {
            var t = new string((s ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
            return t.Length > MaxName ? t.Substring(0, MaxName).TrimEnd() : t;
        }

        static long D2(int ax, int ay, int bx, int by) { long dx = ax - bx, dy = ay - by; return dx * dx + dy * dy; }
    }
}
