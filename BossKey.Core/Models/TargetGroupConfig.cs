namespace BossKey.Core.Models;

public sealed class TargetGroupConfig
{
    public const string DefaultGroupId = "default";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public HotkeyBinding HideHotkey { get; set; } = new();
    public HotkeyBinding ShowHotkey { get; set; } = new();
    public bool IsCollapsed { get; set; }
    public List<TargetAppConfig> Targets { get; set; } = [];
}
