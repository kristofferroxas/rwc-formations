// Offline checks for the planner: matching, mirroring, grid parity, locked
// units, and the layouts file round trip. Exit code 1 on any failure.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RwcFormations;

int fails = 0, checks = 0;
void Check(bool ok, string what)
{
    checks++;
    if (!ok) { fails++; Console.WriteLine("FAIL " + what); }
}
BoardUnit U(int key, int unit, int x, int y, bool movable = true, bool rot = false) =>
    new BoardUnit { Key = key, Unit = unit, X = x, Y = y, Movable = movable, Rot = rot };

// A saved layout: two crawlers (odd footprint, x = 5 mod 10), one fortress (even, multiple of 10) rotated.
var saved = Planner.Capture("test", new List<BoardUnit> {
    U(0, 1, -95, -205), U(1, 1, -75, -205), U(2, 7, 120, -250, rot: true) }, 0);
Check(saved.Slots.Count == 3 && saved.Slots[0].Px == 5 && saved.Slots[2].Px == 0, "capture keeps parity");

// 1. Place on a fresh board (units bunched at the default spot): every unit goes to its spot.
var board = new List<BoardUnit> { U(0, 1, 5, -300), U(1, 1, 25, -300), U(2, 7, 0, -300) };
var p = Planner.Make(saved, board, 0, false);
Check(p.Moves.Count == 3, "place moves all three");
var f = p.Moves.Single(m => m.Key == 2);
Check(f.X == 120 && f.Y == -250 && f.Rot, "fortress to its spot, rotated");
var crawlerSpots = p.Moves.Where(m => m.Key < 2).Select(m => m.X).OrderBy(x => x).ToArray();
Check(crawlerSpots.SequenceEqual(new[] { -95, -75 }), "crawlers fill both crawler spots");

// 2. Mirrored: x flips around the centre, y and rotation stay.
p = Planner.Make(saved, board, 0, true);
f = p.Moves.Single(m => m.Key == 2);
Check(f.X == -120 && f.Y == -250 && f.Rot, "fortress mirrored");
crawlerSpots = p.Moves.Where(m => m.Key < 2).Select(m => m.X).OrderBy(x => x).ToArray();
Check(crawlerSpots.SequenceEqual(new[] { 75, 95 }), "crawlers mirrored, still x = 5 mod 10");

// 3. Mirroring twice gets the original back (Mirror my units, twice).
var once = Planner.Capture("m", new List<BoardUnit> { U(0, 1, 75, -205), U(1, 7, -120, -250, rot: true) }, 0);
var back = Planner.Make(once, new List<BoardUnit> { U(0, 1, 75, -205), U(1, 7, -120, -250, rot: true) }, 0, true);
Check(back.Moves.Any(m => m.Key == 0 && m.X == -75) && back.Moves.Any(m => m.Key == 1 && m.X == 120), "mirror of the current board");

// 4. Another territory centre (2v2 partner): the layout shifts with it, parity fixed if the centre is off-grid.
p = Planner.Make(saved, board, 730, false);
Check(p.Moves.Single(m => m.Key == 2).X == 850, "shifted with the centre");
p = Planner.Make(saved, board, 735, false);
foreach (var m in p.Moves)
{
    var unit = board.Single(b => b.Key == m.Key).Unit;
    Check(Planner.Mod10(m.X) == (unit == 1 ? 5 : 0), $"parity after an off-grid centre (unit {unit} at {m.X})");
}

// 5. Fewer units than spots, and a unit the layout doesn't know.
p = Planner.Make(saved, new List<BoardUnit> { U(0, 1, 5, -300), U(1, 22, 0, -300) }, 0, false);
Check(p.Moves.Count == 1 && p.MissingUnits[1] == 1 && p.MissingUnits[7] == 1 && p.LeftAlone == 1, "missing spots and units left alone");
// The crawler takes the nearer of its two spots.
Check(p.Moves[0].X == -75, "nearest spot first");

// 6. A locked unit (earlier round) on its spot holds it; the new one takes the other.
p = Planner.Make(saved, new List<BoardUnit> { U(0, 1, -95, -205, movable: false), U(1, 1, 30, -300) }, 0, false);
Check(p.HeldByFixed == 1 && p.Moves.Count == 1 && p.Moves[0].Key == 1 && p.Moves[0].X == -75, "locked unit holds its spot");
// A locked unit far from any spot holds nothing, and is never moved.
p = Planner.Make(saved, new List<BoardUnit> { U(0, 1, 200, -100, movable: false), U(1, 1, 30, -300) }, 0, false);
Check(p.HeldByFixed == 0 && p.Moves.All(m => m.Key != 0), "locked unit never moved");

