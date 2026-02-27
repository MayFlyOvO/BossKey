namespace HideProcess.Core.Models;

public sealed class TargetAppConfig
{
    public string ProcessName { get; set; } = string.Empty;
    public string? ProcessPath { get; set; }
    public bool Enabled { get; set; } = true;
    public bool MuteOnHide { get; set; }
}
