// RWC Formations: the mini-map preview's geometry (0.4.0). Game-free, so
// formation-test/ checks it; Plugin.cs draws the boxes.
//
// A layout drawn the way you see your side of the board: the front (toward the
// enemy) at the top, +x on the right. Layouts reach here in the VIEW frame
// (GameBoard.InView, 0.6.0), where your side is below y = 0 and +x is right on
// screen for either seat. (0.4.0 / 0.4.1 drew the game's standard space, which is
// mirrored for one seat: each looked wrong to Kris in a game on the other side.)
// A frame above y = 0 is turned 180 degrees. Mirrored = left and right swapped
// around the centre, as Place does. An area spanning both halves (the old saved
// territory = the whole map) is cropped to the units' half at y = 0.
using System;
using System.Collections.Generic;
using System.Linq;

namespace RwcFormations
{
    public struct PreviewBox
    {
        public int Unit;
        public float X, Y, W, H;   // pixels from the frame's top-left
    }

    public class PreviewPicture
    {
        public float Width, Height;                       // the board frame, pixels
        public float CenterX;                             // the territory centre line, pixels from the left
        public List<PreviewBox> Boxes = new List<PreviewBox>();
        public List<(int unit, int count)> Legend = new List<(int, int)>();   // in order of first appearance
    }

    public static class Preview
    {
        public const int DefaultSize = 20;   // a unit with no footprint anywhere

        // footprint(unit id) = the dump's (x, y) footprint, or null. area = the
        // territory [xMin, yMin, xMax, yMax], x relative to its centre (the board
        // you're on now, else the one saved with the layout); null = fit the units.
        public static PreviewPicture Make(Layout l, bool mirror, int[] area, float width, Func<int, (int w, int h)?> footprint)
        {
            var boxes = new List<(int unit, float x, float y, float w, float h)>();   // map units, centre + size
            foreach (var s in l.Slots)
            {
                float w = s.W, h = s.H;
                if (w <= 0 || h <= 0)
                {
                    // Saved before 0.3.0: the dump's footprint, turned when the placement is.
                    var fp = footprint(s.Unit);
                    (w, h) = fp.HasValue ? (fp.Value.w, fp.Value.h) : (DefaultSize, DefaultSize);
                    if (s.Rot) (w, h) = (h, w);
                }
                boxes.Add((s.Unit, mirror ? -s.X : s.X, s.Y, w, h));
            }

            float x0, y0, x1, y1;
            if (area != null && area.Length == 4)
            {
                x0 = area[0]; y0 = area[1]; x1 = area[2]; y1 = area[3];
                if (y0 < 0 && y1 > 0)
                {
                    // Both halves: keep the side the units are on (below y = 0 when there are none).
                    bool above = boxes.Count > 0 && boxes.Average(b => b.y) > 0;
                    if (above) y0 = 0; else y1 = 0;
                }
            }
            else if (boxes.Count > 0)
            {
                // No territory known: the units' extent plus a margin, centred on the centre line.
                float half = boxes.Max(b => Math.Abs(b.x) + b.w / 2) + 40;
                x0 = -half; x1 = half;
                y0 = boxes.Min(b => b.y - b.h / 2) - 40; y1 = boxes.Max(b => b.y + b.h / 2) + 40;
            }
            else { x0 = -100; x1 = 100; y0 = -100; y1 = 100; }

            // Front at the top: territory below y = 0 faces +y; above it, turn the picture.
            bool turn = (y0 + y1) / 2 > 0;
            float scale = width / Math.Max(1, x1 - x0);
            var pic = new PreviewPicture { Width = width, Height = (y1 - y0) * scale };
            pic.CenterX = (turn ? x1 : -x0) * scale;   // x = 0 in the frame
            foreach (var b in boxes)
            {
                // Pixel position of the box centre.
                float px = turn ? (x1 - b.x) * scale : (b.x - x0) * scale;   // +x on the right
                float py = turn ? (b.y - y0) * scale : (y1 - b.y) * scale;
                pic.Boxes.Add(new PreviewBox { Unit = b.unit, X = px - b.w * scale / 2, Y = py - b.h * scale / 2, W = b.w * scale, H = b.h * scale });
            }
            foreach (var s in l.Slots)
            {
                int i = pic.Legend.FindIndex(e => e.unit == s.Unit);
                if (i < 0) pic.Legend.Add((s.Unit, 1));
                else pic.Legend[i] = (s.Unit, pic.Legend[i].count + 1);
            }
            return pic;
        }
    }
}
