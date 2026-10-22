// RWC Formations: the one place that knows which mod loader is running (0.7.0).
// BepInExPlugin.cs and MelonMod.cs fill it in at load; everything else logs and
// finds its data folder through here.
using System;

namespace RwcFormations
{
    internal static class Host
    {
        public const string Version = "0.7.0";
        public static string Loader = "";      // "BepInEx" or "MelonLoader"
        public static string DataDir = ".";    // where rwc-formations.json lives
        static Action<string> _info = _ => { }, _error = _ => { };

        public static void Init(string loader, string dataDir, Action<string> info, Action<string> error)
        {
            Loader = loader;
            DataDir = dataDir;
            _info = info;
            _error = error;
        }

        public static void Info(string message) => _info(message);
        public static void Error(string message) => _error(message);
    }
}
