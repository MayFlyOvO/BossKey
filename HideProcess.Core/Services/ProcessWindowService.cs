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
    public bool HasHiddenWindowsInGroup(string groupId) =>
        _hiddenWindows.Values.Any(hiddenWindow => string.Equals(hiddenWindow.GroupId, groupId, StringComparison.Ordinal));

    public IReadOnlyList<RunningTargetInfo> GetRunningTargets()
    {
        var result = new List<RunningTargetInfo>();
        foreach (var window in EnumerateWindows())
        {
            result.Add(new RunningTargetInfo
            {
                WindowHandle = window.Handle,
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

    public bool TryGetRunningTargetFromScreenPoint(int x, int y, out RunningTargetInfo target)
    {
        var point = new NativeMethods.Point { X = x, Y = y };
        var handle = NativeMethods.WindowFromPoint(point);
        if (handle == IntPtr.Zero)
        {
            target = null!;
            return false;
        }

        var rootHandle = NativeMethods.GetAncestor(handle, NativeMethods.GaRoot);
        if (rootHandle != IntPtr.Zero)
        {
            handle = rootHandle;
        }

        return TryBuildRunningTargetInfo(handle, out target);
    }

    public int HideTargets(IEnumerable<TargetAppConfig> targets, string? groupId = null)
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
                TryMuteMatchingProcessesForCurrentSession(target, groupId);
            }
        }

        var hiddenCount = 0;
        foreach (var (window, _) in matchedWindows)
        {
            var showState = GetWindowShowState(window.Handle);
            if (NativeMethods.ShowWindow(window.Handle, NativeMethods.SwHide))
            {
                _hiddenWindows[window.Handle] = new HiddenWindowState(window.Handle, showState, groupId);
                hiddenCount++;
            }
        }

        return hiddenCount;
    }

    public int ShowHiddenTargets(string? groupId = null, bool bringToFront = true)
    {
        if (_hiddenWindows.Count == 0)
        {
            RestoreMutedProcesses(groupId);
            return 0;
        }

        var restoredCount = 0;
        IntPtr? firstRestoredWindow = null;

        var hiddenWindows = _hiddenWindows.Values
            .Where(hiddenWindow => groupId is null || string.Equals(hiddenWindow.GroupId, groupId, StringComparison.Ordinal))
            .ToList();
        if (hiddenWindows.Count == 0)
        {
            RestoreMutedProcesses(groupId);
            return 0;
        }

        foreach (var hiddenWindow in hiddenWindows)
        {
            _hiddenWindows.Remove(hiddenWindow.Handle);
        }

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

        RestoreMutedProcesses(groupId);
        return restoredCount;
    }

    public int ShowHiddenTargets(IEnumerable<TargetAppConfig> targets, string? groupId = null, bool bringToFront = true)
    {
        var targetList = targets.ToList();
        if (targetList.Count == 0)
        {
            RestoreMutedProcesses(targetList, groupId);
            return 0;
        }

        var hiddenWindows = _hiddenWindows.Values
            .Where(hiddenWindow => groupId is null || string.Equals(hiddenWindow.GroupId, groupId, StringComparison.Ordinal))
            .Where(hiddenWindow => TryBuildHiddenWindowInfo(hiddenWindow.Handle, out var windowInfo)
                                   && TryGetMatchingTarget(windowInfo.ProcessName, windowInfo.ProcessPath, targetList, out _))
            .ToList();
        if (hiddenWindows.Count == 0)
        {
            RestoreMutedProcesses(targetList, groupId);
            return 0;
        }

        var restoredCount = 0;
        IntPtr? firstRestoredWindow = null;
        foreach (var hiddenWindow in hiddenWindows)
        {
            _hiddenWindows.Remove(hiddenWindow.Handle);
        }

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

            if (hiddenWindow.ShowState != WindowShowState.Minimized)
            {
                firstRestoredWindow ??= handle;
            }
        }

        if (bringToFront && firstRestoredWindow.HasValue)
        {
            NativeMethods.SetForegroundWindow(firstRestoredWindow.Value);
        }

        RestoreMutedProcesses(targetList, groupId);
        return restoredCount;
    }

    private static bool TryGetMatchingTarget(
        WindowInfo window,
        IEnumerable<TargetAppConfig> targets,
        out TargetAppConfig matchedTarget)
    {
        return TryGetMatchingTarget(window.ProcessName, window.ProcessPath, targets, out matchedTarget);
    }

    private static bool TryGetMatchingTarget(
        string processName,
        string? processPath,
        IEnumerable<TargetAppConfig> targets,
        out TargetAppConfig matchedTarget)
    {
        foreach (var target in targets)
        {
            if (!string.IsNullOrWhiteSpace(target.ProcessPath)
                && !string.IsNullOrWhiteSpace(processPath)
                && string.Equals(target.ProcessPath, processPath, StringComparison.OrdinalIgnoreCase))
            {
                matchedTarget = target;
                return true;
            }

            if (string.Equals(NormalizeProcessName(target.ProcessName), processName, StringComparison.OrdinalIgnoreCase))
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

        NativeMethods.EnumWindows((windowHandle, _) =>
        {
            if (!TryBuildWindowInfo(windowHandle, out var windowInfo))
            {
                return true;
            }

            windows.Add(windowInfo);
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static bool TryBuildRunningTargetInfo(IntPtr handle, out RunningTargetInfo target)
    {
        if (!TryBuildWindowInfo(handle, out var windowInfo))
        {
            target = null!;
            return false;
        }

        target = new RunningTargetInfo
        {
            WindowHandle = windowInfo.Handle,
            ProcessId = windowInfo.ProcessId,
            ProcessName = windowInfo.ProcessName,
            ProcessPath = windowInfo.ProcessPath,
            WindowTitle = windowInfo.WindowTitle
        };
        return true;
    }

    private static bool TryBuildHiddenWindowInfo(IntPtr handle, out WindowInfo windowInfo)
    {
        windowInfo = null!;

        if (!NativeMethods.IsWindow(handle))
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(handle, out var processIdRaw);
        var processId = unchecked((int)processIdRaw);
        if (processId <= 0 || processId == Environment.ProcessId)
        {
            return false;
        }

        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch
        {
            return false;
        }

        try
        {
            var processName = NormalizeProcessName(process.ProcessName);
            var processPath = TryGetProcessPath(process);
            windowInfo = new WindowInfo(handle, processId, processName, processPath, string.Empty);
            return true;
        }
        finally
        {
            process.Dispose();
        }
    }

    private static bool TryBuildWindowInfo(IntPtr handle, out WindowInfo windowInfo)
    {
        windowInfo = null!;

        if (!NativeMethods.IsWindow(handle) || !NativeMethods.IsWindowVisible(handle))
        {
            return false;
        }

        if (NativeMethods.GetWindowTextLength(handle) == 0)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(handle, out var processIdRaw);
        var processId = unchecked((int)processIdRaw);
        if (processId <= 0 || processId == Environment.ProcessId)
        {
            return false;
        }

        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch
        {
            return false;
        }

        try
        {
            var processName = NormalizeProcessName(process.ProcessName);
            var processPath = TryGetProcessPath(process);
            var windowTitle = GetWindowTitle(handle);
            if (string.IsNullOrWhiteSpace(windowTitle))
            {
                return false;
            }

            windowInfo = new WindowInfo(handle, processId, processName, processPath, windowTitle);
            return true;
        }
        finally
        {
            process.Dispose();
        }
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

    private void TryMuteMatchingProcessesForCurrentSession(TargetAppConfig target, string? groupId)
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
            _mutedProcesses[processId] = new ProcessMuteSnapshot(processId, originalMuteState, groupId);
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

    private void RestoreMutedProcesses(string? groupId)
    {
        if (_mutedProcesses.Count == 0)
        {
            return;
        }

        var targetEntries = _mutedProcesses
            .Where(entry => groupId is null || string.Equals(entry.Value.GroupId, groupId, StringComparison.Ordinal))
            .ToList();
        if (targetEntries.Count == 0)
        {
            return;
        }

        var snapshots = targetEntries.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value.OriginalMuteState);
        foreach (var entry in targetEntries)
        {
            _mutedProcesses.Remove(entry.Key);
        }

        _processAudioMuteService.RestoreMuteStates(snapshots);
    }

    private void RestoreMutedProcesses(IEnumerable<TargetAppConfig> targets, string? groupId)
    {
        if (_mutedProcesses.Count == 0)
        {
            return;
        }

        var processIds = targets
            .SelectMany(EnumerateMatchingProcessIds)
            .ToHashSet();
        if (processIds.Count == 0)
        {
            return;
        }

        var targetEntries = _mutedProcesses
            .Where(entry => processIds.Contains(entry.Key)
                            && (groupId is null || string.Equals(entry.Value.GroupId, groupId, StringComparison.Ordinal)))
            .ToList();
        if (targetEntries.Count == 0)
        {
            return;
        }

        var snapshots = targetEntries.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value.OriginalMuteState);
        foreach (var entry in targetEntries)
        {
            _mutedProcesses.Remove(entry.Key);
        }

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
        WindowShowState ShowState,
        string? GroupId);

    private sealed record ProcessMuteSnapshot(
        int ProcessId,
        bool OriginalMuteState,
        string? GroupId);

    private enum WindowShowState
    {
        Normal,
        Minimized,
        Maximized
    }
}
