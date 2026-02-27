namespace HideProcess.Core.Models;

public sealed class AppSettings
{
    public HotkeyBinding HideHotkey { get; set; } = HotkeyBinding.FromKeys([0x11, 0x12, 0x48]);
    public HotkeyBinding ShowHotkey { get; set; } = HotkeyBinding.FromKeys([0x11, 0x12, 0x53]);
    public List<TargetAppConfig> Targets { get; set; } = [];
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoCheckForUpdates { get; set; } = true;
    public DateTime? LastUpdateCheckUtc { get; set; }
    public bool IsLogPanelCollapsed { get; set; }
    public string Language { get; set; } = "zh-CN";
    public WindowPlacementSettings? MainWindowPlacement { get; set; }
}
