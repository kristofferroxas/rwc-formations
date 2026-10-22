"""RWC Formations share codes in Python: the same format as Share.cs, for the
analyzer exporting openings from replays later, and for checking the C# side.

"RWCF1." + base64url(raw deflate(UTF-8 JSON)), no padding.
JSON: {"v": 2, "n": name, "a": [xMin, yMin, xMax, yMax] (optional),
       "s": [[unit, x, y, rot 0/1, px, w, h], ...]}
"v" = the coordinates: 2 = the view frame (0.6.0+: the board as seen from your seat,
+x right on screen, +y toward the enemy, x relative to your deployment area's centre;
layouts-file "frame": "view"), 1 = the game's standard space (0.5.0).

    python formation-plugin/share_code.py decode "RWCF1...."
    python formation-plugin/share_code.py encode layout.json   (one layout, layouts-file shape)
"""
import base64
import json
import re
import sys
import zlib

PREFIX = "RWCF1."
CODE = re.compile(r"RWCF1\.[A-Za-z0-9_-]+")


def encode(layout):
    """layout: a dict in the layouts-file shape (name, slots, optional area)."""
    payload = {"v": 2 if layout.get("frame") == "view" else 1, "n": layout.get("name") or ""}
    if layout.get("area") and len(layout["area"]) == 4:
        payload["a"] = [int(v) for v in layout["area"]]
    payload["s"] = [[int(s["unit"]), int(s["x"]), int(s["y"]), 1 if s.get("rot") else 0,
                     int(s.get("px", 0)), int(s.get("w", 0)), int(s.get("h", 0))] for s in layout["slots"]]
    raw = json.dumps(payload, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    c = zlib.compressobj(9, zlib.DEFLATED, -15)
    packed = c.compress(raw) + c.flush()
    return PREFIX + base64.urlsafe_b64encode(packed).decode("ascii").rstrip("=")


def decode(text):
    """The first share code in text -> a dict in the layouts-file shape."""
    m = CODE.search(text or "")
    if not m:
        raise ValueError("no RWCF1 share code")
    b64 = m.group(0)[len(PREFIX):]
    b64 += "=" * (-len(b64) % 4)
    d = zlib.decompressobj(-15)
    raw = d.decompress(base64.urlsafe_b64decode(b64), 64 * 1024)
    if d.unconsumed_tail:
        raise ValueError("the code is too big")
    p = json.loads(raw.decode("utf-8"))
    if p.get("v") not in (1, 2):
        raise ValueError("a share code from a newer version of the mod")
    out = {"name": p.get("n") or "Shared layout",
           "slots": [{"unit": s[0], "x": s[1], "y": s[2], "rot": bool(s[3]), "px": s[4], "w": s[5], "h": s[6]}
                     for s in p.get("s", [])]}
    if p.get("a"):
        out["area"] = p["a"]
    if p["v"] == 2:
        out["frame"] = "view"
    return out


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "decode":
        print(json.dumps(decode(sys.argv[2]), indent=2, ensure_ascii=False))
    elif len(sys.argv) == 3 and sys.argv[1] == "encode":
        with open(sys.argv[2], encoding="utf-8") as f:
            print(encode(json.load(f)))
    else:
        print(__doc__)