// 7. Already in place: no move sent.
p = Planner.Make(saved, new List<BoardUnit> { U(0, 1, -95, -205), U(1, 1, -75, -205), U(2, 7, 120, -250, rot: true) }, 0, false);
Check(p.Moves.Count == 0 && p.AlreadyThere == 3, "nothing to do");
// Same spot but not rotated: one move (rotation only).
p = Planner.Make(saved, new List<BoardUnit> { U(2, 7, 120, -250, rot: false) }, 0, false);
Check(p.Moves.Count == 1 && p.Moves[0].Rot, "rotation-only move");

// 8. Which layouts the panel lists: every unit type in the layout on the board.
var have = Planner.Counts(new List<BoardUnit> { U(0, 1, 0, 0), U(1, 7, 0, 0), U(2, 22, 0, 0) });
Check(have[1] == 1 && have[7] == 1 && have[22] == 1, "counts");
Check(Planner.Fits(saved, have), "fits: crawlers + fortress on the board (extra unit fine)");
Check(!Planner.Fits(saved, Planner.Counts(new List<BoardUnit> { U(0, 1, 0, 0), U(1, 1, 0, 0) })), "doesn't fit: no fortress");
Check(!Planner.Fits(new Layout(), have), "an empty layout never fits");
Check(Planner.Fillable(saved, have) == 2, "fillable: 1 of 2 crawler spots + the fortress");
Check(Planner.Fillable(saved, Planner.Counts(new List<BoardUnit> { U(0, 1, 0, 0), U(1, 1, 0, 0), U(2, 1, 0, 0), U(3, 7, 0, 0) })) == 3, "fillable caps at the spots");
Check(Planner.CleanName("  Left flank rush \n") == "Left flank rush", "name trimmed, control characters out");
Check(Planner.CleanName(new string('a', 40)).Length == Planner.MaxName, "name capped");
Check(Planner.CleanName("   ") == "", "blank name = keep the old one");

// 9. 0.3.0 saves footprints and the territory (x relative to the centre), for the preview.
var withArea = Planner.Capture("a", new List<BoardUnit> { new BoardUnit { Key = 0, Unit = 7, X = 740, Y = -250, W = 40, H = 30 } }, 730, null, new[] { 360, -320, 1100, -20 });
Check(withArea.Slots[0].W == 40 && withArea.Slots[0].H == 30, "footprint saved");
Check(withArea.Area != null && withArea.Area.SequenceEqual(new[] { -370, -320, 370, -20 }), "area saved relative to the centre");
Check(Planner.Capture("b", new List<BoardUnit>(), 0).Area == null, "no area given = none saved");

// 10. Mini-map preview geometry (0.4.0).
(int w, int h)? Fp(int id) => id == 1 ? (50, 20) : id == 7 ? (40, 40) : ((int, int)?)null;
var pl = new Layout { Slots = {
    new Slot { Unit = 1, X = -100, Y = -60 },                       // front-ish, left, old save (no w/h)
    new Slot { Unit = 1, X = -100, Y = -200, Rot = true },          // further back, rotated
    new Slot { Unit = 7, X = 150, Y = -250, W = 40, H = 40 },
    new Slot { Unit = 99, X = 0, Y = -150 } } };                   // unknown unit, no footprint
