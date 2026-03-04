using System.Text.RegularExpressions;

namespace BossKey.Core.Models;

public sealed class TargetGroupConfig
{
    public const string DefaultGroupId = "default";
    public const string DefaultGroupIconColor = "#FFB8C6D6";
    public const string StandardGroupIconColor = "#FF67B2FF";
    private static readonly Regex HexColorPattern = new("^#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.Compiled);
    private static readonly (double HueOffset, double Saturation, double Lightness)[] LightIconProfiles =
    [
        (-24d, 0.84d, 0.74d),
        (-10d, 0.86d, 0.76d),
        (0d, 0.88d, 0.77d),
        (16d, 0.84d, 0.75d),
        (34d, 0.82d, 0.76d),
        (56d, 0.8d, 0.75d),
        (82d, 0.78d, 0.73d),
        (122d, 0.76d, 0.72d),
        (-72d, 0.82d, 0.75d),
        (-108d, 0.86d, 0.74d)
    ];

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string IconColor { get; set; } = StandardGroupIconColor;
    public HotkeyBinding HideHotkey { get; set; } = new();
    public HotkeyBinding ShowHotkey { get; set; } = new();
    public bool IsCollapsed { get; set; }
    public List<TargetAppConfig> Targets { get; set; } = [];

    public static string CreateRandomLightIconColor(string? baseColor = null)
    {
        var normalizedBaseColor = NormalizeIconColor(baseColor, null);
        var (baseHue, baseSaturation, baseLightness) = ToHsl(ParseColor(normalizedBaseColor));
        var profile = LightIconProfiles[Random.Shared.Next(LightIconProfiles.Length)];
        var hue = WrapHue(baseHue + profile.HueOffset);
        var saturation = Clamp01(Math.Max(baseSaturation, profile.Saturation));
        var lightness = Clamp01(Math.Max(baseLightness, profile.Lightness));
        return ToHex(FromHsl(hue, saturation, lightness));
    }

    public static string GetDefaultIconColor(string? groupId)
    {
        return string.Equals(groupId, DefaultGroupId, StringComparison.OrdinalIgnoreCase)
            ? DefaultGroupIconColor
            : StandardGroupIconColor;
    }

    public static string NormalizeIconColor(string? iconColor, string? groupId)
    {
        return !string.IsNullOrWhiteSpace(iconColor) && HexColorPattern.IsMatch(iconColor)
            ? iconColor
            : GetDefaultIconColor(groupId);
    }

    private static (double Hue, double Saturation, double Lightness) ToHsl((byte R, byte G, byte B) color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2d;

        if (Math.Abs(max - min) < double.Epsilon)
        {
            return (0d, 0d, lightness);
        }

        var delta = max - min;
        var saturation = lightness > 0.5d
            ? delta / (2d - max - min)
            : delta / (max + min);

        double hue;
        if (Math.Abs(max - r) < double.Epsilon)
        {
            hue = ((g - b) / delta) + (g < b ? 6d : 0d);
        }
        else if (Math.Abs(max - g) < double.Epsilon)
        {
            hue = ((b - r) / delta) + 2d;
        }
        else
        {
            hue = ((r - g) / delta) + 4d;
        }

        return (hue * 60d, saturation, lightness);
    }

    private static (byte R, byte G, byte B) FromHsl(double hue, double saturation, double lightness)
    {
        if (saturation <= 0d)
        {
            var gray = (byte)Math.Round(lightness * 255d);
            return (gray, gray, gray);
        }

        var q = lightness < 0.5d
            ? lightness * (1d + saturation)
            : lightness + saturation - (lightness * saturation);
        var p = (2d * lightness) - q;
        var hk = hue / 360d;

        var r = HueToRgb(p, q, hk + (1d / 3d));
        var g = HueToRgb(p, q, hk);
        var b = HueToRgb(p, q, hk - (1d / 3d));
        return ((byte)Math.Round(r * 255d), (byte)Math.Round(g * 255d), (byte)Math.Round(b * 255d));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0d)
        {
            t += 1d;
        }

        if (t > 1d)
        {
            t -= 1d;
        }

        if (t < (1d / 6d))
        {
            return p + ((q - p) * 6d * t);
        }

        if (t < 0.5d)
        {
            return q;
        }

        if (t < (2d / 3d))
        {
            return p + ((q - p) * ((2d / 3d) - t) * 6d);
        }

        return p;
    }

    private static (byte R, byte G, byte B) ParseColor(string color)
    {
        var hex = color.TrimStart('#');
        if (hex.Length == 8)
        {
            hex = hex[2..];
        }

        return (
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }

    private static string ToHex((byte R, byte G, byte B) color)
    {
        return $"#FF{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static double WrapHue(double hue)
    {
        hue %= 360d;
        return hue < 0d ? hue + 360d : hue;
    }

    private static double Clamp01(double value)
    {
        return Math.Max(0d, Math.Min(1d, value));
    }
}
