// RWC Formations: font check for Chinese text (chunk 3, 0.8.1). Off unless a file
// named rwc-formations-fonttest is in the mod's data folder. When on, the panel's
// status line shows a line of Chinese (unit names from the game's own text), and
// on the first panel draw the log lists the game's language, the panel's font, and
// every font the game has loaded, each with whether it contains Chinese
// characters. Font.CreateDynamicFontFromOSFont is stripped from this build, so if
// the panel's built-in font can't draw Chinese, the fallback is one of the game's
// own fonts.
using System;
using System.IO;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace RwcFormations
{
    internal static class FontCheck
    {
        public const string SwitchFile = "rwc-formations-fonttest";
        // 字体测试 = "font test"; 爬虫 Crawler, 火獾 Fire Badger, 堡垒 Fortress (the game's names).
        public const string Sample = "字体测试: 爬虫 火獾 堡垒 / 保存 放置 镜像 撤销 粘贴";
        static readonly char[] Probe = { '爬', '獾', '保', '镜' };

        static bool? _on;
        static bool _logged;

        public static bool On
        {
            get
            {
                if (_on == null) { try { _on = File.Exists(Path.Combine(Host.DataDir, SwitchFile)); } catch { _on = false; } }
                return _on.Value;
            }
        }

        static string Covers(Font f)
        {
            int n = 0;
            foreach (var c in Probe) { try { if (f.HasCharacter(c)) n++; } catch { } }
            return $"{n}/{Probe.Length} test characters";
        }

        // Inside OnGUI only (GUI.skin exists there).
        public static void LogOnce()
        {
            if (!On || _logged) return;
            _logged = true;
            try
            {
                string lang = "?";
                try { lang = GRC.GRLocalizationManager.Instance?.GetCurrentLanguage()?.Name ?? "?"; } catch (Exception e) { lang = "? (" + e.Message + ")"; }
                Host.Info($"[font check] game language: {lang}; Windows language: {Application.systemLanguage}");
                var skinFont = GUI.skin?.font;
                Host.Info(skinFont == null ? "[font check] panel font: none (Unity's built-in default)" : $"[font check] panel font: {skinFont.name}, dynamic {skinFont.dynamic}, has {Covers(skinFont)}");
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Font>());
                Host.Info($"[font check] {all.Length} fonts loaded:");
                for (int i = 0; i < all.Length && i < 60; i++)
                {
                    var f = all[i]?.TryCast<Font>();
                    if (f == null) continue;
                    Host.Info($"[font check]   {f.name}: dynamic {f.dynamic}, has {Covers(f)}");
                }
            }
            catch (Exception e) { Host.Error($"[font check] failed: {e}"); }
        }
    }
}
