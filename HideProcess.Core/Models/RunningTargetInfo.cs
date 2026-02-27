namespace HideProcess.Core.Models;

public sealed class RunningTargetInfo
{
    public required nint WindowHandle { get; init; }
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public required string WindowTitle { get; init; }
    public string? ProcessPath { get; init; }
}
