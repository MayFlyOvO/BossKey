using System.Globalization;

namespace BossKey.Core.Models;

public sealed class ThemePalette
{
    public string WindowBackgroundColor { get; set; } = "#FFF6F8FB";
    public string ChromeBackgroundColor { get; set; } = "#FFF7F8FA";
    public string SurfaceBackgroundColor { get; set; } = "#FFFFFFFF";
    public string PanelBackgroundColor { get; set; } = "#FFFAFBFD";
    public string BorderColor { get; set; } = "#FFE4E7EB";
    public string PrimaryTextColor { get; set; } = "#FF1F2937";
    public string SecondaryTextColor { get; set; } = "#FF4A5560";
    public string AccentColor { get; set; } = "#FF0A84FF";
    public string GroupIconColor { get; set; } = "#FF3B82F6";
    public string SuccessColor { get; set; } = "#FF34C759";
    public string WarningColor { get; set; } = "#FFC2410C";

    public static ThemePalette CreateDefaultLight()
    {
        return new ThemePalette
        {
            WindowBackgroundColor = "#FFF6F8FB",
            ChromeBackgroundColor = "#FFF7F8FA",
            SurfaceBackgroundColor = "#FFFFFFFF",
            PanelBackgroundColor = "#FFFAFBFD",
            BorderColor = "#FFE4E7EB",
            PrimaryTextColor = "#FF1F2937",
            SecondaryTextColor = "#FF4A5560",
            AccentColor = "#FF0A84FF",
            GroupIconColor = "#FF3B82F6",
            SuccessColor = "#FF34C759",
            WarningColor = "#FFC2410C"
        };
    }

    public static ThemePalette CreateDefaultDark()
    {
        return new ThemePalette
        {
            WindowBackgroundColor = "#FF0F1115",
            ChromeBackgroundColor = "#FF171B24",
            SurfaceBackgroundColor = "#FF181D27",
            PanelBackgroundColor = "#FF1F2633",
            BorderColor = "#FF2C3647",
            PrimaryTextColor = "#FFF3F7FF",
            SecondaryTextColor = "#FFB4BED0",
            AccentColor = "#FF5AA2FF",
            GroupIconColor = "#FF7CC0FF",
            SuccessColor = "#FF39C976",
            WarningColor = "#FFF59E0B"
        };
    }

    public ThemePalette Clone()
    {
        return new ThemePalette
        {
            WindowBackgroundColor = WindowBackgroundColor,
            ChromeBackgroundColor = ChromeBackgroundColor,
            SurfaceBackgroundColor = SurfaceBackgroundColor,
            PanelBackgroundColor = PanelBackgroundColor,
            BorderColor = BorderColor,
            PrimaryTextColor = PrimaryTextColor,
            SecondaryTextColor = SecondaryTextColor,
            AccentColor = AccentColor,
            GroupIconColor = GroupIconColor,
            SuccessColor = SuccessColor,
            WarningColor = WarningColor
        };
    }

    public static ThemePalette Normalize(ThemePalette? palette, ThemePalette defaults)
    {
        if (palette is null)
        {
            return defaults.Clone();
        }

        return new ThemePalette
        {
            WindowBackgroundColor = NormalizeColor(palette.WindowBackgroundColor, defaults.WindowBackgroundColor),
            ChromeBackgroundColor = NormalizeColor(palette.ChromeBackgroundColor, defaults.ChromeBackgroundColor),
            SurfaceBackgroundColor = NormalizeColor(palette.SurfaceBackgroundColor, defaults.SurfaceBackgroundColor),
            PanelBackgroundColor = NormalizeColor(palette.PanelBackgroundColor, defaults.PanelBackgroundColor),
            BorderColor = NormalizeColor(palette.BorderColor, defaults.BorderColor),
            PrimaryTextColor = NormalizeColor(palette.PrimaryTextColor, defaults.PrimaryTextColor),
            SecondaryTextColor = NormalizeColor(palette.SecondaryTextColor, defaults.SecondaryTextColor),
            AccentColor = NormalizeColor(palette.AccentColor, defaults.AccentColor),
            GroupIconColor = NormalizeColor(palette.GroupIconColor, defaults.GroupIconColor),
            SuccessColor = NormalizeColor(palette.SuccessColor, defaults.SuccessColor),
            WarningColor = NormalizeColor(palette.WarningColor, defaults.WarningColor)
        };
    }

    public static string NormalizeColor(string? value, string fallback)
    {
        return TryNormalizeColor(value, out var normalized) ? normalized : fallback;
    }

    public static bool TryNormalizeColor(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hex = value.Trim();
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        if (hex.Length == 6)
        {
            hex = $"FF{hex}";
        }

        if (hex.Length != 8 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        normalized = $"#{hex.ToUpperInvariant()}";
        return true;
    }
}
