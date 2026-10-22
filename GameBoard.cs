// RWC Formations: the game half. Reads the local player's units and sends moves
// through the game's own move action, the one a drag sends:
//   PAD_MoveUnit (one action can carry many units, like a group drag)
//   checked first with TerritoryManager.CanMoveUnits, then
//   PlayerController.TryPerformAction(pad)
// The game validates every spot, records the moves in the replay like any other
// move, and the fight engine is never touched. Units from earlier rounds are
// locked by the game (CardElement.IsOldUnit) and are left where they are.
//
// Coordinates (0.6.0): layouts are kept in the VIEW frame, the board as you see it
// from your seat: +x = right on screen, +y = toward the enemy. The game turns the
// camera 180 degrees for a seat on the +y side (memory/long-term.md "In-game view
// orientation"), so view = world for a -y seat and -world for a +y seat. 0.1 to 0.5
// used the game's ConvertToStandardWorldPosition instead, which flips depth but NOT
// left and right: the preview was mirrored for one seat (Kris, 0.4.0 and 0.5.0 in
// two games) and a layout placed from the other seat came out mirrored. Layouts
// saved then ("std" frame) still go through the game's conversion, so they land
// where they always did.
using System;
using System.Collections.Generic;
using System.Linq;
#if MELON
using Il2CppGameRiver;
using Il2CppGameRiver.Client;
#else
using GameRiver;
using GameRiver.Client;
#endif

namespace RwcFormations
{
    internal class Board
    {
        public MatchClient Match;
        public PlayerController Player;
        public TerritoryManager Territory;
        public MapSystem Map;
        public List<CardElement> Elements = new List<CardElement>();
        public List<BoardUnit> Units = new List<BoardUnit>();
        public int H;          // +1 if your side is at +y (world), -1 if at -y
        public int CenterX;    // your deployment area's centre, view x
        public int[] Area;     // your deployment area, view frame, absolute: xMin, yMin, xMax, yMax
        public int CenterXStd; // the old (0.1 to 0.5) centre: the whole territory, standard x

        public MapVector ToView(MapVector world) => new MapVector(-H * world.x, -H * world.y);
        public MapVector ToWorld(int vx, int vy) => new MapVector(-H * vx, -H * vy);
    }

    internal static class GameBoard
    {
        // Moves only in games the player is playing: never in a replay, a replay
        // simulation (the sim plugin's runs) or while spectating.
        static readonly HashSet<MatchType> Allowed = new HashSet<MatchType> { MatchType.Normal, MatchType.AI, MatchType.Test, MatchType.Level };

        // Null (with the reason) unless it's this player's deployment right now.
        public static Board Read(out string why)
        {
            why = null;
            var match = MatchClient.Current;
            if (match == null) { why = "not in a game"; return null; }
            var setting = MatchClient.BattleSetting;
            if (setting == null || !Allowed.Contains(setting.MatchType)) { why = "not a game you're playing (replay or spectating)"; return null; }
            if (!match.IsInPreparePhase()) { why = "wait for the deployment phase"; return null; }
            var pc = match.GetLocalPlayerController();
            if (pc == null) { why = "no local player"; return null; }
            if (!pc.IsDeploying() || pc.IsDeployOver()) { why = "deployment is over for this round"; return null; }
            var tm = pc.GetTerritoryManager();
            if (tm == null || tm.mapSystem == null || tm.playerTerritory == null) { why = "map not ready"; return null; }

            var b = new Board { Match = match, Player = pc, Territory = tm, Map = tm.mapSystem };
            var bound = tm.playerTerritory.Bound;   // the whole map (both halves), measured 0.4.0
            var c1 = b.Map.ConvertToStandardWorldPosition(pc, new MapVector(bound.xMin, bound.yMin));
            var c2 = b.Map.ConvertToStandardWorldPosition(pc, new MapVector(bound.xMax, bound.yMax));
            b.CenterXStd = (c1.x + c2.x) / 2;

            // Your side and your deployment area (the main region); if that bound is
            // unusable, your half of the territory.
            var list = pc.unitManager?.units;
            MapRect region = default;
            bool haveRegion = false;
            try { region = tm.playerTerritory.mainRegionBound; haveRegion = region.width >= 100 && region.height >= 20; } catch { }
            int cy = haveRegion ? region.y + region.height / 2 : 0;
            if (cy == 0 && list != null && list.Count > 0)
            {
                long sum = 0;
                for (int i = 0; i < list.Count; i++) { var me0 = list[i]?.GetMapElement(); if (me0 != null) sum += me0.GetPosition().y; }
                cy = Math.Sign(sum);
            }
            b.H = cy > 0 ? 1 : -1;
            int rx0, ry0, rx1, ry1;
            if (haveRegion) { rx0 = region.xMin; ry0 = region.yMin; rx1 = region.xMax; ry1 = region.yMax; }
            else { rx0 = bound.xMin; rx1 = bound.xMax; ry0 = b.H > 0 ? 0 : bound.yMin; ry1 = b.H > 0 ? bound.yMax : 0; }
            var v1 = b.ToView(new MapVector(rx0, ry0));
            var v2 = b.ToView(new MapVector(rx1, ry1));
            b.Area = new[] { Math.Min(v1.x, v2.x), Math.Min(v1.y, v2.y), Math.Max(v1.x, v2.x), Math.Max(v1.y, v2.y) };
            b.CenterX = (b.Area[0] + b.Area[2]) / 2;

            if (list != null)
                for (int i = 0; i < list.Count; i++)
                {
                    var el = list[i];
                    var me = el?.GetMapElement();
                    if (me == null) continue;
                    var world = me.GetPosition();
                    var view = b.ToView(world);
                    var size = me.GetBound().size;   // a 180-degree turn keeps width and depth
                    b.Units.Add(new BoardUnit
                    {
                        Key = b.Elements.Count, Unit = el.GetID(), X = view.x, Y = view.y,
                        Rot = me.isRotate, Movable = IsMovable(tm, el, world), W = size.x, H = size.y,
                    });
                    b.Elements.Add(el);
                }
            return b;
        }

