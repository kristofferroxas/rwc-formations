// RWC Formations: version safety (0.8.0). The mod hooks the game's own code, so a
// game patch can break it. At load it checks the game build against the builds it
// has been checked on; on any other build it installs no hooks and the panel only
// shows a short notice (Panel off mode). Game-free, so formation-test/ checks it.
//
// A build = the game's version string (Unity's Application.version, also stored in
// Mechabellum_Data/globalgamemanagers) plus the size of GameAssembly.dll (all the
// game's code: a hotfix that keeps the version string still changes the file).
//
// After a game patch: test the mod on the new build, add a line to Known, release.
// Testers can run it anyway with a file named rwc-formations-allow-any-build in
// the mod's data folder.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RwcFormations
{
    public static class Builds
    {
        public struct Build
        {
            public string Version;     // Application.version
            public long AssemblySize;  // GameAssembly.dll, bytes
            public string Note;
        }

        public static readonly Build[] Known =
        {
            new Build { Version = "2.0.0.4.2331", AssemblySize = 71137792, Note = "Steam build 25924694, checked 2026-10-22 (RWC Formations 0.1 to 0.9.1)" },
            new Build { Version = "2.0.0.5.2340", AssemblySize = 71201280, Note = "Steam build 26153644 (patch of 2026-10-23), checked by Kris in an AI game on 0.9.1 with the override file; mod compiles against its regenerated interop" },
        };

        public const string OverrideFile = "rwc-formations-allow-any-build";

        public enum Verdict { Known, UnknownVersion, ChangedCode, Unreadable }

        // size < 0 = GameAssembly.dll couldn't be read: decide on the version alone.
        public static Verdict Check(string version, long size)
        {
            if (string.IsNullOrWhiteSpace(version)) return Verdict.Unreadable;
            var sameVersion = Known.Where(b => b.Version == version.Trim()).ToList();
            if (sameVersion.Count == 0) return Verdict.UnknownVersion;
            if (size < 0 || sameVersion.Any(b => b.AssemblySize == size)) return Verdict.Known;
            return Verdict.ChangedCode;
        }

        // The panel's one-line notice (null when the build is known).
        public static string Notice(Verdict v, string version, string modVersion) => v switch
        {
            Verdict.Known => null,
            Verdict.UnknownVersion => $"RWC Formations {modVersion} hasn't been checked on game version {version} yet: it's off until an update.",
            Verdict.ChangedCode => $"The game was updated (version {version}, new code): RWC Formations {modVersion} is off until it's checked.",
            _ => $"RWC Formations {modVersion} couldn't read the game version: it's off.",
        };

        // The version string from globalgamemanagers (the first a.b.c.d.e-style
        // string), for when Application.version isn't available yet.
        static readonly Regex VersionPattern = new Regex(@"\d+\.\d+\.\d+\.\d+\.\d+");

        public static string VersionFromSettings(byte[] data)
        {
            if (data == null) return null;
            var m = VersionPattern.Match(Encoding.ASCII.GetString(data));
            return m.Success ? m.Value : null;
        }

        public static string VersionFromSettingsFile(string gameDir)
        {
            try
            {
                var dataDir = Directory.GetDirectories(gameDir, "*_Data").FirstOrDefault();
                var path = dataDir == null ? null : Path.Combine(dataDir, "globalgamemanagers");
                return path != null && File.Exists(path) ? VersionFromSettings(File.ReadAllBytes(path)) : null;
            }
            catch { return null; }
        }

        public static long AssemblySize(string gameDir)
        {
            try
            {
                var p = Path.Combine(gameDir, "GameAssembly.dll");
                return File.Exists(p) ? new FileInfo(p).Length : -1;
            }
            catch { return -1; }
        }
    }
}
