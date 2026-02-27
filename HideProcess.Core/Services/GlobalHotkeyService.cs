using System.ComponentModel;
using System.Runtime.InteropServices;
using HideProcess.Core.Models;
using HideProcess.Core.Native;

namespace HideProcess.Core.Services;

public enum HotkeyAction
{
    Hide,
    Show,
    Toggle
}

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly HashSet<int> _pressedKeys = [];
    private readonly object _syncLock = new();
    private readonly HashSet<int> _activeSuppressedChord = [];
    private NativeMethods.LowLevelKeyboardProc? _hookProc;
    private IntPtr _hookHandle;
    private HashSet<int> _hideKeys = [];
    private HashSet<int> _showKeys = [];
    private HashSet<int> _toggleKeys = [];
    private bool _useToggleMode;
    private bool _hideFired;
    private bool _showFired;
    private bool _toggleFired;
    private bool _disposed;

    public event EventHandler<HotkeyAction>? HotkeyTriggered;

    // Default true: once hotkey triggers, swallow the chord so it does not propagate to system/app.
    public bool SuppressTriggeredHotkeys { get; set; } = true;

    public void UpdateBindings(HotkeyBinding hideBinding, HotkeyBinding showBinding)
    {
        lock (_syncLock)
        {
            _hideKeys = hideBinding.GetNormalizedKeys();
            _showKeys = showBinding.GetNormalizedKeys();
            _useToggleMode = _hideKeys.Count > 0 && _hideKeys.SetEquals(_showKeys);
            _toggleKeys = _useToggleMode ? _hideKeys : [];
            _hideFired = false;
            _showFired = false;
            _toggleFired = false;
            _pressedKeys.Clear();
            _activeSuppressedChord.Clear();
        }
    }

    public void Start()
    {
        ThrowIfDisposed();
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        _hookProc = HookCallback;
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _hookProc, IntPtr.Zero, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install low-level keyboard hook.");
        }
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _hookProc = null;
        lock (_syncLock)
        {
            _pressedKeys.Clear();
            _activeSuppressedChord.Clear();
            _hideFired = false;
            _showFired = false;
            _toggleFired = false;
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

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var hookData = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
        var key = VirtualKeyCodes.Normalize((int)hookData.VkCode);
        if (key <= 0)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var isKeyDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
        var isKeyUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;
        var shouldSuppress = false;

        lock (_syncLock)
        {
            if (isKeyDown)
            {
                _pressedKeys.Add(key);
                var action = EvaluateHotkeys();
                if (action.HasValue && SuppressTriggeredHotkeys)
                {
                    _activeSuppressedChord.Clear();
                    _activeSuppressedChord.UnionWith(GetActionKeys(action.Value));
                    shouldSuppress = true;
                }
            }
            else if (isKeyUp)
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
            : NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private HotkeyAction? EvaluateHotkeys()
    {
        if (_useToggleMode)
        {
            if (IsMatch(_toggleKeys) && !_toggleFired)
            {
                _toggleFired = true;
                HotkeyTriggered?.Invoke(this, HotkeyAction.Toggle);
                return HotkeyAction.Toggle;
            }

            return null;
        }

        if (IsMatch(_hideKeys) && !_hideFired)
        {
            _hideFired = true;
            HotkeyTriggered?.Invoke(this, HotkeyAction.Hide);
            return HotkeyAction.Hide;
        }

        if (IsMatch(_showKeys) && !_showFired)
        {
            _showFired = true;
            HotkeyTriggered?.Invoke(this, HotkeyAction.Show);
            return HotkeyAction.Show;
        }

        return null;
    }

    private void ResetFireFlagsWhenChordBroken()
    {
        if (_useToggleMode)
        {
            if (!IsMatch(_toggleKeys))
            {
                _toggleFired = false;
            }

            return;
        }

        if (!IsMatch(_hideKeys))
        {
            _hideFired = false;
        }

        if (!IsMatch(_showKeys))
        {
            _showFired = false;
        }
    }

    // Subset match supports 3+ keys robustly (e.g., Ctrl+Shift+X) even if other keys are currently held.
    private bool IsMatch(HashSet<int> targetKeys)
    {
        return targetKeys.Count > 0 && targetKeys.IsSubsetOf(_pressedKeys);
    }

    private HashSet<int> GetActionKeys(HotkeyAction action)
    {
        return action switch
        {
            HotkeyAction.Hide => _useToggleMode ? _toggleKeys : _hideKeys,
            HotkeyAction.Show => _showKeys,
            HotkeyAction.Toggle => _toggleKeys,
            _ => []
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
