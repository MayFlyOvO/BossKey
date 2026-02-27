using System.Diagnostics;
using System.Text;
using HideProcess.Core.Models;
using HideProcess.Core.Native;

namespace HideProcess.Core.Services;

public sealed class ProcessWindowService
{
    private readonly ProcessAudioMuteService _processAudioMuteService = new();
    private readonly Dictionary<IntPtr, HiddenWindowState> _hiddenWindows = [];
    private readonly Dictionary<int, ProcessMuteSnapshot> _mutedProcesses = [];

    public bool HasHiddenWindows => _hiddenWindows.Count > 0;

    public IReadOnlyList<RunningTargetInfo> GetRunningTargets()
    {
        var result = new List<RunningTargetInfo>();
        foreach (var window in EnumerateWindows())
        {
            result.Add(new RunningTargetInfo
            {
                ProcessId = window.ProcessId,
                ProcessName = window.ProcessName,
                ProcessPath = window.ProcessPath,
                WindowTitle = window.WindowTitle
            });
        }

        return result
            .OrderBy(static x => x.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static x => x.ProcessId)
            .ToList();
    }

    public int HideTargets(IEnumerable<TargetAppConfig> targets)
    {
        var targetList = targets.Where(static t => t.Enabled).ToList();
        if (targetList.Count == 0)
        {
            return 0;
        }

        var matchedWindows = new List<(WindowInfo Window, TargetAppConfig Target)>();
        foreach (var window in EnumerateWindows())
        {
            if (TryGetMatchingTarget(window, targetList, out var matchedTarget))
            {
                matchedWindows.Add((window, matchedTarget));
            }
        }

        if (matchedWindows.Count == 0)
        {
            return 0;
        }

        var processedMuteTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, target) in matchedWindows)
        {
            if (!target.MuteOnHide)
            {
                continue;
            }

            var muteKey = GetTargetMuteKey(target);
            if (processedMuteTargets.Add(muteKey))
            {
                TryMuteMatchingProcessesForCurrentSession(target);
            }
        }

        var hiddenCount = 0;
        foreach (var (window, _) in matchedWindows)
        {
            var showState = GetWindowShowState(window.Handle);
            if (NativeMethods.ShowWindow(window.Handle, NativeMethods.SwHide))
            {
                _hiddenWindows[window.Handle] = new HiddenWindowState(window.Handle, showState);
                hiddenCount++;
            }
        }

        return hiddenCount;
    }

    public int ShowHiddenTargets(bool bringToFront = true)
    {
        if (_hiddenWindows.Count == 0)
        {
            RestoreMutedProcesses();
            return 0;
        }

        var restoredCount = 0;
        IntPtr? firstRestoredWindow = null;

        var hiddenWindows = _hiddenWindows.Values.ToList();
        _hiddenWindows.Clear();

        foreach (var hiddenWindow in hiddenWindows)
        {
            var handle = hiddenWindow.Handle;
            if (!NativeMethods.IsWindow(handle))
            {
                continue;
            }

            var command = hiddenWindow.ShowState switch
            {
                WindowShowState.Minimized => NativeMethods.SwShowMinNoActive,
                WindowShowState.Maximized => NativeMethods.SwShowMaximized,
                _ => NativeMethods.SwRestore
            };
            NativeMethods.ShowWindow(handle, command);
            restoredCount++;

            // Keep focus behavior for non-minimized windows only.
            if (hiddenWindow.ShowState != WindowShowState.Minimized)
            {
                firstRestoredWindow ??= handle;
            }
        }

        if (bringToFront && firstRestoredWindow.HasValue)
        {
            NativeMethods.SetForegroundWindow(firstRestoredWindow.Value);
        }

        RestoreMutedProcesses();
        return restoredCount;
    }

    private static bool TryGetMatchingTarget(
        WindowInfo window,
        IEnumerable<TargetAppConfig> targets,
        out TargetAppConfig matchedTarget)
    {
        foreach (var target in targets)
        {
            if (!string.IsNullOrWhiteSpace(target.ProcessPath)
                && !string.IsNullOrWhiteSpace(window.ProcessPath)
                && string.Equals(target.ProcessPath, window.ProcessPath, StringComparison.OrdinalIgnoreCase))
            {
                matchedTarget = target;
                return true;
            }

            if (string.Equals(NormalizeProcessName(target.ProcessName), window.ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                matchedTarget = target;
                return true;
            }
        }

        matchedTarget = null!;
        return false;
    }

    private static IEnumerable<WindowInfo> EnumerateWindows()
    {
        var windows = new List<WindowInfo>();
        var currentPid = Environment.ProcessId;

        NativeMethods.EnumWindows((windowHandle, _) =>
        {
            if (!NativeMethods.IsWindowVisible(windowHandle))
            {
                return true;
            }

            if (NativeMethods.GetWindowTextLength(windowHandle) == 0)
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(windowHandle, out var processIdRaw);
            var processId = unchecked((int)processIdRaw);
            if (processId <= 0 || processId == currentPid)
            {
                return true;
            }

            Process process;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch
            {
                return true;
            }

            var processName = NormalizeProcessName(process.ProcessName);
            var processPath = TryGetProcessPath(process);
            var windowTitle = GetWindowTitle(windowHandle);
            if (string.IsNullOrWhiteSpace(windowTitle))
            {
                return true;
            }

            windows.Add(new WindowInfo(windowHandle, processId, processName, processPath, windowTitle));
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static string NormalizeProcessName(string processName)
    {
        return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var length = NativeMethods.GetWindowTextLength(handle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    private static string? TryGetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static WindowShowState GetWindowShowState(IntPtr handle)
    {
        if (NativeMethods.IsIconic(handle))
        {
            return WindowShowState.Minimized;
        }

        if (NativeMethods.IsZoomed(handle))
        {
            return WindowShowState.Maximized;
        }

        return WindowShowState.Normal;
    }

    private void TryMuteMatchingProcessesForCurrentSession(TargetAppConfig target)
    {
        var candidates = EnumerateMatchingProcessIds(target)
            .Where(processId => !_mutedProcesses.ContainsKey(processId))
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var snapshots = _processAudioMuteService.CaptureAndMuteProcesses(candidates);
        foreach (var (processId, originalMuteState) in snapshots)
        {
            _mutedProcesses[processId] = new ProcessMuteSnapshot(processId, originalMuteState);
        }
    }

    private static IEnumerable<int> EnumerateMatchingProcessIds(TargetAppConfig target)
    {
        var targetPath = target.ProcessPath;
        var targetName = NormalizeProcessName(target.ProcessName);
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return [];
        }

        var usePathMatch = !string.IsNullOrWhiteSpace(targetPath);
        var processIds = new HashSet<int>();

        foreach (var process in Process.GetProcessesByName(targetName))
        {
            try
            {
                if (process.Id <= 0 || process.Id == Environment.ProcessId)
                {
                    continue;
                }

                if (usePathMatch)
                {
                    var processPath = TryGetProcessPath(process);
                    var pathMatched = !string.IsNullOrWhiteSpace(processPath)
                        && string.Equals(processPath, targetPath, StringComparison.OrdinalIgnoreCase);

                    // If path is inaccessible, keep this process by name fallback for multi-process apps (e.g. Edge).
                    if (!pathMatched && !string.IsNullOrWhiteSpace(processPath))
                    {
                        continue;
                    }
                }

                processIds.Add(process.Id);
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return processIds;
    }

    private static string GetTargetMuteKey(TargetAppConfig target)
    {
        return $"{NormalizeProcessName(target.ProcessName)}|{target.ProcessPath ?? string.Empty}";
    }

    private void RestoreMutedProcesses()
    {
        if (_mutedProcesses.Count == 0)
        {
            return;
        }

        var snapshots = _mutedProcesses.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value.OriginalMuteState);
        _mutedProcesses.Clear();
        _processAudioMuteService.RestoreMuteStates(snapshots);
    }

    private sealed record WindowInfo(
        IntPtr Handle,
        int ProcessId,
        string ProcessName,
        string? ProcessPath,
        string WindowTitle);

    private sealed record HiddenWindowState(
        IntPtr Handle,
        WindowShowState ShowState);

    private sealed record ProcessMuteSnapshot(
        int ProcessId,
        bool OriginalMuteState);

    private enum WindowShowState
    {
        Normal,
        Minimized,
        Maximized
    }
}
