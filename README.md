# RWC Formations

A BepInEx 6 (IL2CPP) plugin for Mechabellum: save your unit positioning, then put it
back with a click, as saved or mirrored left to right. Separate from the sim plugin
(`sim-plugin/`, RwcSimProbe); **not part of the website, never deployed.**

> **Players:** this repository is private, please don't share it. Download the latest
> `RWC-Formations-x.y.z.zip` from **Releases** (right-hand side of this page), unzip it,
> close Mechabellum and double-click `Install or update.bat`. `HOW-TO-INSTALL.txt` in the
> zip has the details and the manual steps. For a new version, do the same again.

## Use

In a game, during deployment, a small panel sits in the top-right corner:

- **Save current**: saves every unit you have on the board, then opens the name box:
  type a name and press Enter (or OK). Leave it blank to keep "Layout N".
- **Name**: renames a layout the same way. While the name box is open, the game's own
  shortcut keys and camera keys are paused, so typing doesn't trigger them.
- **The list follows your board.** It shows only the layouts whose unit types are all
  on your board right now (extra units on the board are fine). "(5 of 7)" means you have
  units for 5 of its 7 spots. **Show all** lists the rest too. Outside deployment it lists
  everything.
- **Place**: moves your units onto a saved layout.
- **Mirrored**: the same layout with left and right swapped.
- **Mirror my units**: swaps your current board left to right.
- **Preview** (0.4.0): hover a layout's row and a mini-map appears beside the panel: your
  side of the board with the front (enemy side) at the top, each unit as a coloured box,
  the centre line Mirrored swaps around, and a legend with unit names and counts. Hover
  **Mirrored** to see the mirrored version.
- **Copy** (0.5.0): puts the layout on your clipboard as a share code, one line of text
  starting `RWCF1.` (about 150 to 400 characters). Paste it in Discord.
- **Paste** (top of the panel): adds the share code on your clipboard as a new layout. The
  code can sit inside a longer message. A layout you already have (same units on the same
  spots) isn't added twice; a taken name gets " (2)".
- **Revert** (0.3.0): puts the units the last Place or Mirror moved back where they were.
  It works step by step ("Revert (3)" = three to undo) until deployment ends.
- **x** (click twice): deletes a layout. **Move** cycles the panel's corner, **Hide** or
  **F6** hides it. Six layouts per page, **<** / **>** to page.
- Hotkeys: **Ctrl+Alt+1..9** places listed layout 1..9 (the numbers shown in the list),
  **Ctrl+Alt+Shift+1..9** mirrored.

The buttons are only on during your own deployment phase, never in a replay, a replay
simulation or while spectating.

## How placing works

- **Same move as a drag.** The mod builds the game's own move action (`PAD_MoveUnit`,
  one action for the whole layout, like a group drag). It checks it with the game's
  `TerritoryManager.CanMoveUnits`, then sends it with `PlayerController.TryPerformAction`.
  The game checks every spot, so nothing goes where you couldn't drag it. The moves are
  recorded in the replay like any other move, and the fight engine is never touched.
- **Units from earlier rounds stay put.** The game locks them (`CardElement.IsOldUnit`),
  except where it allows a redeploy. A locked unit already on its saved spot counts as
  filling that spot.
- **Matching.** Each saved spot is matched to one of your units of the same type, nearest
  first. Unit level is ignored. Units with no spot stay where they are, and spots with
  no unit to fill them are skipped. The status line says what happened.
- **Fallback.** If the game refuses the move as a whole, the mod moves units one at a
  time over a few passes, skips the spots it still refuses, and gives the reason codes
  (`InvalidPosition`, `IsMoved`, ...).
- **Coordinates.** Layouts are stored in the game's standard space
  (`MapSystem.ConvertToStandardWorldPosition`), so a layout saved on one side of the map
  fits the other. x is stored relative to your territory's centre, so a 2v2 layout saved
  as one partner fits the other. Mirroring swaps around that centre and keeps each unit
  on its grid squares (odd footprints on x = 5 mod 10, even ones on multiples of 10).

