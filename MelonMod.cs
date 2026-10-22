// RWC Formations for MelonLoader (0.7): the entry point. Fills in Host, installs
// the hooks itself (one by one, like BepInEx: HarmonyDontPatchAll stops
// MelonLoader patching them all at once), and runs the panel from MelonMod's own
// per-frame calls. Layouts: UserData/rwc-formations.json.
#if MELON
using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(RwcFormations.FormationsMod), "RWC Formations", RwcFormations.Host.Version, "Roxas (Rhino Wasp Cult)")]
[assembly: MelonGame("GameRiver", "Mechabellum")]
[assembly: HarmonyDontPatchAll]

namespace RwcFormations
{
    public class FormationsMod : MelonMod
    {
        Panel _panel;

        public override void OnInitializeMelon()
        {
            Host.Init("MelonLoader", MelonEnvironment.UserDataDirectory, MelonEnvironment.GameRootDirectory, m => LoggerInstance.Msg(m), m => LoggerInstance.Error(m));
            _panel = new Panel();
            if (!Host.CheckBuild()) { Host.Info($"RWC Formations {Host.Version} (MelonLoader) loaded, OFF on this game build."); return; }
            var installed = Hooks.Install(HarmonyInstance);
            Host.Info($"RWC Formations {Host.Version} (MelonLoader) loaded. F6 = panel; Ctrl+Alt+1..9 = place layout (with Shift = mirrored). Hooks: {string.Join(", ", installed)}");
        }

        public override void OnUpdate() => _panel?.Update();
        public override void OnGUI() => _panel?.OnGUI();
    }
}
#endif
