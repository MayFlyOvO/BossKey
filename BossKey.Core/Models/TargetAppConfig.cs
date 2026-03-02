namespace BossKey.Core.Models;

public sealed class TargetAppConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProcessName { get; set; } = string.Empty;
    public string? ProcessPath { get; set; }
    public bool Enabled { get; set; } = true;
    public bool MuteOnHide { get; set; }
}
