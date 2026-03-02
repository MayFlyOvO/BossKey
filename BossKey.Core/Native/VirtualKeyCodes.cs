namespace BossKey.Core.Native;

public static class VirtualKeyCodes
{
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
}
