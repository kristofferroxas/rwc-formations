// RWC Formations 0.7.0: save your unit positioning and put it back with a click,
// as saved or mirrored left to right. The panel: loader-free (0.7.0), driven by
// BepInExPlugin.cs or MelonMod.cs. Separate from the sim plugin (RwcSimProbe).
//
// In a game, during deployment:
//   F6                    show / hide the panel
//   panel                 Save current (then type a name, Enter), Mirror my units, Revert
//                         (undo the last Place / Mirror, as many steps as you made), and per
//                         saved layout Place / Mirrored / Name / x (hover a row = mini-map
//                         preview beside the panel; hover Mirrored = mirrored), Copy (the layout
//                         as a share code on the clipboard, Share.cs) and Paste (add one). The list shows only the
//                         layouts whose unit types are all on your board ("Show all" for the rest).
//   Ctrl+Alt+1..9         place listed layout 1..9 (Ctrl+Alt+Shift = mirrored)
// Layouts live in rwc-formations.json in the loader's data folder (Host.DataDir:
// BepInEx/ or UserData/). Clicks on the panel don't reach the
// map (ClickBlock.cs).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RwcFormations
{
    public class Panel
    {
        static string FilePath => Path.Combine(Host.DataDir, "rwc-formations.json");
        bool _started;

        LayoutFile _file;
        bool _shown = true;
        int _corner = 1;                 // 0 top-left, 1 top-right, 2 bottom-right, 3 bottom-left
        string _message = "";
        string _status = "";             // why the buttons are off, or "" when deploying
        bool _ready;
        Dictionary<int, int> _have;      // unit id -> count on your board (null when not deploying)
        int[] _areaRel;                  // your deployment area now, view frame, x relative to its centre (null when not deploying)
        Board _board;                    // the last board read (for the preview), null when not deploying
        readonly Dictionary<Layout, Layout> _viewCache = new Dictionary<Layout, Layout>();

        // A layout as the preview should draw it: in the view frame for your board.
        Layout PreviewOf(Layout l)
        {
            if (_board == null || l.Frame == Layout.ViewFrame) return l;
            if (!_viewCache.TryGetValue(l, out var v))
            {
                try { v = GameBoard.InView(_board, l); } catch { v = l; }
                _viewCache[l] = v;
            }
            return v;
        }
        bool _showAll;
        float _nextStatus;
        int _confirmDelete = -1;
        Layout _editing;                 // the layout being named, or null
        string _buf = "";
        // Revert: per Place / Mirror, where the units it moved stood before (unit
        // object pointer, standard position, rotation). Cleared when deployment ends.
        readonly List<List<(IntPtr ptr, int x, int y, bool rot)>> _undo = new List<List<(IntPtr, int, int, bool)>>();
        bool _wasReady;

        // Once, on the first frame (Unity is ready by then under both loaders).
        void Start()
        {
            _started = true;
            if (Host.OffNotice != null) return;   // unchecked game build: nothing touches the game
            try { ClickBlock.Create(); }
            catch (Exception e) { Host.Error($"Click blocker failed (the hooks still block): {e}"); }
            try { _file = LayoutFile.Load(FilePath); }
            catch (Exception e)
            {
                // Keep the unreadable file, start a fresh list beside it.
                Host.Error($"Reading {FilePath} failed: {e.Message}");
                try { File.Copy(FilePath, FilePath + ".unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")); } catch { }
                _file = new LayoutFile();
            }
        }

        // The layouts the panel lists: those that fit your board, or all of them
        // (Show all, or when there's no board to compare with).
        List<Layout> Listed() =>
            _have == null || _showAll ? _file.Layouts.ToList() : _file.Layouts.Where(l => Planner.Fits(l, _have)).ToList();

        public void Update()
        {
            if (!_started) Start();
            if (Host.OffNotice != null) { if (Input.GetKeyDown(KeyCode.F6)) _shown = !_shown; return; }
            if (Time.unscaledTime >= _nextStatus)
            {
                _nextStatus = Time.unscaledTime + 0.5f;
                try
                {
                    var b = GameBoard.Read(out var why);
                    _ready = b != null;
                    _status = why ?? "";
                    _have = b == null ? null : Planner.Counts(b.Units);
                    _areaRel = b == null ? null : new[] { b.Area[0] - b.CenterX, b.Area[1], b.Area[2] - b.CenterX, b.Area[3] };
                    _board = b;
                    _viewCache.Clear();   // old layouts convert per board (seat, area)
                }
                catch (Exception e) { _ready = false; _have = null; _areaRel = null; _board = null; _status = "error: " + e.Message; }
                if (_wasReady && !_ready && _undo.Count > 0) _undo.Clear();   // deployment over: nothing to revert to
                _wasReady = _ready;
            }
            ClickBlock.PanelShown = _shown && !_guiFailed && GRC.MatchClient.Current != null;
            try { ClickBlock.Sync(); } catch { }

            if (_editing != null)
            {
                if (!_shown || GRC.MatchClient.Current == null) { StopEditing(false); return; }
                foreach (char c in Input.inputString ?? "")
                {
                    if (c == '\b') { if (_buf.Length > 0) _buf = _buf.Substring(0, _buf.Length - 1); }
                    else if (c == '\n' || c == '\r') { StopEditing(true); break; }
                    else if (!char.IsControl(c) && _buf.Length < Planner.MaxName) _buf += c;
                }
                return;   // no mod hotkeys while typing
            }

            if (Input.GetKeyDown(KeyCode.F6)) _shown = !_shown;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (ctrl && alt)
                for (int i = 0; i < 9; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    {
                        var listed = Listed();
                        if (i < listed.Count) Place(listed[i], shift);
                        else _message = $"No layout {i + 1} in the list.";
                    }
        }

        void StartEditing(Layout l, string startText)
        {
            _editing = l;
            _buf = startText ?? "";
            _confirmDelete = -1;
            KeyBlock.Typing = true;
        }

        void StopEditing(bool keep)
        {
            if (keep && _editing != null)
            {
                var name = Planner.CleanName(_buf);
                if (name.Length > 0 && name != _editing.Name)
                {
                    var old = _editing.Name;
                    _editing.Name = name;
                    if (Persist()) _message = $"Named \"{name}\" (was \"{old}\").";
                }
            }
            _editing = null;
            _buf = "";
            KeyBlock.Typing = false;
            KeyBlock.UntilFrame = Time.frameCount + 1;
        }

        // Drawn with fixed-position GUI.Box / Label / Button only. This game build
        // has Unity's automatic layout (GUILayout), scroll views and text fields
        // stripped out ("Method unstripping failed", 0.1.0), so: pages instead of a
        // scroll list, and a name box made of a Box plus the typed keys
        // (Input.inputString).
        const int PerPage = 6;
        const float W = 460, Row = 28, Pad = 8;
        int _page;
        bool _guiFailed;

        public void OnGUI()
        {
            if (!_started) return;
            if (!_shown || _guiFailed || GRC.MatchClient.Current == null) return;
            if (Host.OffNotice != null) { DrawOff(); return; }
            try { DrawPanel(); }
            catch (Exception e)
            {
                // Once, not every frame: stop drawing and say why.
                _guiFailed = true;
                GUI.enabled = true;
                if (_editing != null) StopEditing(false);
                Host.Error($"Panel drawing failed, panel off until the game restarts (hotkeys still work): {e}");
            }
        }

        // Off mode (unchecked game build): just the notice, in the panel's corner.
        void DrawOff()
        {
            try
            {
                const float w = 460, h = 64;
                float x0 = _corner == 0 || _corner == 3 ? 10 : Screen.width - w - 10;
                float y0 = _corner < 2 ? 90 : Screen.height - h - 90;
                GUI.Box(new Rect(x0, y0, w, h), "");
                GUI.Label(new Rect(x0 + 8, y0 + 6, w - 74, h - 12), Host.OffNotice + " (F6 hides this.)");
                if (GUI.Button(new Rect(x0 + w - 60, y0 + 8, 52, 24), "Hide")) _shown = false;
            }
            catch (Exception e) { _guiFailed = true; Host.Error($"Notice drawing failed: {e.Message}"); }
        }

        void DrawPanel()
        {
            var listed = Listed();
            int pages = Math.Max(1, (listed.Count + PerPage - 1) / PerPage);
            _page = Math.Min(_page, pages - 1);
            float h = Pad * 2 + Row * (7 + PerPage) + 8;
            float x0 = _corner == 0 || _corner == 3 ? 10 : Screen.width - W - 10;
            float y0 = _corner < 2 ? 90 : Screen.height - h - 90;
            ClickBlock.PanelRect = new Rect(x0, y0, W, h);
            GUI.Box(new Rect(x0, y0, W, h), "");
            float x = x0 + Pad, y = y0 + Pad, iw = W - Pad * 2;

            GUI.Label(new Rect(x, y, iw - 186, Row), $"RWC Formations {Host.Version}");
            GUI.enabled = _editing == null;
            if (GUI.Button(new Rect(x + iw - 180, y, 72, Row - 4), "Paste")) PasteLayout();
            GUI.enabled = true;
            if (GUI.Button(new Rect(x + iw - 104, y, 50, Row - 4), "Move")) _corner = (_corner + 1) % 4;
            if (GUI.Button(new Rect(x + iw - 50, y, 50, Row - 4), "Hide")) { if (_editing != null) StopEditing(false); _shown = false; }
            y += Row;
            GUI.Label(new Rect(x, y, iw, Row), _ready ? "Deploying: buttons on. F6 hides." : "Off: " + _status);
            y += Row;

            GUI.enabled = _ready && _editing == null;
            if (GUI.Button(new Rect(x, y, 112, Row - 4), "Save current")) SaveCurrent();
            if (GUI.Button(new Rect(x + 116, y, 132, Row - 4), "Mirror my units")) MirrorCurrent();
            GUI.enabled = _ready && _editing == null && _undo.Count > 0;
            if (GUI.Button(new Rect(x + 252, y, iw - 252, Row - 4), _undo.Count > 1 ? $"Revert ({_undo.Count})" : "Revert")) Revert();
            GUI.enabled = true;
            y += Row;
            GUI.Label(new Rect(x, y, iw, Row * 2), _message ?? "");
            y += Row * 2;

            if (_editing != null)
            {
                // The name box: what's typed so far, with a blinking cursor.
                GUI.Label(new Rect(x, y, 48, Row), "Name:");
                bool blink = ((int)(Time.unscaledTime * 2)) % 2 == 0;
                string shown = _buf.Length == 0 ? $"type a name ({_editing.Name})" : _buf + (blink ? "|" : " ");
                GUI.Box(new Rect(x + 50, y, iw - 50 - 128, Row - 4), shown);
                if (GUI.Button(new Rect(x + iw - 124, y, 50, Row - 4), "OK")) StopEditing(true);
                if (GUI.Button(new Rect(x + iw - 70, y, 70, Row - 4), "Cancel")) StopEditing(false);
            }
            else if (_have != null)
            {
                int hidden = _file.Layouts.Count - _file.Layouts.Count(l => Planner.Fits(l, _have));
                GUI.Label(new Rect(x, y, iw - 104, Row),
                    _showAll ? $"All {_file.Layouts.Count} layouts" : $"{listed.Count} fit your board" + (hidden > 0 ? $", {hidden} hidden" : ""));
                if ((_showAll || hidden > 0) && GUI.Button(new Rect(x + iw - 100, y, 100, Row - 4), _showAll ? "Fitting only" : "Show all"))
                {
                    _showAll = !_showAll;
                    _page = 0;
                    _confirmDelete = -1;
                }
            }
            else GUI.Label(new Rect(x, y, iw, Row), $"All {_file.Layouts.Count} layouts");
            y += Row;

            if (listed.Count == 0)
                GUI.Label(new Rect(x, y, iw, Row * 2), _file.Layouts.Count == 0 ? "No layouts saved yet." : "None of your layouts fit the units on your board.");
            var mp = Input.mousePosition;
            var mouse = new Vector2(mp.x, Screen.height - mp.y);
            Layout hover = null;
            bool hoverMirror = false;
            for (int k = 0; k < PerPage; k++)
            {
                int i = _page * PerPage + k;
                if (i >= listed.Count) break;
                var l = listed[i];
                float ry = y + Row * k;
                if (new Rect(x, ry, iw, Row).Contains(mouse))
                {
                    hover = l;
                    hoverMirror = new Rect(x + iw - 226, ry, 70, Row - 4).Contains(mouse);
                }
                string count = _have == null ? $"{l.Slots.Count}" : $"{Planner.Fillable(l, _have)} of {l.Slots.Count}";
                if (_have != null && Planner.Fillable(l, _have) == l.Slots.Count) count = $"{l.Slots.Count}";
                GUI.Label(new Rect(x, ry, iw - 286, Row), $"{i + 1}. {l.Name} ({count})");
                GUI.enabled = _ready && _editing == null;
                if (GUI.Button(new Rect(x + iw - 282, ry, 52, Row - 4), "Place")) Place(l, false);
                if (GUI.Button(new Rect(x + iw - 226, ry, 70, Row - 4), "Mirrored")) Place(l, true);
                GUI.enabled = _editing == null;
                if (GUI.Button(new Rect(x + iw - 152, ry, 54, Row - 4), "Name")) StartEditing(l, l.Name);
                if (GUI.Button(new Rect(x + iw - 94, ry, 50, Row - 4), "Copy")) CopyLayout(l);
                if (GUI.Button(new Rect(x + iw - 40, ry, 40, Row - 4), _confirmDelete == _file.Layouts.IndexOf(l) ? "sure?" : "x"))
                {
                    int fi = _file.Layouts.IndexOf(l);
                    if (_confirmDelete == fi) { _file.Layouts.RemoveAt(fi); _confirmDelete = -1; Persist(); GUI.enabled = true; return; }
                    _confirmDelete = fi;
                }
                GUI.enabled = true;
            }
            if (pages > 1)
            {
                float py = y + Row * PerPage;
                if (GUI.Button(new Rect(x, py, 40, Row - 4), "<") && _page > 0) _page--;
                GUI.Label(new Rect(x + 50, py, 120, Row), $"page {_page + 1} of {pages}");
                if (GUI.Button(new Rect(x + 170, py, 40, Row - 4), ">") && _page < pages - 1) _page++;
            }
            if (hover != null && _editing == null) DrawPreview(hover, hoverMirror, x0, y0);
        }

        // The mini-map (0.4.0): beside the panel while a layout's row is hovered.
        // Only shown while the mouse is on the panel, so it never needs click blocking.
        const float PreviewW = 360;
        static readonly Color[] Palette =
        {
            new Color(0.22f, 0.53f, 0.90f), new Color(0.84f, 0.32f, 0.51f), new Color(0.79f, 0.52f, 0f),
            new Color(0.25f, 0.70f, 0.50f), new Color(0.61f, 0.42f, 0.84f), new Color(0.88f, 0.42f, 0.24f),
            new Color(0.31f, 0.72f, 0.78f), new Color(0.78f, 0.78f, 0.31f),
        };

        void DrawPreview(Layout l, bool mirror, float panelX, float panelY)
        {
            var pic = Preview.Make(PreviewOf(l), mirror, _areaRel ?? l.Area, PreviewW,
                id => UnitInfo.Units.TryGetValue(id, out var u) && u.W > 0 ? (u.W, u.H) : ((int, int)?)null);
            int legendRows = (pic.Legend.Count + 1) / 2;
            float bw = PreviewW + Pad * 2;
            float bh = Pad + Row + Math.Min(pic.Height, 420) + 6 + legendRows * 22 + Pad;
            float bx = _corner == 0 || _corner == 3 ? panelX + W + 6 : panelX - bw - 6;
            bx = Math.Max(0, Math.Min(bx, Screen.width - bw));
            GUI.Box(new Rect(bx, panelY, bw, bh), "");
            float fx = bx + Pad, fy = panelY + Pad;
            GUI.Label(new Rect(fx, fy, PreviewW, Row), $"{l.Name}{(mirror ? " (mirrored)" : "")}: front is at the top");
            fy += Row;

            var white = Texture2D.whiteTexture;
            var old = GUI.color;
            try
            {
                float fh = Math.Min(pic.Height, 420);
                GUI.color = new Color(0f, 0f, 0f, 0.45f);
                GUI.DrawTexture(new Rect(fx, fy, PreviewW, fh), white);
                GUI.color = new Color(1f, 1f, 1f, 0.18f);
                GUI.DrawTexture(new Rect(fx + pic.CenterX, fy, 1, fh), white);   // the centre line Mirrored swaps around
                foreach (var b in pic.Boxes)
                {
                    if (b.Y > fh) continue;
                    int ci = pic.Legend.FindIndex(e => e.unit == b.Unit);
                    GUI.color = Palette[ci % Palette.Length];
                    GUI.DrawTexture(new Rect(fx + b.X, fy + b.Y, Math.Max(2, b.W - 1), Math.Max(2, b.H - 1)), white);
                }
                fy += fh + 6;
                for (int i = 0; i < pic.Legend.Count; i++)
                {
                    float lx = fx + (i % 2) * (PreviewW / 2), ly = fy + (i / 2) * 22;
                    GUI.color = Palette[i % Palette.Length];
                    GUI.DrawTexture(new Rect(lx, ly + 5, 12, 12), white);
                    GUI.color = old;
                    var (unit, count) = pic.Legend[i];
                    GUI.Label(new Rect(lx + 18, ly, PreviewW / 2 - 20, 22), $"{UnitInfo.Name(unit)} x{count}");
                }
            }
            finally { GUI.color = old; }
        }

        void SaveCurrent()
        {
            var b = GameBoard.Read(out var why);
            if (b == null) { _message = "Can't save: " + why; return; }
            if (b.Units.Count == 0) { _message = "No units on the board to save."; return; }
            var name = NextName();
            var layout = GameBoard.Capture(b, name);
            _file.Layouts.Add(layout);
            _showAll = false;
            _have = Planner.Counts(b.Units);
            _page = Math.Max(0, (Listed().IndexOf(layout)) / PerPage);
            GameBoard.LogBoard(b);
            if (Persist())
            {
                _message = $"Saved \"{name}\" ({layout.Slots.Count} units). Type a name and press Enter, or OK to keep it.";
                StartEditing(layout, "");
            }
        }

        void MirrorCurrent()
        {
            var b = GameBoard.Read(out var why);
            if (b == null) { _message = "Can't mirror: " + why; return; }
            Run(b, GameBoard.Capture(b, "current"), true, "Mirrored your units");
        }

        void Place(Layout l, bool mirror)
        {
            var b = GameBoard.Read(out var why);
            if (b == null) { _message = "Can't place: " + why; return; }
            Run(b, GameBoard.InView(b, l), mirror, $"\"{l.Name}\"{(mirror ? " mirrored" : "")}");
        }

        void Run(Board b, Layout l, bool mirror, string what)
        {
            try
            {
                var plan = Planner.Make(l, b.Units, b.CenterX, mirror);
                var before = plan.Moves.ToDictionary(m => m.Key, m => (b.Elements[m.Key].Pointer, b.Units[m.Key].X, b.Units[m.Key].Y, b.Units[m.Key].Rot));
                var r = GameBoard.Apply(b, plan);
                if (r.Done.Count > 0) _undo.Add(r.Done.Select(m => before[m.Key]).ToList());
                var parts = new List<string> { $"{r.Moved} moved" };
                if (plan.AlreadyThere > 0) parts.Add($"{plan.AlreadyThere} already in place");
                if (r.Failed > 0) parts.Add($"{r.Failed} refused by the game ({r.Detail})");
                int missing = plan.MissingUnits.Values.Sum();
                if (missing > 0) parts.Add($"{missing} saved spots with no unit for them");
                if (plan.LeftAlone > 0) parts.Add($"{plan.LeftAlone} units not in the layout left alone");
                _message = $"{what}: {string.Join(", ", parts)}.";
                Host.Info(_message);
            }
            catch (Exception e)
            {
                _message = "Failed: " + e.Message;
                Host.Error($"{what} failed: {e}");
            }
        }

        // Share codes (0.5.0, Share.cs): one line of text for Discord.
        void CopyLayout(Layout l)
        {
            try
            {
                var code = Share.Encode(l);
                GUIUtility.systemCopyBuffer = code;
                _message = $"Copied \"{l.Name}\" as a share code ({code.Length} characters). Paste it in Discord; friends add it with Paste.";
            }
            catch (Exception e) { _message = "Copy failed: " + e.Message; Host.Error($"Copy failed: {e}"); }
        }

        void PasteLayout()
        {
            string text;
            try { text = GUIUtility.systemCopyBuffer; }
            catch (Exception e) { _message = "Couldn't read the clipboard: " + e.Message; return; }
            if (!Share.TryDecode(text, out var l, out var error)) { _message = "Paste: " + error + "."; return; }
            var same = _file.Layouts.FirstOrDefault(x => Share.SameSpots(x, l));
            if (same != null) { _message = $"You already have this layout as \"{same.Name}\"."; return; }
            var name = l.Name;
            for (int n = 2; _file.Layouts.Any(x => x.Name == l.Name); n++) l.Name = Planner.CleanName($"{name} ({n})");
            _file.Layouts.Add(l);
            if (!Persist()) return;
            bool hidden = _have != null && !_showAll && !Planner.Fits(l, _have);
            if (!hidden) _page = Math.Max(0, Listed().IndexOf(l) / PerPage);
            _message = $"Added \"{l.Name}\" ({l.Slots.Count} units)" + (hidden ? ". It's hidden: you don't have its units now (Show all lists it)." : ".");
            Host.Info(_message);
        }

        // Put the units the last Place / Mirror moved back where they stood.
        void Revert()
        {
            if (_undo.Count == 0) return;
            var b = GameBoard.Read(out var why);
            if (b == null) { _message = "Can't revert: " + why; return; }
            var snap = _undo[_undo.Count - 1];
            var plan = new Plan();
            int gone = 0;
            foreach (var o in snap)
            {
                int key = b.Elements.FindIndex(e => e.Pointer == o.ptr);
                if (key < 0) { gone++; continue; }   // sold or removed since
                var u = b.Units[key];
                if (u.X == o.x && u.Y == o.y && u.Rot == o.rot) { plan.AlreadyThere++; continue; }
                plan.Moves.Add(new Move { Key = key, X = o.x, Y = o.y, Rot = o.rot });
            }
            try
            {
                var r = GameBoard.Apply(b, plan);
                _undo.RemoveAt(_undo.Count - 1);
                var parts = new List<string> { $"{r.Moved} moved back" };
                if (plan.AlreadyThere > 0) parts.Add($"{plan.AlreadyThere} already back");
                if (r.Failed > 0) parts.Add($"{r.Failed} refused by the game ({r.Detail})");
                if (gone > 0) parts.Add($"{gone} no longer on the board");
                _message = $"Reverted: {string.Join(", ", parts)}.";
                Host.Info(_message);
            }
            catch (Exception e)
            {
                _message = "Revert failed: " + e.Message;
                Host.Error($"Revert failed: {e}");
            }
        }

        bool Persist()
        {
            try { _file.Save(FilePath); return true; }
            catch (Exception e) { _message = "Saving the layouts file failed: " + e.Message; Host.Error(_message); return false; }
        }

        string NextName()
        {
            for (int n = (_file?.Layouts.Count ?? 0) + 1; ; n++)
            {
                var s = $"Layout {n}";
                if (_file == null || !_file.Layouts.Any(l => l.Name == s)) return s;
            }
        }
    }
}
