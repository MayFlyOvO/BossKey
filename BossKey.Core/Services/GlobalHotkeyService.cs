using System.ComponentModel;
using System.Runtime.InteropServices;
using BossKey.Core.Models;
using BossKey.Core.Native;

namespace BossKey.Core.Services;

public enum HotkeyAction
{
    Hide,
    Show,
    Toggle
}

public sealed record HotkeyRouteBinding(
    string RouteId,
    HotkeyBinding HideBinding,
    HotkeyBinding ShowBinding);

public sealed class HotkeyTriggeredEventArgs(string routeId, HotkeyAction action) : EventArgs
{
    public string RouteId { get; } = routeId;
    public HotkeyAction Action { get; } = action;
}

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly HashSet<int> _pressedKeys = [];
    private readonly object _syncLock = new();
    private readonly HashSet<int> _activeSuppressedChord = [];
    private readonly List<RouteState> _routes = [];
    private NativeMethods.LowLevelKeyboardProc? _keyboardHookProc;
    private NativeMethods.LowLevelMouseProc? _mouseHookProc;
    private IntPtr _keyboardHookHandle;
    private IntPtr _mouseHookHandle;
    private bool _disposed;

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

    // Default true: once hotkey triggers, swallow the chord so it does not propagate to system/app.
    public bool SuppressTriggeredHotkeys { get; set; } = true;

    public void UpdateBindings(HotkeyBinding hideBinding, HotkeyBinding showBinding)
    {
        UpdateBindings([new HotkeyRouteBinding("default", hideBinding, showBinding)]);
    }

    public void UpdateBindings(IEnumerable<HotkeyRouteBinding> bindings)
    {
        lock (_syncLock)
        {
            _routes.Clear();
            foreach (var binding in bindings)
            {
                var hideKeys = binding.HideBinding.GetNormalizedKeys();
                var showKeys = binding.ShowBinding.GetNormalizedKeys();
                var useToggleMode = hideKeys.Count > 0 && hideKeys.SetEquals(showKeys);
                _routes.Add(new RouteState(
                    binding.RouteId,
                    hideKeys,
                    showKeys,
                    useToggleMode ? hideKeys : []));
            }

            _pressedKeys.Clear();
            _activeSuppressedChord.Clear();
        }
    }

    public void Start()
    {
        ThrowIfDisposed();
        if (_keyboardHookHandle != IntPtr.Zero || _mouseHookHandle != IntPtr.Zero)
        {
            return;
        }

        _keyboardHookProc = KeyboardHookCallback;
        _mouseHookProc = MouseHookCallback;

        _keyboardHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _keyboardHookProc, IntPtr.Zero, 0);
        if (_keyboardHookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install low-level keyboard hook.");
        }

        _mouseHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _mouseHookProc, IntPtr.Zero, 0);
        if (_mouseHookHandle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            Stop();
            throw new Win32Exception(error, "Failed to install low-level mouse hook.");
        }
    }

    public void Stop()
    {
        if (_keyboardHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }

        if (_mouseHookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }

        _keyboardHookProc = null;
        _mouseHookProc = null;
        lock (_syncLock)
        {
            _pressedKeys.Clear();
            _activeSuppressedChord.Clear();
            foreach (var route in _routes)
            {
                route.HideFired = false;
                route.ShowFired = false;
                route.ToggleFired = false;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var hookData = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
        var key = VirtualKeyCodes.Normalize((int)hookData.VkCode);
        if (key <= 0)
        {
            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        var isKeyDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
        var isKeyUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;
        if (!isKeyDown && !isKeyUp)
        {
            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        return ProcessInputHookEvent(_keyboardHookHandle, nCode, wParam, lParam, key, isKeyDown);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var hookData = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);
        if (!VirtualKeyCodes.TryGetMouseButtonFromMessage(message, hookData.MouseData, out var key, out var isMouseDown))
        {
            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        return ProcessInputHookEvent(_mouseHookHandle, nCode, wParam, lParam, key, isMouseDown);
    }

    private IntPtr ProcessInputHookEvent(IntPtr hookHandle, int nCode, IntPtr wParam, IntPtr lParam, int key, bool isKeyDown)
    {
        var shouldSuppress = false;

        lock (_syncLock)
        {
            if (isKeyDown)
            {
                _pressedKeys.Add(key);
                var trigger = EvaluateHotkeys();
                if (trigger is not null && SuppressTriggeredHotkeys)
                {
                    _activeSuppressedChord.Clear();
                    _activeSuppressedChord.UnionWith(trigger.Route.GetActionKeys(trigger.Action));
                    shouldSuppress = true;
                }
            }
            else
            {
                _pressedKeys.Remove(key);
                ResetFireFlagsWhenChordBroken();

                if (_activeSuppressedChord.Count > 0 && !_activeSuppressedChord.IsSubsetOf(_pressedKeys))
                {
                    // Chord fully/partially released, consume this key-up and stop suppressing further keys.
                    shouldSuppress = SuppressTriggeredHotkeys && _activeSuppressedChord.Contains(key);
                    _activeSuppressedChord.Clear();
                }
            }

            if (!shouldSuppress
                && SuppressTriggeredHotkeys
                && _activeSuppressedChord.Count > 0
                && _activeSuppressedChord.Contains(key))
            {
                shouldSuppress = true;
            }
        }

        return shouldSuppress
            ? new IntPtr(1)
            : NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
    }

    private HotkeyTrigger? EvaluateHotkeys()
    {
        foreach (var route in _routes)
        {
            if (route.UseToggleMode)
            {
                if (IsMatch(route.ToggleKeys) && !route.ToggleFired)
                {
                    route.ToggleFired = true;
                    HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(route.RouteId, HotkeyAction.Toggle));
                    return new HotkeyTrigger(route, HotkeyAction.Toggle);
                }

                continue;
            }

            if (IsMatch(route.HideKeys) && !route.HideFired)
            {
                route.HideFired = true;
                HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(route.RouteId, HotkeyAction.Hide));
                return new HotkeyTrigger(route, HotkeyAction.Hide);
            }

            if (IsMatch(route.ShowKeys) && !route.ShowFired)
            {
                route.ShowFired = true;
                HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(route.RouteId, HotkeyAction.Show));
                return new HotkeyTrigger(route, HotkeyAction.Show);
            }
        }

        return null;
    }

    private void ResetFireFlagsWhenChordBroken()
    {
        foreach (var route in _routes)
        {
            if (route.UseToggleMode)
            {
                if (!IsMatch(route.ToggleKeys))
                {
                    route.ToggleFired = false;
                }

                continue;
            }

            if (!IsMatch(route.HideKeys))
            {
                route.HideFired = false;
            }

            if (!IsMatch(route.ShowKeys))
            {
                route.ShowFired = false;
            }
        }
    }

    // Subset match supports 3+ keys robustly (e.g., Ctrl+Shift+X) even if other keys are currently held.
    private bool IsMatch(HashSet<int> targetKeys)
    {
        return targetKeys.Count > 0 && targetKeys.IsSubsetOf(_pressedKeys);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class RouteState(
        string routeId,
        HashSet<int> hideKeys,
        HashSet<int> showKeys,
        HashSet<int> toggleKeys)
    {
        public string RouteId { get; } = routeId;
        public HashSet<int> HideKeys { get; } = hideKeys;
        public HashSet<int> ShowKeys { get; } = showKeys;
        public HashSet<int> ToggleKeys { get; } = toggleKeys;
        public bool UseToggleMode { get; } = toggleKeys.Count > 0;
        public bool HideFired { get; set; }
        public bool ShowFired { get; set; }
        public bool ToggleFired { get; set; }

        public HashSet<int> GetActionKeys(HotkeyAction action)
        {
            return action switch
            {
                HotkeyAction.Hide => UseToggleMode ? ToggleKeys : HideKeys,
                HotkeyAction.Show => ShowKeys,
                HotkeyAction.Toggle => ToggleKeys,
                _ => []
            };
        }
    }

    private sealed class HotkeyTrigger(RouteState route, HotkeyAction action)
    {
        public RouteState Route { get; } = route;
        public HotkeyAction Action { get; } = action;
    }
}
