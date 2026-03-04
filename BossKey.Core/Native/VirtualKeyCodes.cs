namespace BossKey.Core.Native;

public static class VirtualKeyCodes
{
    public const int LeftMouse = 0x01;
    public const int RightMouse = 0x02;
    public const int MiddleMouse = 0x04;
    public const int XButton1 = 0x05;
    public const int XButton2 = 0x06;
    public const int LeftShift = 0xA0;
    public const int RightShift = 0xA1;
    public const int LeftControl = 0xA2;
    public const int RightControl = 0xA3;
    public const int LeftAlt = 0xA4;
    public const int RightAlt = 0xA5;
    public const int LeftWin = 0x5B;
    public const int RightWin = 0x5C;

    public static int Normalize(int key)
    {
        return key switch
        {
            LeftShift or RightShift => 0x10,
            LeftControl or RightControl => 0x11,
            LeftAlt or RightAlt => 0x12,
            RightWin => LeftWin,
            _ => key
        };
    }

    public static bool IsExtended(int key)
    {
        return key is 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28
            or 0x2D or 0x2E or 0x90 or 0x91 or RightControl or RightAlt;
    }

    public static bool TryGetMouseButtonFromMessage(int message, uint mouseData, out int key, out bool isDown)
    {
        isDown = true;
        key = message switch
        {
            NativeMethods.WmLButtonDown or NativeMethods.WmLButtonUp => LeftMouse,
            NativeMethods.WmRButtonDown or NativeMethods.WmRButtonUp => RightMouse,
            NativeMethods.WmMButtonDown or NativeMethods.WmMButtonUp => MiddleMouse,
            NativeMethods.WmXButtonDown or NativeMethods.WmXButtonUp => ((mouseData >> 16) & 0xFFFF) switch
            {
                1 => XButton1,
                2 => XButton2,
                _ => 0
            },
            _ => 0
        };

        if (key <= 0)
        {
            return false;
        }

        isDown = message is NativeMethods.WmLButtonDown
            or NativeMethods.WmRButtonDown
            or NativeMethods.WmMButtonDown
            or NativeMethods.WmXButtonDown;
        return true;
    }
}