Layouts are kept in `BepInEx/rwc-formations.json` (if the file can't be read, it's
copied aside as `.unreadable-<time>` and the list starts empty).

## Share codes

`Share.cs` (and `share_code.py`, the same format in Python, for the analyzer later):
`"RWCF1." + base64url(raw deflate(UTF-8 JSON))`, JSON `{"v":1,"n":name,"a":[xMin,yMin,xMax,yMax],
"s":[[unit,x,y,rot,px,w,h],...]}` in the layouts file's coordinates. Codes come from other
people, so decoding caps the input (20,000 characters) and the unpacked size (64 KB),
range-checks every number (300 units, coordinates within 5,000, footprints within 500) and
cleans the name. C# and Python read each other's codes (checked in `formation-test/`).

## Unit names and sizes

`UnitInfo.cs` is GENERATED by `python formation-plugin/make_unit_info.py` from the
project's lookup (`files/mechabellum_lookup.py`: `UNIT_LOOKUP` names, title-cased like the
site, and the dump's footprints). Never hand-edit it; re-run the script after the lookup
changes. Layouts saved before 0.3.0 have no footprint of their own, so the preview uses
these (turned when the placement is rotated). The preview geometry is in `Preview.cs`
(game-free, checked by `formation-test/`).

## Languages (0.9.0)

The panel is in English or Simplified Chinese (`Text.cs`, every string in both). It
follows the game's own language (a code starting `zh` = Chinese, read from the game's
localization manager); a file named `rwc-formations-language` in the data folder with
`en` or `zh` overrides it. Unit names in the preview come from the game's own data
(`name_zh` in the dump's `units.json`, all 101 units; `make_unit_info.py`). Layout names
stay as typed; log lines stay English for bug reports. The panel's built-in font draws
Chinese (font check, 0.8.1: `FontCheck.cs`, on only with a `rwc-formations-fonttest`
file). The Chinese was reviewed by a 201 player, llxmod-院长大人 (2026-10-22): clear overall, but
镜像 (Mirrored) and 复制 (Copy) side by side read as the same thing, so since 0.9.1
Mirrored = 翻转 (flip) and the Copy button = 分享 (share).

## Version safety (0.8.0)

The mod hooks the game's own code, so a game patch can break it. At load, before any
hook goes in, `Host.CheckBuild` compares the game build with `Builds.Known`
(`Builds.cs`): the game's version string (Unity's `Application.version`, falling back to
the one in `Mechabellum_Data/globalgamemanagers`) and the size of `GameAssembly.dll`
(a hotfix that keeps the version string still changes that file).

- **Known build:** runs normally. The log says e.g. `Game build 2.0.0.4.2331,
  GameAssembly.dll 71137792 bytes: Known.`
- **Any other build:** no hooks, no board reading, no moves. The panel shows only a
  one-line notice (F6 or Hide hides it) and the log says why and how to override.
- **Override for testing:** a file named `rwc-formations-allow-any-build` in the mod's
  data folder (`BepInEx/` or `UserData/`) runs it anyway.

**After a game patch:** create the override file, test one AI game (Save, Place,
Mirrored, Revert, preview, Copy/Paste), read the new version and size from the log, add
them to `Builds.Known`, update the real-data check in `formation-test/`, release, and
delete the override file.

## Releasing to friends (private GitHub repo)

`formation-plugin/` is its own git repo: https://github.com/kristofferroxas/rwc-formations
(PRIVATE; friends are added as collaborators, Settings > Collaborators). For a new version:
bump `Plugin.Version`, build, run `formation-test/`, rebuild `friend-package/` (copy the DLL
in, update the version in HOW-TO-INSTALL.txt, zip DLL + txt + bat + ps1 as
`RWC-Formations-x.y.z.zip`), commit and push, then
`gh release create vX.Y.Z friend-package/RWC-Formations-x.y.z.zip` with the zip's SHA-256
in the notes. Built files (bin/, obj/, the zip, the DLL copy) stay out of git.

