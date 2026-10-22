// RWC Formations: the panel's text in English and Simplified Chinese (0.9.0).
// Game-free, so formation-test/ checks it. Which language: the game's own setting
// (a language code starting "zh" = Chinese, see Panel.RefreshLanguage), or a file
// named rwc-formations-language in the data folder containing "en" or "zh".
// Layout names stay as the player typed them. Log lines stay English (bug reports).
// The Chinese is a first draft: have a native speaker review it before release.
using System.Collections.Generic;

namespace RwcFormations
{
    public static class Tx
    {
        public static bool Zh;
        static string P(string en, string zh) => Zh ? zh : en;
        static string Q(string name) => Zh ? $"“{name}”" : $"\"{name}\"";

        // Separators: Chinese uses full-width punctuation.
        public static string Comma => P(", ", "，");
        public static string Stop => P(".", "。");
        public static string Colon => P(": ", "：");

        // Header and status
        public static string Paste => P("Paste", "粘贴");
        public static string MovePanel => P("Move", "移动");
        public static string Hide => P("Hide", "隐藏");
        public static string Deploying => P("Deploying: buttons on. F6 hides.", "部署中：按钮可用。F6 隐藏面板。");
        public static string OffBecause(string why) => P("Off: ", "不可用：") + Reason(why);
        public static string OffNoticeHint => P(" (F6 hides this.)", "（F6 隐藏）");

        // Main buttons
        public static string SaveCurrent => P("Save current", "保存当前");
        public static string MirrorMine => P("Mirror my units", "镜像我的单位");
        public static string Revert(int steps) => steps > 1 ? P($"Revert ({steps})", $"撤回 ({steps})") : P("Revert", "撤回");

        // Name box
        public static string NameLabel => P("Name:", "名称：");
        public static string TypeAName(string current) => P($"type a name ({current})", $"输入名称（{current}）");
        public static string Ok => P("OK", "确定");
        public static string Cancel => P("Cancel", "取消");

        // List
        public static string AllLayouts(int n) => P($"All {n} layouts", $"全部 {n} 个阵型");
        public static string FitYourBoard(int n, int hidden) =>
            P($"{n} fit your board", $"{n} 个适合你的单位") + (hidden > 0 ? P($", {hidden} hidden", $"，隐藏 {hidden} 个") : "");
        public static string FittingOnly => P("Fitting only", "仅显示适用");
        public static string ShowAll => P("Show all", "全部显示");
        public static string NoLayouts => P("No layouts saved yet.", "还没有保存的阵型。");
        public static string NoneFit => P("None of your layouts fit the units on your board.", "没有阵型适合你场上的单位。");
        public static string Fillable(int have, int total) => P($"{have} of {total}", $"{have}/{total}");
        public static string Place => P("Place", "放置");
        public static string Mirrored => P("Mirrored", "镜像");
        public static string NameButton => P("Name", "命名");
        public static string Copy => P("Copy", "复制");
        public static string Sure => P("sure?", "确定?");
        public static string Page(int page, int pages) => P($"page {page} of {pages}", $"第 {page}/{pages} 页");

        // Preview
        public static string PreviewTitle(string name, bool mirror) =>
            Zh ? $"{name}{(mirror ? "（镜像）" : "")}：上方为前线" : $"{name}{(mirror ? " (mirrored)" : "")}: front is at the top";