        // New this round, or the game says it can go back on its own spot
        // (redeploy). A wrong guess here only costs a skipped move: the game
        // checks every move again before it runs.
        static bool IsMovable(TerritoryManager tm, CardElement el, MapVector world)
        {
            try
            {
                if (!el.IsOldUnit()) return true;
                return tm.CanMoveUnit(el, world, false) == PlayerActionCheckResult.OK;
            }
            catch { return false; }
        }

        public static Layout Capture(Board b, string name) =>
            Planner.Capture(name, b.Units, b.CenterX, key => (int)b.Elements[key].GetLevel(), b.Area);

        // A layout in the view frame for this board. "view" layouts (0.6.0+) already
        // are; older ("std") ones go through the game's own conversion, exactly as
        // they were placed before, then into the view frame.
        public static Layout InView(Board b, Layout l)
        {
            if (l.Frame == Layout.ViewFrame) return l;
            var v = new Layout { Name = l.Name, Saved = l.Saved, Frame = Layout.ViewFrame };
            foreach (var s in l.Slots)
            {
                var world = b.Map.ConvertPositionByPlayerSapce(b.Player, new MapVector(b.CenterXStd + s.X, s.Y));
                var view = b.ToView(world);
                v.Slots.Add(new Slot { Unit = s.Unit, X = view.x - b.CenterX, Y = view.y, Rot = s.Rot, Px = Planner.Mod10(view.x), Level = s.Level, W = s.W, H = s.H });
            }
            return v;
        }

        public class Result
        {
            public int Moved, Failed;
            public string Detail = "";
            public List<Move> Done = new List<Move>();   // the moves the game carried out
        }

        public static Result Apply(Board b, Plan plan)
        {
            var r = new Result();
            if (plan.Moves.Count == 0) return r;
            var moves = plan.Moves.Select(m => (m, world: b.ToWorld(m.X, m.Y))).ToList();

            // One action for the whole layout (what a group drag sends).
            var all = NewPad();
            foreach (var (m, world) in moves) all.AddMoveData(b.Player, b.Elements[m.Key], world, m.Rot);
            var check = b.Territory.CanMoveUnits(all.moveUnitDatas, false);
            Host.Info($"Apply: {moves.Count} moves in one action, check = {check}");
            if (check == PlayerActionCheckResult.OK && b.Player.TryPerformAction(all))
            {
                r.Moved = moves.Count;
                r.Done.AddRange(plan.Moves);
                return r;
            }

            // Fallback: one unit at a time, a few passes (a spot can be free only
            // after another unit has left it). Spots the game refuses are skipped.
            var left = moves.ToList();
            var reasons = new Dictionary<PlayerActionCheckResult, int>();
            for (int pass = 0; pass < 4 && left.Count > 0; pass++)
            {
                reasons.Clear();
                var next = new List<(Move m, MapVector world)>();
                foreach (var mv in left)
                {
                    var one = NewPad();
                    one.AddMoveData(b.Player, b.Elements[mv.m.Key], mv.world, mv.m.Rot);
                    var c = b.Territory.CanMoveUnits(one.moveUnitDatas, false);
                    if (c == PlayerActionCheckResult.OK && b.Player.TryPerformAction(one)) { r.Moved++; r.Done.Add(mv.m); }
                    else
                    {
                        next.Add(mv);
                        reasons.TryGetValue(c, out int n);
                        reasons[c] = n + 1;
                    }
                }
                if (next.Count == left.Count) break;
                left = next;
            }
            r.Failed = left.Count;
            if (r.Failed > 0)
            {
                r.Detail = string.Join(", ", reasons.Select(kv => $"{kv.Value} {kv.Key}"));
                Host.Info($"Apply: {r.Failed} moves refused by the game ({r.Detail})");
            }
            return r;
        }

        static PAD_MoveUnit NewPad()
        {
            PAD_MoveUnit pad;
            try { pad = PlayerActionData.Create<PAD_MoveUnit>(); }
            catch { pad = new PAD_MoveUnit(); }
            if (pad.moveUnitDatas == null) pad.moveUnitDatas = new Il2CppSystem.Collections.Generic.List<MoveUnitData>();
            return pad;
        }

        // A line per unit to the log, for checking the coordinates on a first run.
        public static void LogBoard(Board b)
        {
            Host.Info($"Board: {b.Units.Count} units, side {(b.H > 0 ? "+y" : "-y")}, area (view) [{string.Join(", ", b.Area)}], centre x = {b.CenterX}, local seat {SafeSeat(b)}");
            for (int i = 0; i < b.Units.Count; i++)
            {
                var u = b.Units[i];
                var world = b.Elements[i].GetMapElement().GetPosition();
                var std = b.Map.ConvertToStandardWorldPosition(b.Player, world);
                Host.Info($"  unit {u.Unit} view ({u.X}, {u.Y}) world ({world.x}, {world.y}) std ({std.x}, {std.y}) rot {u.Rot} movable {u.Movable}");
            }
        }

        static string SafeSeat(Board b) { try { return b.Match.GetLocalPlayerSeat().ToString(); } catch { return "?"; } }
    }
}