## Code layout: one core, two loaders (0.7.0)

- **Core, no loader code:** `Panel.cs` (the panel), `GameBoard.cs` (reading the board,
  sending moves), `ClickBlock.cs`, `Hooks.cs` (key and click blocks), plus the game-free
  `Layouts.cs`, `Preview.cs`, `Share.cs`, `UnitInfo.cs`.
- **`Host.cs`:** the only place that knows the loader (logging, data folder, version).
- **`GameTypes.cs`:** the game's type names per loader. BepInEx keeps the game's
  namespaces; MelonLoader 0.7.3 prefixes them with `Il2Cpp` (`Il2CppGameRiver`,
  `Il2CppGameRiver.Client`, `Il2Cpp` for the global namespace; checked on its generated
  files, 2026-10-22). Its generated files are named `Il2CppGRCore.dll` etc.
- **Switching loaders for testing:** `loader-switch/use-melonloader.ps1` and
  `use-bepinex.ps1` (game closed) only rename `winhttp.dll` (BepInEx) and `version.dll`
  (MelonLoader) to `.off`; nothing is deleted. The first switch unpacks MelonLoader from
  `-MelonZip`, checked against its SHA-256.
- **Entry points:** `BepInExPlugin.cs` (built by `RwcFormations.csproj`) and
  `MelonMod.cs` (built by `melon/RwcFormations.Melon.csproj`, with `MELON` defined).
- **Data folder:** BepInEx keeps `BepInEx/rwc-formations.json` (as before); MelonLoader
  uses `UserData/rwc-formations.json`. Same format, same share codes.
- **The MelonLoader build needs MelonLoader's files** (`MelonLoader/net6/*.dll` and the
  `MelonLoader/Il2CppAssemblies/` it generates on its first game start). Point
  `MelonDir` at a game folder that has them: `dotnet build -c Release -p:MelonDir=...`.

## Build, test, install

```
dotnet build -c Release                  (bin/Release/net6.0/RwcFormations.dll)
cd formation-test && dotnet run -c Release   (offline checks of the planner, no game)
cd melon && dotnet build -c Release            (MelonLoader build; needs MelonLoader's files)
```

Install: with the game closed, copy `RwcFormations.dll` into `BepInEx/plugins/`.
Uninstall: delete it. Everything goes to `BepInEx/LogOutput.log` too: each Save writes a line
per unit (standard coordinates, movable or not).

## Why the panel is plain

