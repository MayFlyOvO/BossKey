using System.ComponentModel;
using System.Runtime.InteropServices;
using BossKey.Core.Models;
using BossKey.Core.Native;

namespace BossKey.Core.Services;

public sealed class WindowPickerService(ProcessWindowService processWindowService) : IDisposable
{
    private readonly object _syncLock = new();
    private TaskCompletionSource<WindowPickResult>? _pendingSelection;
    private NativeMethods.LowLevelMouseProc? _mouseHookProc;
    private NativeMethods.LowLevelKeyboardProc? _keyboardHookProc;
    private IntPtr _mouseHookHandle;
    private IntPtr _keyboardHookHandle;
    private nint _hoverHandle;
    private bool _disposed;

    public event EventHandler<WindowPickerHoverChangedEventArgs>? HoverTargetChanged;

    public bool IsPicking
    {
        get
        {
            lock (_syncLock)
            {
                return _pendingSelection is not null;
            }
        }
    }

    public Task<WindowPickResult> PickAsync()
    {
        lock (_syncLock)
        {
            ThrowIfDisposed();

            if (_pendingSelection is not null)
            {
                throw new InvalidOperationException("Window picker is already active.");
            }

            _pendingSelection = new TaskCompletionSource<WindowPickResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _mouseHookProc = MouseHookCallback;
            _keyboardHookProc = KeyboardHookCallback;

            _mouseHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _mouseHookProc, IntPtr.Zero, 0);
            if (_mouseHookHandle == IntPtr.Zero)
            {
                CleanupHooks();
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install low-level mouse hook.");
            }

            _keyboardHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _keyboardHookProc, IntPtr.Zero, 0);
            if (_keyboardHookHandle == IntPtr.Zero)
            {
                CleanupHooks();
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install low-level keyboard hook.");
            }

            return _pendingSelection.Task;
        }
    }

    public void Cancel()
    {
        Complete(WindowPickResult.Canceled());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Complete(WindowPickResult.Canceled());
        _disposed = true;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        if (message == NativeMethods.WmMouseMove)
        {
            var hookData = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);
            UpdateHoverState(hookData.Pt.X, hookData.Pt.Y);
        }
        else if (message == NativeMethods.WmLButtonDown)
        {
            var hookData = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);
            if (processWindowService.TryGetRunningTargetFromScreenPoint(hookData.Pt.X, hookData.Pt.Y, out var target))
            {
                Complete(WindowPickResult.Selected(target));
            }

            return new IntPtr(1);
        }
        else if (message == NativeMethods.WmRButtonDown)
        {
            Complete(WindowPickResult.Canceled());
            return new IntPtr(1);
        }

        return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        if (message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown)
        {
            var hookData = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
            if (hookData.VkCode == 0x1B)
            {
                Complete(WindowPickResult.Canceled());
                return new IntPtr(1);
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    private void Complete(WindowPickResult result)
    {
        TaskCompletionSource<WindowPickResult>? completionSource;
        lock (_syncLock)
        {
            completionSource = _pendingSelection;
            if (completionSource is null)
            {
                return;
            }

            _pendingSelection = null;
            _hoverHandle = IntPtr.Zero;
            CleanupHooks();
        }

        HoverTargetChanged?.Invoke(this, new WindowPickerHoverChangedEventArgs(null));
        completionSource.TrySetResult(result);
    }

    private void UpdateHoverState(int x, int y)
    {
        WindowPickerHoverTarget? hoverTarget = null;
        nint nextHandle = IntPtr.Zero;

        if (processWindowService.TryGetRunningTargetFromScreenPoint(x, y, out var target)
            && NativeMethods.GetWindowRect(target.WindowHandle, out var rect))
        {
            nextHandle = target.WindowHandle;
            hoverTarget = new WindowPickerHoverTarget(
                target,
                rect.Left,
                rect.Top,
                Math.Max(0, rect.Right - rect.Left),
                Math.Max(0, rect.Bottom - rect.Top));
        }

        lock (_syncLock)
        {
            if (_pendingSelection is null || _hoverHandle == nextHandle)
            {
                return;
            }

            _hoverHandle = nextHandle;
        }

        HoverTargetChanged?.Invoke(this, new WindowPickerHoverChangedEventArgs(hoverTarget));
    }

    private void CleanupHooks()
    {
        if (_mouseHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }

        if (_keyboardHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }

        _mouseHookProc = null;
        _keyboardHookProc = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

public sealed record WindowPickerHoverTarget(
    RunningTargetInfo Target,
    int Left,
    int Top,
    int Width,
    int Height);

public sealed class WindowPickerHoverChangedEventArgs(WindowPickerHoverTarget? hoverTarget) : EventArgs
{
    public WindowPickerHoverTarget? HoverTarget { get; } = hoverTarget;
}

public sealed record WindowPickResult(bool IsCanceled, RunningTargetInfo? Target)
{
    public static WindowPickResult Selected(RunningTargetInfo target)
    {
        return new WindowPickResult(false, target);
    }

    public static WindowPickResult Canceled()
    {
        return new WindowPickResult(true, null);
    }
}
