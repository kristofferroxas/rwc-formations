// RWC Formations: share codes (0.5.0). A layout as one line of text to paste in
// Discord: "RWCF1." + base64url(raw deflate(UTF-8 JSON)). Game-free, so
// formation-test/ checks it; share_code.py is the same format in Python (for the
// analyzer exporting openings from replays later).
//
// JSON (compact): {"v":2,"n":name,"a":[xMin,yMin,xMax,yMax] or absent,
//                  "s":[[unit,x,y,rot 0/1,px,w,h], ...]}
// Same coordinates as the layouts file. "v" says which: 2 = the view frame (0.6.0+,
// seat-proof), 1 = the game's standard space (0.5.0 codes; still read, converted
// like old saved layouts). A 0.5.0 mod refuses v2 codes ("update the mod").
//
// Codes come from other people, so decoding trusts nothing: the input and the
// unpacked size are capped, every number is range-checked, and the name is cleaned.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RwcFormations
{
    public static class Share
    {
        public const string Prefix = "RWCF1.";
        const int MaxInput = 20000, MaxJson = 64 * 1024, MaxSlots = 300, MaxCoord = 5000, MaxSize = 500;
        static readonly Regex Code = new Regex(@"RWCF1\.[A-Za-z0-9_-]+");

        public static string Encode(Layout l)
        {
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms))
            {
                w.WriteStartObject();
                w.WriteNumber("v", l.Frame == Layout.ViewFrame ? 2 : 1);
                w.WriteString("n", l.Name ?? "");
                if (l.Area != null && l.Area.Length == 4)
                {
                    w.WriteStartArray("a");
                    foreach (var v in l.Area) w.WriteNumberValue(v);
                    w.WriteEndArray();
                }
                w.WriteStartArray("s");
                foreach (var s in l.Slots)
                {
                    w.WriteStartArray();
                    foreach (var v in new[] { s.Unit, s.X, s.Y, s.Rot ? 1 : 0, s.Px, s.W, s.H }) w.WriteNumberValue(v);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            using var packed = new MemoryStream();
            using (var d = new DeflateStream(packed, CompressionLevel.Optimal, true)) d.Write(ms.ToArray());
            return Prefix + Convert.ToBase64String(packed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        // The first share code anywhere in the text (a whole Discord message is fine).
        public static bool TryDecode(string text, out Layout layout, out string error)
        {
            layout = null;
            error = null;
            if (string.IsNullOrEmpty(text)) { error = "the clipboard is empty"; return false; }
            if (text.Length > MaxInput) text = text.Substring(0, MaxInput);
            var m = Code.Match(text);
            if (!m.Success) { error = "no RWCF1 share code on the clipboard"; return false; }
            try
            {
                var b64 = m.Value.Substring(Prefix.Length).Replace('-', '+').Replace('_', '/');
                b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
                byte[] json;
                using (var d = new DeflateStream(new MemoryStream(Convert.FromBase64String(b64)), CompressionMode.Decompress))
                using (var outMs = new MemoryStream())
                {
                    var buf = new byte[8192];
                    int n;
                    while ((n = d.Read(buf, 0, buf.Length)) > 0)
                    {
                        if (outMs.Length + n > MaxJson) { error = "the code is too big"; return false; }
                        outMs.Write(buf, 0, n);
                    }
                    json = outMs.ToArray();
                }
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number
                    || !v.TryGetInt32(out int version) || (version != 1 && version != 2))
                { error = "a share code from a newer version of the mod (update yours)"; return false; }

                var l = new Layout { Saved = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Frame = version == 2 ? Layout.ViewFrame : null };
                l.Name = root.TryGetProperty("n", out var n0) && n0.ValueKind == JsonValueKind.String ? Planner.CleanName(n0.GetString()) : "";
                if (l.Name.Length == 0) l.Name = "Shared layout";
                if (root.TryGetProperty("a", out var a) && a.ValueKind == JsonValueKind.Array && a.GetArrayLength() == 4)
                {
                    var area = a.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int x) ? x : int.MinValue).ToArray();
                    if (area.All(x => Math.Abs((long)x) <= MaxCoord) && area[0] < area[2] && area[1] < area[3]) l.Area = area;
                }
                if (!root.TryGetProperty("s", out var s) || s.ValueKind != JsonValueKind.Array) { error = "the code has no units"; return false; }
                if (s.GetArrayLength() > MaxSlots) { error = "the code has too many units"; return false; }
                foreach (var e in s.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() != 7) { error = "a unit in the code is malformed"; return false; }
                    var f = e.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out int i) ? (int?)i : null).ToArray();
                    if (f.Any(x => x == null)) { error = "a unit in the code is malformed"; return false; }
                    int unit = f[0].Value, x0 = f[1].Value, y0 = f[2].Value, rot = f[3].Value, px = f[4].Value, w = f[5].Value, h = f[6].Value;
                    if (unit <= 0 || unit > 100000 || Math.Abs(x0) > MaxCoord || Math.Abs(y0) > MaxCoord || (rot != 0 && rot != 1)
                        || px < 0 || px > 9 || w < 0 || w > MaxSize || h < 0 || h > MaxSize)
                    { error = "a unit in the code is out of range"; return false; }
                    l.Slots.Add(new Slot { Unit = unit, X = x0, Y = y0, Rot = rot == 1, Px = px, W = w, H = h });
                }
                if (l.Slots.Count == 0) { error = "the code has no units"; return false; }
                layout = l;
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidDataException || ex is JsonException || ex is IOException)
            {
                error = "the share code is damaged (copied only part of it?)";
                return false;
            }
        }

        // Same units on the same spots (names and save times aside).
        public static bool SameSpots(Layout a, Layout b)
        {
            string Key(Layout l) => string.Join(";", l.Slots.Select(s => $"{s.Unit},{s.X},{s.Y},{(s.Rot ? 1 : 0)}").OrderBy(x => x, StringComparer.Ordinal));
            return a.Frame == b.Frame && a.Slots.Count == b.Slots.Count && Key(a) == Key(b);
        }
    }
}