This game build has parts of Unity's IMGUI stripped out: automatic layout (`GUILayout`,
its `GUILayoutGroup.GetNext`), scroll views and text fields all throw "Method unstripping
failed" (0.1.0's panel never drew). 0.1.1 draws only with `GUI.Box` / `GUI.Label` /
`GUI.Button` at fixed positions, which are real methods in the game. If drawing still
fails, the panel switches itself off with one log line and the hotkeys keep working.
The name box (0.2.0) is a `GUI.Box` showing the keys typed so far (`Input.inputString`),
since the real text field is stripped. While it's open, Harmony prefixes skip
`GRInputManager.Update` (all the game's shortcut keys) and the camera's
`OrbitCameraController.tryOrbitByKeyCode` / `ZoomCameraController.tryZone`, and for one
frame after Enter.

## Clicks on the panel (0.3.0)

IMGUI is invisible to the game's input, so before 0.3.0 a click on a panel button also
clicked the map underneath. `ClickBlock.cs` stops that in two ways, each enough alone: an
invisible UGUI rectangle under the panel (top-most overlay canvas), which the game's own
"mouse over UI" raycast sees, and Harmony postfixes that make that check
(`InputController.IsBlockedByUI` / `CheckIsBlockedByUI`, `GRUIManager.CheckIsBlockedByUI`)
answer "blocked" while the mouse is over the panel. New saves also store each unit's
footprint (`w`, `h`) and the territory (`area`) for the coming preview; older layouts load
unchanged.

## Status

0.1.0 (2026-10-21): installed; first AI game: the panel never drew (stripped IMGUI, above).
0.1.1 (2026-10-21): panel redrawn with fixed-position GUI calls only. **Works in an AI game**
(Kris): Save, Place, Mirrored and Mirror my units on 7 units in round 1, each as one move
action the game accepted. Not seen yet: rounds 2+ (locked units), Undo, 2v2, the other seat,
click-through. The trace hook never fired, for drags or the mod, so it does not show the
path; and that game left no replay to check how the moves were recorded.
0.2.0 (2026-10-21, Kris asked): the list shows only layouts that fit your board (Show all
for the rest); naming on Save and a Name button (game keys paused while typing); the
unused trace hook removed. **Tested by Kris: works** (naming, and layouts hide when the
starting units differ).
0.3.0 (2026-10-22): clicks on the panel no longer reach the map; Revert; footprints +
territory saved for the preview. 31/31 offline checks, installed. **Not tried in the game yet.**
**Tested by Kris: works** (Place, Revert, Mirror, no clicks through).
0.4.0 (2026-10-22): mini-map preview on hover (UnitInfo.cs generated from the lookup).
43/43 offline checks. Kris 2026-10-22: works, but too tall and left/right swapped.
0.4.1 (2026-10-22): preview cropped to your half (the game's territory bound is the whole
map, both halves), and +x drawn on the left as seen from your seat. 46/46 offline checks, installed.
0.5.0 (2026-10-22, Kris asked): share codes, Copy / Paste. Kris: works, but the preview is
still mirrored (it was mirrored for one seat in 0.4.0 and for the other in 0.5.0).
0.6.0 (2026-10-22): layouts in the VIEW frame (see GameBoard.cs): as seen from your seat,
turned the way the camera turns (view = world for a -y seat, -world for a +y seat), x
relative to your deployment area (main region). Fixes the preview and placing a layout
from the other seat. Old layouts (no "frame") convert through the game as before. Share
codes v2 (view) / v1 (old). 64/64 offline checks. **Kris: the preview looks right** (old
layouts). Not seen yet: a new save in the view frame, and placing it from the other seat.
Next chunk: openings saved from replays.
Still to check in a game (AI or custom, not ranked):
1. 0.2.0: the list shows only layouts that fit your board; Show all; Save opens the name
   box; typing a name doesn't trigger game shortcuts or move the camera; Enter confirms
   without opening anything in the game; Name renames.
2. Round 2+: are earlier-round units marked `movable False` in the Save log, and left
   alone?
3. Undo after a Place: does the whole layout come back in one step?
4. Click a panel button while a unit is picked up for moving: does the click also land
   on the map? The panel is drawn with Unity's IMGUI, which doesn't block the game's own
   input.
0.7.0 (2026-10-22): one core, two loaders. BepInEx build installed. MelonLoader 0.7.3 build:
tested by Kris in an AI game (loads with all six hooks; Place, Mirrored, Revert and the
preview work). Save and Paste weren't in that log: check them on the next MelonLoader run.
0.8.0 (2026-10-22): version safety (`Builds.cs`). 75/75 offline checks, including this PC's
game counting as a known build. Installed for both loaders. **Off mode not seen in a game yet**
(it only shows on an unknown build).
0.8.1: font check (Kris: Chinese draws). 0.9.0 (2026-10-22): English + Chinese panel, Chinese unit
names. 126/126 offline checks, installed. **Kris: the Chinese panel works.** Translation reviewed
by a 201 player. 0.9.1: their wording (翻转 / 分享); 128/128 offline checks, installed, zip
rebuilt.

Online play: the devs have said they're fine with modding, and the mod only sends moves
you could make by hand. Still, try it outside ranked first.
