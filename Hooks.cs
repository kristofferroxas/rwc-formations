// RWC Formations: the game hooks, installed the same way under both loaders.
// Each is patched on its own so one failing leaves the others.
//  - Key blocks: while a name is being typed, the game's shortcut keys and camera
//    keys are held back.
//  - Click blocks (ClickBlock.cs, 0.3.0): the game's "mouse is over UI" check also
//    says yes over the panel.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RwcFormations
{
    internal static class Hooks
    {
        static readonly Type[] All =
        {
            typeof(ShortcutKeyBlock), typeof(OrbitKeyBlock), typeof(ZoomKeyBlock),
            typeof(InputBlockedHook), typeof(InputCheckBlockedHook), typeof(UIManagerBlockedHook),
        };

        // The names of the hooks that went in.
        // (HarmonyLib.Harmony in full: MelonLoader's references also have a "Harmony" namespace.)
        public static List<string> Install(HarmonyLib.Harmony harmony)
        {
            var installed = new List<string>();
            foreach (var t in All)
            {
                try { harmony.PatchAll(t); installed.Add(t.Name); }
                catch (Exception e) { Host.Error($"Hook {t.Name} failed: {e.Message}"); }
            }
            return installed;
        }
    }

    // On while typing, and for the frame after Enter (so the Enter that confirms a
    // name doesn't also reach the game).
    internal static class KeyBlock
    {
        public static bool Typing;
        public static int UntilFrame = -1;
        public static bool Active => Typing || Time.frameCount <= UntilFrame;
    }

    // The game's shortcut keys all go through GRInputManager.Update.
    [HarmonyPatch(typeof(GR.GRInputManager), nameof(GR.GRInputManager.Update))]
    internal static class ShortcutKeyBlock
    {
        private static bool Prefix() => !KeyBlock.Active;
    }

    [HarmonyPatch(typeof(OrbitCam), nameof(OrbitCam.tryOrbitByKeyCode))]
    internal static class OrbitKeyBlock
    {
        private static bool Prefix() => !KeyBlock.Active;
    }

    [HarmonyPatch(typeof(ZoomCam), nameof(ZoomCam.tryZone))]
    internal static class ZoomKeyBlock
    {
        private static bool Prefix() => !KeyBlock.Active;
    }
}
