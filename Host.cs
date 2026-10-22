// RWC Formations: the one place that knows which mod loader is running (0.7.0).
// BepInExPlugin.cs and MelonMod.cs fill it in at load; everything else logs and
// finds its data folder through here.
using System;
using System.IO;
using UnityEngine;

namespace RwcFormations
{
    internal static class Host
    {
        public const string Version = "0.8.0";
        public static string Loader = "";      // "BepInEx" or "MelonLoader"
        public static string DataDir = ".";    // where rwc-formations.json lives
        public static string GameDir = ".";    // the game folder (GameAssembly.dll)
        // Set by CheckBuild when the game build isn't one the mod was checked on: the
        // panel shows only this notice and no hooks go in (0.8.0, Builds.cs).
        public static string OffNotice;
        static Action<string> _info = _ => { }, _error = _ => { };

        public static void Init(string loader, string dataDir, string gameDir, Action<string> info, Action<string> error)
        {
            Loader = loader;
            DataDir = dataDir;
            GameDir = gameDir;
            _info = info;
            _error = error;
        }

        public static void Info(string message) => _info(message);
        public static void Error(string message) => _error(message);

        // Before any hook goes in: true = run, false = OffNotice is set, stay off.
        public static bool CheckBuild()
        {
            string version = null;
            try { version = Application.version; } catch { }
            if (string.IsNullOrWhiteSpace(version)) version = Builds.VersionFromSettingsFile(GameDir);
            long size = Builds.AssemblySize(GameDir);
            var verdict = Builds.Check(version, size);
            Info($"Game build {version ?? "?"}, GameAssembly.dll {size} bytes: {verdict}.");
            if (verdict == Builds.Verdict.Known) return true;
            if (File.Exists(Path.Combine(DataDir, Builds.OverrideFile)))
            {
                Info($"Running anyway: {Builds.OverrideFile} is in {DataDir} (testing an unchecked build).");
                return true;
            }
            OffNotice = Builds.Notice(verdict, version, Version);
            Error(OffNotice + $" No hooks installed. To run it anyway (testing), create a file named {Builds.OverrideFile} in {DataDir}.");
            return false;
        }
    }
}
