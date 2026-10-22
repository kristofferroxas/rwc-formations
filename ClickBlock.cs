// RWC Formations: keep clicks on the panel away from the map (0.3.0; before,
// clicking a panel button also clicked whatever was under it).
//
// The panel is IMGUI, which the game's input never sees, so the game treats the
// mouse as over the battlefield. Two layers, each enough on its own:
//  1. An invisible UGUI rectangle under the panel (top-most overlay canvas). The
//     game's own "is the mouse over UI" check raycasts the UI, sees it, and
//     ignores the click, as it does for its own menus.
//  2. Harmony postfixes on that check (InputController.IsBlockedByUI /
//     CheckIsBlockedByUI, GRUIManager.CheckIsBlockedByUI): over the panel, the
//     answer is "blocked".
using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace RwcFormations
{
    internal static class ClickBlock
    {
        // The panel's rectangle in GUI coordinates (origin top-left), while drawn.
        public static Rect PanelRect;
        public static bool PanelShown;

        static GameObject _canvasGo;
        static RectTransform _blocker;

        public static bool MouseOverPanel
        {
            get
            {
                if (!PanelShown) return false;
                var m = Input.mousePosition;
                return PanelRect.Contains(new Vector2(m.x, Screen.height - m.y));
            }
        }

        public static void Create()
        {
            _canvasGo = new GameObject("RwcFormationsClickBlock");
            UnityEngine.Object.DontDestroyOnLoad(_canvasGo);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            _canvasGo.AddComponent<GraphicRaycaster>();

            var child = new GameObject("Blocker");
            child.transform.SetParent(_canvasGo.transform, false);
            var img = child.AddComponent<Image>();   // adds the RectTransform
            img.color = new Color(0f, 0f, 0f, 0f);   // invisible, still hit by raycasts
            img.raycastTarget = true;
            _blocker = child.GetComponent<RectTransform>();
            _blocker.anchorMin = Vector2.zero;
            _blocker.anchorMax = Vector2.zero;
            _blocker.pivot = Vector2.zero;
            child.SetActive(false);
        }

        // Every frame: put the blocker under the panel, or switch it off.
        public static void Sync()
        {
            if (_blocker == null) return;
            var go = _blocker.gameObject;
            if (!PanelShown) { if (go.activeSelf) go.SetActive(false); return; }
            // UGUI's origin is bottom-left; an overlay canvas without a scaler is in pixels.
            _blocker.anchoredPosition = new Vector2(PanelRect.x, Screen.height - PanelRect.y - PanelRect.height);
            _blocker.sizeDelta = new Vector2(PanelRect.width, PanelRect.height);
            if (!go.activeSelf) go.SetActive(true);
        }
    }

    [HarmonyPatch(typeof(GRC.InputController), nameof(GRC.InputController.IsBlockedByUI))]
    internal static class InputBlockedHook
    {
        private static void Postfix(ref bool __result) { if (!__result && ClickBlock.MouseOverPanel) __result = true; }
    }

    [HarmonyPatch(typeof(GRC.InputController), nameof(GRC.InputController.CheckIsBlockedByUI))]
    internal static class InputCheckBlockedHook
    {
        private static void Postfix(ref bool __result) { if (!__result && ClickBlock.MouseOverPanel) __result = true; }
    }

    [HarmonyPatch(typeof(GRC.GRUIManager), nameof(GRC.GRUIManager.CheckIsBlockedByUI))]
    internal static class UIManagerBlockedHook
    {
        private static void Postfix(ref bool __result) { if (!__result && ClickBlock.MouseOverPanel) __result = true; }
    }
}
