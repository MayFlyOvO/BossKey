using System.Text;
using HideProcess.Core.Models;
using HideProcess.Core.Native;

namespace HideProcess.Core.Services;

public static class HotkeyFormatter
{
    private static readonly Dictionary<int, string> KnownKeyNames = new()
    {
        [0x10] = "Shift",
        [0x11] = "Ctrl",
        [0x12] = "Alt",
        [0x5B] = "Win",
        [0x20] = "Space",
        [0x0D] = "Enter",
        [0x1B] = "Esc",
        [0x09] = "Tab",
        [0x08] = "Backspace",
        [0x2E] = "Delete",
        [0x2D] = "Insert",
        [0x24] = "Home",
        [0x23] = "End",
        [0x21] = "PageUp",
        [0x22] = "PageDown",
        [0x25] = "Left",
        [0x26] = "Up",
        [0x27] = "Right",
        [0x28] = "Down"
    };

    public static string Format(HotkeyBinding binding)
    {
        var keys = binding.GetNormalizedKeys().Order().ToList();
        if (keys.Count == 0)
        {
            return "Not Set";
        }

        var names = keys.Select(GetKeyName);
        return string.Join(" + ", names);
    }

    private static string GetKeyName(int key)
    {
        if (KnownKeyNames.TryGetValue(key, out var known))
        {
            return known;
        }

        if (key >= 0x70 && key <= 0x7B)
        {
            return $"F{key - 0x6F}";
        }

        var scanCode = NativeMethods.MapVirtualKey((uint)key, 0);
        var lParam = (int)(scanCode << 16);
        if (VirtualKeyCodes.IsExtended(key))
        {
            lParam |= 1 << 24;
        }

        var builder = new StringBuilder(64);
        var len = NativeMethods.GetKeyNameText(lParam, builder, builder.Capacity);
        if (len > 0)
        {
            return builder.ToString();
        }

        return $"VK_{key:X2}";
    }
}