int[] area = { -370, -320, 370, -20 };                              // a territory below y = 0
var pic = Preview.Make(pl, false, area, 370, Fp);                   // scale 0.5
Check(Math.Abs(pic.Height - 150) < 0.01 && Math.Abs(pic.CenterX - 185) < 0.01, "frame size and centre line");
var b0 = pic.Boxes[0]; var b1 = pic.Boxes[1];
Check(b0.Y < b1.Y, "front at the top: the unit nearer y = 0 is drawn higher");
Check(Math.Abs(b0.W - 25) < 0.01 && Math.Abs(b0.H - 10) < 0.01, "old save: dump footprint");
Check(Math.Abs(b1.W - 10) < 0.01 && Math.Abs(b1.H - 25) < 0.01, "old save, rotated: footprint turned");
Check(Math.Abs(pic.Boxes[3].W - Preview.DefaultSize * 0.5f) < 0.01, "unknown unit: default size");
Check(b0.X + b0.W < pic.CenterX && pic.Boxes[2].X > pic.CenterX, "view frame: -x on the left, +x on the right (0.6.0)");
var mir = Preview.Make(pl, true, area, 370, Fp);
Check(Math.Abs((mir.Boxes[0].X + mir.Boxes[0].W / 2) - (2 * pic.CenterX - (b0.X + b0.W / 2))) < 0.01, "mirrored: x reflected around the centre line");
Check(Math.Abs(mir.Boxes[0].Y - b0.Y) < 0.01, "mirrored: depth unchanged");
Check(pic.Legend.SequenceEqual(new[] { (1, 2), (7, 1), (99, 1) }), "legend: units in order, counted");
// A territory above y = 0: turned 180 degrees, so the front (toward y = 0) is still on top.
var up = new Layout { Slots = { new Slot { Unit = 7, X = -100, Y = 60, W = 40, H = 40 }, new Slot { Unit = 7, X = -100, Y = 250, W = 40, H = 40 } } };
var upic = Preview.Make(up, false, new[] { -370, 20, 370, 320 }, 370, Fp);
Check(upic.Boxes[0].Y < upic.Boxes[1].Y, "turned: front still at the top");
Check(upic.Boxes[0].X > upic.CenterX, "turned: x flips with the picture");
Check(Preview.Make(pl, false, null, 370, Fp).Boxes.All(b => b.X >= 0 && b.X + b.W <= 370), "no area: units fit the frame");
// The game's territory bound is the whole map: crop to the units' half (0.4.1), so the frame is wide.
var whole = Preview.Make(pl, false, new[] { -360, -310, 360, 310 }, 360, Fp);
Check(Math.Abs(whole.Height - 155) < 0.01, "whole map cropped to your half: 720 x 310 -> 360 x 155");
Check(whole.Boxes.All(b => b.Y >= 0 && b.Y + b.H <= whole.Height), "units inside the cropped frame");
var wholeUp = Preview.Make(up, false, new[] { -360, -310, 360, 310 }, 360, Fp);
Check(Math.Abs(wholeUp.Height - 155) < 0.01 && wholeUp.Boxes[0].Y < wholeUp.Boxes[1].Y, "units above y = 0: that half, front on top");

// 11. Share codes (0.5.0).
var shareSrc = new Layout { Name = "Left flank", Area = new[] { -360, -310, 360, 310 }, Slots = {
    new Slot { Unit = 10, X = -200, Y = -105, Rot = true, Px = 0, W = 20, H = 50 },
    new Slot { Unit = 30, X = 140, Y = -80, Rot = false, Px = 5, W = 40, H = 20 } } };