        // Messages
        public static string CantSave(string why) => P("Can't save: ", "无法保存：") + Reason(why);
        public static string NothingToSave => P("No units on the board to save.", "场上没有可保存的单位。");
        public static string Saved(string name, int n) =>
            P($"Saved {Q(name)} ({n} units). Type a name and press Enter, or OK to keep it.", $"已保存{Q(name)}（{n} 个单位）。输入名称后按回车，或点确定保留原名。");
        public static string Named(string name, string old) => P($"Named {Q(name)} (was {Q(old)}).", $"已命名为{Q(name)}（原名{Q(old)}）。");
        public static string CantMirror(string why) => P("Can't mirror: ", "无法镜像：") + Reason(why);
        public static string MirroredYours => P("Mirrored your units", "已镜像你的单位");
        public static string CantPlace(string why) => P("Can't place: ", "无法放置：") + Reason(why);
        public static string Placing(string name, bool mirror) => Q(name) + (mirror ? P(" mirrored", "（镜像）") : "");
        public static string Moved(int n) => P($"{n} moved", $"移动 {n} 个");
        public static string AlreadyThere(int n) => P($"{n} already in place", $"{n} 个已在位置上");
        public static string Refused(int n, string detail) => P($"{n} refused by the game ({detail})", $"游戏拒绝 {n} 个（{detail}）");
        public static string Missing(int n) => P($"{n} saved spots with no unit for them", $"{n} 个位置没有对应的单位");
        public static string LeftAlone(int n) => P($"{n} units not in the layout left alone", $"{n} 个阵型外的单位未移动");
        public static string Result(string what, IEnumerable<string> parts) => what + Colon + string.Join(Comma, parts) + Stop;
        public static string Failed(string why) => P("Failed: ", "失败：") + why;
        public static string NoLayoutNumber(int n) => P($"No layout {n} in the list.", $"列表中没有第 {n} 个阵型。");
        public static string Copied(string name, int length) =>
            P($"Copied {Q(name)} as a share code ({length} characters). Paste it in Discord; friends add it with Paste.",
              $"已将{Q(name)}复制为分享码（{length} 个字符）。发给朋友后，对方点“粘贴”即可添加。");
        public static string CopyFailed(string why) => P("Copy failed: ", "复制失败：") + why;
        public static string ClipboardUnreadable(string why) => P("Couldn't read the clipboard: ", "无法读取剪贴板：") + why;
        public static string PasteFailed(string error) => P("Paste: ", "粘贴：") + Reason(error) + Stop;
        public static string AlreadyHave(string name) => P($"You already have this layout as {Q(name)}.", $"你已经有这个阵型了：{Q(name)}。");
        public static string Added(string name, int n, bool hidden) =>
            P($"Added {Q(name)} ({n} units)", $"已添加{Q(name)}（{n} 个单位）") +
            (hidden ? P(". It's hidden: you don't have its units now (Show all lists it).", "。你现在没有它的单位，所以它被隐藏了（点“全部显示”可查看）。") : Stop);
        public static string CantRevert(string why) => P("Can't revert: ", "无法撤回：") + Reason(why);
        public static string MovedBack(int n) => P($"{n} moved back", $"{n} 个已移回");
        public static string AlreadyBack(int n) => P($"{n} already back", $"{n} 个已在原位");
        public static string Gone(int n) => P($"{n} no longer on the board", $"{n} 个已不在场上");
        public static string Reverted(IEnumerable<string> parts) => P("Reverted", "已撤回") + Colon + string.Join(Comma, parts) + Stop;
        public static string RevertFailed(string why) => P("Revert failed: ", "撤回失败：") + why;
        public static string SaveFileFailed(string why) => P("Saving the layouts file failed: ", "保存阵型文件失败：") + why;

        // Version safety notices (Builds.Notice gives the English).
        public static string Notice(Builds.Verdict v, string version, string modVersion) => !Zh ? Builds.Notice(v, version, modVersion) : v switch
        {
            Builds.Verdict.Known => null,
            Builds.Verdict.UnknownVersion => $"RWC Formations {modVersion} 尚未在游戏版本 {version} 上测试，更新前已关闭。",
            Builds.Verdict.ChangedCode => $"游戏已更新（版本 {version}，代码有变）：RWC Formations {modVersion} 在测试前已关闭。",
            _ => $"RWC Formations {modVersion} 无法读取游戏版本，已关闭。",
        };

        // English reasons from GameBoard.Read and Share.TryDecode, in Chinese.
        static readonly Dictionary<string, string> Reasons = new Dictionary<string, string>
        {
            ["not in a game"] = "不在对局中",
            ["not a game you're playing (replay or spectating)"] = "不是你正在进行的对局（回放或观战）",
            ["wait for the deployment phase"] = "请等待部署阶段",
            ["no local player"] = "找不到本地玩家",
            ["deployment is over for this round"] = "本回合部署已结束",
            ["map not ready"] = "地图未就绪",
            ["the clipboard is empty"] = "剪贴板是空的",
            ["no RWCF1 share code on the clipboard"] = "剪贴板里没有 RWCF1 分享码",
            ["a share code from a newer version of the mod (update yours)"] = "这是新版模组的分享码（请更新模组）",
            ["the code is too big"] = "分享码太大",
            ["the code has no units"] = "分享码里没有单位",
            ["the code has too many units"] = "分享码里的单位太多",
            ["a unit in the code is malformed"] = "分享码里有单位格式错误",
            ["a unit in the code is out of range"] = "分享码里有单位超出范围",
            ["the share code is damaged (copied only part of it?)"] = "分享码已损坏（可能只复制了一部分？）",
        };

        // Unknown text (e.g. "error: ...") stays as it is.
        public static string Reason(string en) => en != null && Zh && Reasons.TryGetValue(en, out var zh) ? zh : en;

        public static IEnumerable<string> ReasonKeys => Reasons.Keys;

        // "en" / "zh" from the override file, else the game's language code.
        public static bool ChineseFor(string overrideText, string gameLanguage)
        {
            var o = (overrideText ?? "").Trim().ToLowerInvariant();
            if (o.StartsWith("zh")) return true;
            if (o.StartsWith("en")) return false;
            return (gameLanguage ?? "").Trim().ToLowerInvariant().StartsWith("zh");
        }
    }
}
