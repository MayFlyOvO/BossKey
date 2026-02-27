using System.Globalization;

namespace HideProcess.App.Localization;

public static class Localizer
{
    private const string DefaultLanguage = "zh-CN";

    private static readonly Dictionary<string, Dictionary<string, string>> Resources = new()
    {
        ["zh-CN"] = new Dictionary<string, string>
        {
            ["Main.WindowTitle"] = "HideProcess 老板键",
            ["Main.Title"] = "HideProcess",
            ["Main.RunningLabel"] = "运行中窗口",
            ["Main.Refresh"] = "刷新",
            ["Main.AddTarget"] = "添加目标",
            ["Main.PickWindow"] = "窗口选择",
            ["Main.SelectedTargets"] = "已选目标",
            ["Main.Remove"] = "移除",
            ["Main.HideNow"] = "立即隐藏",
            ["Main.ShowNow"] = "立即显示",
            ["Main.OpenSettings"] = "设置",
            ["Main.HotkeyHint"] = "隐藏: {0}    显示: {1}",
            ["Main.ToggleHint"] = "切换: {0}",
            ["Main.LogTitle"] = "运行日志",
            ["Main.ClearLogs"] = "清空日志",
            ["Main.CollapseLogs"] = "折叠",
            ["Main.ExpandLogs"] = "展开",
            ["Main.LogCleared"] = "日志已清空。",
            ["Main.PreviousUncleanExit"] = "检测到上次程序未正常退出，请确认目标窗口状态。",
            ["Main.StatusReady"] = "全局热键已启用。",
            ["Main.StatusRefreshed"] = "运行中窗口列表已刷新。",
            ["Main.StatusDuplicate"] = "目标已存在。",
            ["Main.StatusAdded"] = "已添加目标: {0}",
            ["Main.StatusRemoved"] = "已移除目标: {0}",
            ["Main.StatusNoTargets"] = "当前没有可操作的目标。",
            ["Main.StatusHidden"] = "已隐藏 {0} 个窗口。",
            ["Main.StatusNoMatched"] = "未找到可隐藏的匹配窗口。",
            ["Main.StatusShown"] = "已恢复 {0} 个窗口。",
            ["Main.StatusNoHidden"] = "当前没有隐藏窗口。",
            ["Main.StatusSaveFailed"] = "保存失败: {0}",
            ["Main.StatusSettingsApplied"] = "设置已应用。",
            ["Main.StatusPickerStarted"] = "窗口选择模式已启动。",
            ["Main.StatusPickerBusy"] = "窗口选择模式正在运行。",
            ["Main.StatusPickerCanceled"] = "已取消窗口选择。",
            ["Main.StatusGroupCreated"] = "已新建分组。",
            ["Main.StatusGroupRenamed"] = "分组名称已更新。",
            ["Main.StatusMovedToGroup"] = "已将 {0} 移入 {1}。",
            ["Main.StatusGroupDeleted"] = "分组已删除。",
            ["Main.InitErrorTitle"] = "错误",
            ["Main.InitErrorText"] = "初始化失败: {0}",
            ["Main.HintTitle"] = "提示",
            ["Main.SelectRunningTarget"] = "请先选择一个运行中的窗口。",
            ["Main.PickWindowHint"] = "左键点击目标窗口，右键或 Esc 取消。",
            ["Main.NewGroup"] = "新建分组",
            ["Main.Ungrouped"] = "未分组",
            ["Main.GroupSetHideHotkey"] = "设置分组隐藏热键",
            ["Main.GroupSetShowHotkey"] = "设置分组显示热键",
            ["Main.GroupDeleteTitle"] = "删除分组",
            ["Main.GroupDeletePrompt"] = "删除分组 {0}？组内程序会移回未分组。",
            ["Main.TargetEnabled"] = "启用",
            ["Main.TargetMuteOnHide"] = "隐藏时静音",
            ["Main.GroupRename"] = "重命名分组",
            ["Main.GroupDelete"] = "删除分组",
            ["Main.GroupToggle"] = "折叠或展开分组",
            ["Main.EmptyGroupHint"] = "拖动程序到这里",
            ["Main.UngroupedDropHint"] = "拖到这里可取消分组",

            ["Tray.ShowMain"] = "显示主窗口",
            ["Tray.HideTargets"] = "隐藏目标",
            ["Tray.ShowTargets"] = "恢复目标",
            ["Tray.CheckUpdates"] = "检查更新",
            ["Tray.Exit"] = "退出",
            ["Tray.MinimizedTitle"] = "HideProcess",
            ["Tray.MinimizedText"] = "程序已最小化到托盘。",

            ["Settings.Title"] = "设置",
            ["Settings.Header"] = "应用设置",
            ["Settings.Hotkeys"] = "全局热键",
            ["Settings.HideHotkey"] = "隐藏热键",
            ["Settings.ShowHotkey"] = "显示热键",
            ["Settings.SetHotkey"] = "设置热键",
            ["Settings.GroupHotkeys"] = "分组热键",
            ["Settings.GroupTarget"] = "目标分组",
            ["Settings.GroupHideHotkey"] = "分组隐藏热键",
            ["Settings.GroupShowHotkey"] = "分组显示热键",
            ["Settings.General"] = "通用",
            ["Settings.StartWithWindows"] = "开机自动启动",
            ["Settings.MinimizeToTray"] = "关闭窗口时最小化到托盘",
            ["Settings.AutoCheckUpdates"] = "启动时自动检查更新",
            ["Settings.CheckUpdatesNow"] = "立即检查",
            ["Settings.Language"] = "语言",
            ["Settings.Import"] = "导入",
            ["Settings.Export"] = "导出",
            ["Settings.ImportSuccess"] = "设置导入成功。",
            ["Settings.ExportSuccess"] = "设置导出成功。",
            ["Settings.ImportFailed"] = "导入失败: {0}",
            ["Settings.ExportFailed"] = "导出失败: {0}",
            ["Settings.HotkeyWarningToggle"] = "隐藏和显示热键相同，将启用切换模式。",
            ["Settings.HotkeyWarningNoModifier"] = "{0} 建议包含 Ctrl/Alt/Shift/Win，以避免误触。",
            ["Settings.HotkeyWarningReserved"] = "{0} 可能与系统快捷键冲突: {1}",
            ["Settings.GroupHotkeyWarningToggle"] = "当前分组的隐藏和显示热键相同，将启用切换模式。",
            ["Settings.Cancel"] = "取消",
            ["Settings.Save"] = "保存",
            ["Settings.AboutTooltip"] = "关于",

            ["About.Title"] = "关于",
            ["About.Description"] = "应用与更新信息",
            ["About.CurrentVersion"] = "当前版本",
            ["About.PackageType"] = "打包类型",
            ["About.PackageInstaller"] = "安装包版",
            ["About.PackageSingleFile"] = "单文件版",
            ["About.Author"] = "作者",
            ["About.Github"] = "GitHub",

            ["Hotkey.Title"] = "热键捕获",
            ["Hotkey.TitleHide"] = "设置隐藏热键",
            ["Hotkey.TitleShow"] = "设置显示热键",
            ["Hotkey.Instruction"] = "按下你想要的组合键，然后点击确定。",
            ["Hotkey.Current"] = "当前组合",
            ["Hotkey.Clear"] = "清空",
            ["Hotkey.Ok"] = "确定",
            ["Hotkey.Cancel"] = "取消",
            ["Hotkey.Empty"] = "请至少按下一个按键。",

            ["Update.StatusChecking"] = "正在检查更新...",
            ["Update.StatusNoUpdate"] = "当前已是最新版本。",
            ["Update.NoUpdateTitle"] = "检查更新",
            ["Update.NoUpdateMessage"] = "当前版本 {0} 已是最新版本。",
            ["Update.StatusCheckFailed"] = "检查更新失败: {0}",
            ["Update.CheckFailedTitle"] = "更新失败",
            ["Update.CheckFailed"] = "无法检查更新: {0}",
            ["Update.StatusAvailable"] = "发现新版本: {0}",
            ["Update.AvailableTitle"] = "发现新版本",
            ["Update.AvailablePrompt"] = "发现新版本 {1}（当前 {0}）。现在下载并安装吗？",
            ["Update.ProgressTitle"] = "正在下载更新",
            ["Update.ReleaseNotesLabel"] = "更新说明:",
            ["Update.NoInstallerAsset"] = "最新版本中没有可下载的更新包。",
            ["Update.StatusOpenedReleasePage"] = "已打开版本发布页。",
            ["Update.StatusDownloading"] = "正在下载更新包...",
            ["Update.StatusApplyingSingleFile"] = "更新已下载，正在替换程序并重启...",
            ["Update.StatusStartingInstaller"] = "安装程序已启动，应用即将退出。",
            ["Update.StatusDownloadFailed"] = "下载更新失败: {0}",
            ["Update.DownloadFailed"] = "下载或启动更新失败: {0}"
        },
        ["en-US"] = new Dictionary<string, string>
        {
            ["Main.WindowTitle"] = "HideProcess Boss Key",
            ["Main.Title"] = "HideProcess",
            ["Main.RunningLabel"] = "Running Windows",
            ["Main.Refresh"] = "Refresh",
            ["Main.AddTarget"] = "Add Target",
            ["Main.PickWindow"] = "Pick Window",
            ["Main.SelectedTargets"] = "Selected Targets",
            ["Main.Remove"] = "Remove",
            ["Main.HideNow"] = "Hide Now",
            ["Main.ShowNow"] = "Show Now",
            ["Main.OpenSettings"] = "Settings",
            ["Main.HotkeyHint"] = "Hide: {0}    Show: {1}",
            ["Main.ToggleHint"] = "Toggle: {0}",
            ["Main.LogTitle"] = "Activity Log",
            ["Main.ClearLogs"] = "Clear Logs",
            ["Main.CollapseLogs"] = "Collapse",
            ["Main.ExpandLogs"] = "Expand",
            ["Main.LogCleared"] = "Logs cleared.",
            ["Main.PreviousUncleanExit"] = "Detected previous unclean exit. Please verify target window states.",
            ["Main.StatusReady"] = "Global hotkeys are active.",
            ["Main.StatusRefreshed"] = "Running window list refreshed.",
            ["Main.StatusDuplicate"] = "Target already exists.",
            ["Main.StatusAdded"] = "Target added: {0}",
            ["Main.StatusRemoved"] = "Target removed: {0}",
            ["Main.StatusNoTargets"] = "No target configured.",
            ["Main.StatusHidden"] = "Hidden {0} window(s).",
            ["Main.StatusNoMatched"] = "No matching windows to hide.",
            ["Main.StatusShown"] = "Restored {0} window(s).",
            ["Main.StatusNoHidden"] = "No hidden windows to restore.",
            ["Main.StatusSaveFailed"] = "Save failed: {0}",
            ["Main.StatusSettingsApplied"] = "Settings applied.",
            ["Main.StatusPickerStarted"] = "Window picker mode started.",
            ["Main.StatusPickerBusy"] = "Window picker mode is already active.",
            ["Main.StatusPickerCanceled"] = "Window picker canceled.",
            ["Main.StatusGroupCreated"] = "Group created.",
            ["Main.StatusGroupRenamed"] = "Group renamed.",
            ["Main.StatusMovedToGroup"] = "{0} moved to {1}.",
            ["Main.StatusGroupDeleted"] = "Group deleted.",
            ["Main.InitErrorTitle"] = "Error",
            ["Main.InitErrorText"] = "Initialization failed: {0}",
            ["Main.HintTitle"] = "Info",
            ["Main.SelectRunningTarget"] = "Please select a running window first.",
            ["Main.PickWindowHint"] = "Left-click the target window. Right-click or press Esc to cancel.",
            ["Main.NewGroup"] = "New Group",
            ["Main.Ungrouped"] = "Ungrouped",
            ["Main.GroupSetHideHotkey"] = "Set group hide hotkey",
            ["Main.GroupSetShowHotkey"] = "Set group show hotkey",
            ["Main.GroupDeleteTitle"] = "Delete Group",
            ["Main.GroupDeletePrompt"] = "Delete group {0}? Apps inside it will be moved back to Ungrouped.",
            ["Main.TargetEnabled"] = "Enabled",
            ["Main.TargetMuteOnHide"] = "Mute on hide",
            ["Main.GroupRename"] = "Rename group",
            ["Main.GroupDelete"] = "Delete group",
            ["Main.GroupToggle"] = "Collapse or expand group",
            ["Main.EmptyGroupHint"] = "Drag apps here",
            ["Main.UngroupedDropHint"] = "Drop here to remove from group",

            ["Tray.ShowMain"] = "Show Main Window",
            ["Tray.HideTargets"] = "Hide Targets",
            ["Tray.ShowTargets"] = "Restore Targets",
            ["Tray.CheckUpdates"] = "Check for Updates",
            ["Tray.Exit"] = "Exit",
            ["Tray.MinimizedTitle"] = "HideProcess",
            ["Tray.MinimizedText"] = "App is minimized to tray.",

            ["Settings.Title"] = "Settings",
            ["Settings.Header"] = "App Settings",
            ["Settings.Hotkeys"] = "Global Hotkeys",
            ["Settings.HideHotkey"] = "Hide Hotkey",
            ["Settings.ShowHotkey"] = "Show Hotkey",
            ["Settings.SetHotkey"] = "Set Hotkey",
            ["Settings.GroupHotkeys"] = "Group Hotkeys",
            ["Settings.GroupTarget"] = "Group",
            ["Settings.GroupHideHotkey"] = "Group Hide Hotkey",
            ["Settings.GroupShowHotkey"] = "Group Show Hotkey",
            ["Settings.General"] = "General",
            ["Settings.StartWithWindows"] = "Start with Windows",
            ["Settings.MinimizeToTray"] = "Minimize to tray when closing",
            ["Settings.AutoCheckUpdates"] = "Automatically check updates on startup",
            ["Settings.CheckUpdatesNow"] = "Check now",
            ["Settings.Language"] = "Language",
            ["Settings.Import"] = "Import",
            ["Settings.Export"] = "Export",
            ["Settings.ImportSuccess"] = "Settings imported successfully.",
            ["Settings.ExportSuccess"] = "Settings exported successfully.",
            ["Settings.ImportFailed"] = "Import failed: {0}",
            ["Settings.ExportFailed"] = "Export failed: {0}",
            ["Settings.HotkeyWarningToggle"] = "Hide and show hotkeys are identical. Toggle mode is enabled.",
            ["Settings.HotkeyWarningNoModifier"] = "{0} should include Ctrl/Alt/Shift/Win to avoid accidental trigger.",
            ["Settings.HotkeyWarningReserved"] = "{0} may conflict with system shortcut: {1}",
            ["Settings.GroupHotkeyWarningToggle"] = "Selected group's hide and show hotkeys are identical. Toggle mode is enabled.",
            ["Settings.Cancel"] = "Cancel",
            ["Settings.Save"] = "Save",
            ["Settings.AboutTooltip"] = "About",

            ["About.Title"] = "About",
            ["About.Description"] = "Application and update information",
            ["About.CurrentVersion"] = "Current Version",
            ["About.PackageType"] = "Package Type",
            ["About.PackageInstaller"] = "Installer package",
            ["About.PackageSingleFile"] = "Single-file package",
            ["About.Author"] = "Author",
            ["About.Github"] = "GitHub",

            ["Hotkey.Title"] = "Hotkey Capture",
            ["Hotkey.TitleHide"] = "Set Hide Hotkey",
            ["Hotkey.TitleShow"] = "Set Show Hotkey",
            ["Hotkey.Instruction"] = "Press your preferred key combination, then click OK.",
            ["Hotkey.Current"] = "Current Combination",
            ["Hotkey.Clear"] = "Clear",
            ["Hotkey.Ok"] = "OK",
            ["Hotkey.Cancel"] = "Cancel",
            ["Hotkey.Empty"] = "Please press at least one key.",

            ["Update.StatusChecking"] = "Checking for updates...",
            ["Update.StatusNoUpdate"] = "You are using the latest version.",
            ["Update.NoUpdateTitle"] = "Check for Updates",
            ["Update.NoUpdateMessage"] = "Current version {0} is up to date.",
            ["Update.StatusCheckFailed"] = "Update check failed: {0}",
            ["Update.CheckFailedTitle"] = "Update Error",
            ["Update.CheckFailed"] = "Unable to check for updates: {0}",
            ["Update.StatusAvailable"] = "New version available: {0}",
            ["Update.AvailableTitle"] = "Update Available",
            ["Update.AvailablePrompt"] = "A newer version {1} is available (current: {0}). Download and install now?",
            ["Update.ProgressTitle"] = "Downloading update",
            ["Update.ReleaseNotesLabel"] = "Release notes:",
            ["Update.NoInstallerAsset"] = "No downloadable package was found in the latest release.",
            ["Update.StatusOpenedReleasePage"] = "Release page opened.",
            ["Update.StatusDownloading"] = "Downloading installer...",
            ["Update.StatusApplyingSingleFile"] = "Update downloaded. Replacing executable and restarting...",
            ["Update.StatusStartingInstaller"] = "Installer started. The app will now exit.",
            ["Update.StatusDownloadFailed"] = "Update download failed: {0}",
            ["Update.DownloadFailed"] = "Failed to download or start updater: {0}"
        }
    };

    public static IReadOnlyList<LanguageOption> SupportedLanguages { get; } =
    [
        new("zh-CN", "简体中文"),
        new("en-US", "English")
    ];

    public static string CurrentLanguage { get; private set; } = DefaultLanguage;

    public static event EventHandler? LanguageChanged;

    public static void SetLanguage(string? languageCode)
    {
        var normalized = NormalizeLanguage(languageCode);
        if (string.Equals(CurrentLanguage, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentLanguage = normalized;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string NormalizeLanguage(string? languageCode)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var exact = Resources.Keys.FirstOrDefault(key => string.Equals(key, languageCode, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exact))
            {
                return exact;
            }
        }

        return DefaultLanguage;
    }

    public static string T(string key)
    {
        return T(key, CurrentLanguage);
    }

    public static string T(string key, string? languageCode)
    {
        var lang = NormalizeLanguage(languageCode);
        if (Resources.TryGetValue(lang, out var selected) && selected.TryGetValue(key, out var value))
        {
            return value;
        }

        if (Resources[DefaultLanguage].TryGetValue(key, out var fallback))
        {
            return fallback;
        }

        return key;
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, T(key), args);
    }
}