var code = Share.Encode(shareSrc);
Check(code.StartsWith("RWCF1.") && code.Length < 200 && code.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_'), $"code is one short URL-safe line ({code.Length} chars)");
Check(Share.TryDecode("gg, try this: " + code + " (my opener)", out var back2, out _) && back2.Name == "Left flank"
      && Share.SameSpots(back2, shareSrc) && back2.Slots[0].W == 20 && back2.Slots[0].Rot && back2.Area.SequenceEqual(shareSrc.Area), "round trip, found inside a message");
// Python (share_code.py) made this one: same format both ways.
const string PyCode = "RWCF1.FYmxCYAwFERXkasv8GOIYloXsA8pLOzEQkERcSDncDHP5t7x3oUdyRMLEoaz6tfxmKd1q94HxIiUXWiMLnjjf8RCbPJZxtWm8RbpaayNUTWLXaRrTS5KKZRyfw";
Check(Share.TryDecode(PyCode, out var py, out _) && py.Name == "Py Crawlers é" && py.Slots.Count == 2 && py.Slots[0].Unit == 10
      && py.Slots[0].X == -200 && py.Slots[0].Rot && py.Slots[1].Px == 5 && py.Slots[1].W == 50, "decodes a Python-made code");
File.WriteAllText(Path.Combine(Path.GetTempPath(), "rwcf-cs-code.txt"), code);
// Damaged or hostile input: refused with a reason, never thrown.
Check(!Share.TryDecode("", out _, out var e1) && e1.Contains("empty"), "empty clipboard");
Check(!Share.TryDecode("hello there", out _, out var e2) && e2.Contains("no RWCF1"), "no code");
Check(!Share.TryDecode(code.Substring(0, code.Length / 2), out _, out var e3) && e3.Length > 0, "half a code");
Check(!Share.TryDecode("RWCF1.!!!!", out _, out _), "garbage");
string Pack(string json)
{
    using var ms = new MemoryStream();
    using (var d = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) d.Write(System.Text.Encoding.UTF8.GetBytes(json));
    return "RWCF1." + Convert.ToBase64String(ms.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
Check(!Share.TryDecode(Pack("{\"v\":3,\"s\":[[1,0,0,0,5,0,0]]}"), out _, out var e4) && e4.Contains("newer version"), "unknown version refused");
// 0.6.0: view-frame layouts travel as v2 and come back in the view frame; v1 (0.5.0) codes stay standard space.
var viewSrc = new Layout { Name = "View", Frame = Layout.ViewFrame, Slots = { new Slot { Unit = 10, X = -200, Y = -105, Rot = true, W = 20, H = 50 } } };
Check(Share.TryDecode(Share.Encode(viewSrc), out var viewBack, out _) && viewBack.Frame == Layout.ViewFrame && Share.SameSpots(viewBack, viewSrc), "v2 code keeps the view frame");
Check(back2.Frame == null && py.Frame == null, "v1 codes stay standard space");
Check(!Share.SameSpots(viewBack, new Layout { Slots = { new Slot { Unit = 10, X = -200, Y = -105, Rot = true } } }), "same numbers in another frame are not the same layout");
Check(Planner.Capture("c", new List<BoardUnit> { U(0, 1, 5, -300) }, 0).Frame == Layout.ViewFrame, "new saves are view frame");
Check(!Share.TryDecode(Pack("{\"v\":1,\"s\":[[1,99999,0,0,5,0,0]]}"), out _, out var e5) && e5.Contains("range"), "far-off coordinates refused");
Check(!Share.TryDecode(Pack("{\"v\":1,\"s\":[]}"), out _, out _), "no units refused");
Check(!Share.TryDecode(Pack("{\"v\":1,\"s\":[" + string.Join(",", Enumerable.Repeat("[1,0,0,0,5,0,0]", 301)) + "]}"), out _, out var e6) && e6.Contains("many"), "too many units refused");
Check(!Share.TryDecode(Pack("{\"v\":1,\"pad\":\"" + new string('a', 70000) + "\",\"s\":[[1,0,0,0,5,0,0]]}"), out _, out var e7) && e7.Contains("big"), "oversized unpack refused");
Check(Share.TryDecode(Pack("{\"v\":1,\"n\":\"  \\u0007Bad\\nname that is far too long for the panel \",\"s\":[[1,0,0,0,5,0,0]]}"), out var cleaned, out _)
      && cleaned.Name.Length <= Planner.MaxName && !cleaned.Name.Any(char.IsControl), "names cleaned and capped");
Check(Share.TryDecode(Pack("{\"v\":1,\"s\":[[1,0,0,0,5,0,0]]}"), out var noName, out _) && noName.Name == "Shared layout" && noName.Area == null, "no name / no area");

// 12. Version safety (0.8.0).
var kb = Builds.Known[0];
Check(Builds.Check(kb.Version, kb.AssemblySize) == Builds.Verdict.Known, "known build runs");
Check(Builds.Check(kb.Version, -1) == Builds.Verdict.Known, "GameAssembly unreadable: decided on the version");
Check(Builds.Check(kb.Version, kb.AssemblySize + 512) == Builds.Verdict.ChangedCode, "same version, new code: off");
Check(Builds.Check("2.0.0.5.2340", kb.AssemblySize) == Builds.Verdict.UnknownVersion, "new version: off");
Check(Builds.Check(null, kb.AssemblySize) == Builds.Verdict.Unreadable && Builds.Check(" ", 1) == Builds.Verdict.Unreadable, "no version: off");
Check(Builds.Notice(Builds.Verdict.Known, kb.Version, "0.8.0") == null, "no notice on a known build");
Check(Builds.Notice(Builds.Verdict.UnknownVersion, "2.0.0.5.2340", "0.8.0").Contains("2.0.0.5.2340"), "notice names the build");
Check(Builds.VersionFromSettings(System.Text.Encoding.ASCII.GetBytes("\0\0GameRiver\0Mechabellum\0\x01 2.0.0.4.2331\0 2022.3.62f3")) == "2.0.0.4.2331", "version found in settings bytes (not the Unity version)");
Check(Builds.VersionFromSettings(new byte[] { 1, 2, 3 }) == null, "no version in junk");
// Real data, read-only: this PC's game install must count as a known build.
const string RealGame = @"C:\Program Files (x86)\Steam\steamapps\common\Mechabellum";
if (Directory.Exists(RealGame))
{
    var realVersion = Builds.VersionFromSettingsFile(RealGame);
    var realSize = Builds.AssemblySize(RealGame);
    Check(realVersion == kb.Version && realSize == kb.AssemblySize && Builds.Check(realVersion, realSize) == Builds.Verdict.Known,
          $"this PC's game ({realVersion}, {realSize} bytes) is a known build");
}
Check(Builds.AssemblySize(Path.GetTempPath()) == -1, "no GameAssembly.dll: size -1");

// 13. Panel text in English and Chinese (0.9.0).
Check(Tx.ChineseFor(null, "zh-CN") && Tx.ChineseFor("", "zh-Hans") && Tx.ChineseFor(null, "ZH-tw"), "Chinese game language = Chinese panel");
Check(!Tx.ChineseFor(null, "en") && !Tx.ChineseFor(null, null) && !Tx.ChineseFor(null, "ja-JP"), "other languages = English panel");
Check(Tx.ChineseFor("zh\n", "en") && !Tx.ChineseFor(" EN ", "zh-CN"), "the override file wins");
// Every reason the board and share codes can give has a Chinese version (reads the sources).
var keys = new HashSet<string>(Tx.ReasonKeys);
string Src(string f) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", f));
var reasonLiterals = System.Text.RegularExpressions.Regex.Matches(Src("GameBoard.cs") + Src("Share.cs"), @"(?:why|error) = ""([^""]+)""")
    .Select(m => m.Groups[1].Value).Distinct().ToList();
Check(reasonLiterals.Count >= 12, $"found the reasons in the sources ({reasonLiterals.Count})");
foreach (var r in reasonLiterals) Check(keys.Contains(r), $"Chinese for reason \"{r}\"");
// Every panel string differs in Chinese (nothing left untranslated).
var props = typeof(Tx).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
    .Where(pp => pp.PropertyType == typeof(string) && pp.Name != "Zh").ToList();
Tx.Zh = false; var en = props.ToDictionary(pp => pp.Name, pp => (string)pp.GetValue(null));
Tx.Zh = true; var zh = props.ToDictionary(pp => pp.Name, pp => (string)pp.GetValue(null));
foreach (var pp in props) Check(en[pp.Name] != zh[pp.Name], $"Tx.{pp.Name} is translated");
Check(Tx.Saved("Rush", 7).Contains("“Rush”") && Tx.Saved("Rush", 7).Contains("7"), "Chinese message keeps the layout name and numbers");
Check(Tx.Result(Tx.Placing("A", true), new[] { Tx.Moved(3), Tx.Missing(1) }) == "“A”（翻转）：移动 3 个，1 个位置没有对应的单位。", "Chinese result sentence");
// The 201 review (0.9.1): nothing says 镜像 any more, and the row's Flip and Share buttons differ.
Check(zh.Values.All(v => !v.Contains("镜像")) && !Tx.Placing("A", true).Contains("镜像") && !Tx.PreviewTitle("A", true).Contains("镜像"), "no 镜像 left (Mirrored = 翻转)");
Check(Tx.Mirrored == "翻转" && Tx.MirrorMine == "翻转我的单位" && Tx.Copy == "分享" && Tx.CopyFailed("x").StartsWith("分享失败"), "reviewed words: 翻转 and 分享");
Check(Tx.Reason("error: boom") == "error: boom", "unknown reasons pass through");
Check(UnitInfo.Name(10) == "爬虫" && UnitInfo.Name(32) == "百夫长" && UnitInfo.Name(99999) == "单位 99999", "Chinese unit names");
Check(Tx.Notice(Builds.Verdict.UnknownVersion, "2.0.0.5.2340", "0.9.0").Contains("2.0.0.5.2340"), "Chinese off notice names the build");
Tx.Zh = false;
Check(UnitInfo.Name(10) == "Crawler" && UnitInfo.Name(99999) == "Unit 99999", "English unit names");
Check(Tx.Result(Tx.Placing("A", true), new[] { Tx.Moved(3), Tx.Missing(1) }) == "\"A\" mirrored: 3 moved, 1 saved spots with no unit for them.", "English result sentence unchanged");
Check(Tx.Notice(Builds.Verdict.ChangedCode, "x", "0.9.0") == Builds.Notice(Builds.Verdict.ChangedCode, "x", "0.9.0"), "English notice = Builds.Notice");

// 14. Layouts file round trip.
var path = Path.Combine(Path.GetTempPath(), "rwc-formations-test.json");
if (File.Exists(path)) File.Delete(path);
var file = new LayoutFile();
file.Layouts.Add(saved);
file.Save(path);
file.Save(path);   // second save goes through File.Replace
var read = LayoutFile.Load(path);
Check(read.Layouts.Count == 1 && read.Layouts[0].Name == "test" && read.Layouts[0].Slots.Count == 3
      && read.Layouts[0].Slots[2].Rot && read.Layouts[0].Slots[0].X == -95, "file round trip");
Check(LayoutFile.Load(path + ".missing").Layouts.Count == 0, "missing file = empty list");
File.Delete(path);

Console.WriteLine($"{checks - fails}/{checks} checks passed");
return fails == 0 ? 0 : 1;
