// RWC Formations for BepInEx 6 (IL2CPP): the entry point. Fills in Host,
// installs the hooks, and runs the panel from an injected MonoBehaviour.
// Layouts: BepInEx/rwc-formations.json (where 0.1 to 0.6 kept them).
#if !MELON
using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace RwcFormations
{
    [BepInPlugin("com.rhinowaspcult.formations", "RWC Formations", Host.Version)]
    public class BepInExPlugin : BasePlugin
    {
        public override void Load()
        {
            Host.Init("BepInEx", Paths.BepInExRootPath, Paths.GameRootPath, m => Log.LogInfo(m), m => Log.LogError(m));
            bool run = Host.CheckBuild();
            ClassInjector.RegisterTypeInIl2Cpp<FormationBehaviour>();
            var go = new GameObject("RwcFormations");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<FormationBehaviour>();
            if (!run) { Host.Info($"RWC Formations {Host.Version} (BepInEx) loaded, OFF on this game build."); return; }
            var installed = Hooks.Install(new Harmony("com.rhinowaspcult.formations"));
            Host.Info($"RWC Formations {Host.Version} (BepInEx) loaded. F6 = panel; Ctrl+Alt+1..9 = place layout (with Shift = mirrored). Hooks: {string.Join(", ", installed)}");
        }
    }

    // BepInEx needs a MonoBehaviour for per-frame calls; it just runs the panel.
    public class FormationBehaviour : MonoBehaviour
    {
        public FormationBehaviour(IntPtr ptr) : base(ptr) { }
        readonly Panel _panel = new Panel();
        void Update() => _panel.Update();
        void OnGUI() => _panel.OnGUI();
    }
}
#endif
