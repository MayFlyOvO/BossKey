using System.Windows;
using System.Windows.Media;
using BossKey.Core.Models;
using Color = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;

namespace BossKey.App.Services;

public static class ThemeManager
{
    private const string ThemeDictionaryKey = "BossKey.ThemeDictionary";
    private static ThemeSettings _currentTheme = ThemeSettings.CreateDefault();
    public static event EventHandler? ThemeApplied;

    public static ThemeSettings CurrentTheme => _currentTheme.Clone();

    public static void ApplyTheme(ThemeSettings? settings)
    {
        _currentTheme = ThemeSettings.Normalize(settings);
        if (WpfApplication.Current is null)
        {
            return;
        }

        var resources = WpfApplication.Current.Resources;
        if (resources[ThemeDictionaryKey] is ResourceDictionary existing)
        {
            resources.MergedDictionaries.Remove(existing);
        }

        var themeDictionary = BuildThemeDictionary(_currentTheme);
        resources[ThemeDictionaryKey] = themeDictionary;
        resources.MergedDictionaries.Add(themeDictionary);
        ThemeApplied?.Invoke(null, EventArgs.Empty);
    }

    private static ResourceDictionary BuildThemeDictionary(ThemeSettings settings)
    {
        var palette = settings.GetActivePalette();
        var isDark = string.Equals(settings.ActiveMode, ThemeModes.Dark, StringComparison.Ordinal);

        var windowBackground = ParseColor(palette.WindowBackgroundColor);
        var chromeBackground = ParseColor(palette.ChromeBackgroundColor);
        var surface = ParseColor(palette.SurfaceBackgroundColor);
        var panel = ParseColor(palette.PanelBackgroundColor);
        var border = ParseColor(palette.BorderColor);
        var primaryText = ParseColor(palette.PrimaryTextColor);
        var secondaryText = ParseColor(palette.SecondaryTextColor);
        var accent = ParseColor(palette.AccentColor);
        var success = ParseColor(palette.SuccessColor);
        var warning = ParseColor(palette.WarningColor);
        var emphasizedSecondary = Mix(secondaryText, isDark ? Colors.White : Colors.Black, isDark ? 0.14 : 0.18);
        var neutralButtonForeground = Mix(secondaryText, isDark ? Colors.White : Colors.Black, isDark ? 0.18 : 0.22);
        var iconColor = Mix(secondaryText, isDark ? Colors.White : Colors.Black, isDark ? 0.1 : 0.14);
        var statusBadgeIconColor = Mix(secondaryText, isDark ? Colors.White : Colors.Black, isDark ? 0.2 : 0.16);

        var resources = new ResourceDictionary();
        resources["Theme.IsDark"] = isDark;
        SetBrush(resources, "Theme.WindowBackgroundBrush", windowBackground);
        SetBrush(resources, "Theme.ChromeBackgroundBrush", chromeBackground);
        SetBrush(resources, "Theme.SurfaceBackgroundBrush", surface);
        SetBrush(resources, "Theme.PanelBackgroundBrush", panel);
        SetBrush(resources, "Theme.CardBackgroundBrush", surface);
        SetBrush(resources, "Theme.SubtleBackgroundBrush", Mix(panel, surface, isDark ? 0.18 : 0.3));
        SetBrush(resources, "Theme.SubtleBackgroundStrongBrush", Mix(surface, panel, isDark ? 0.62 : 0.74));
        SetBrush(resources, "Theme.BorderBrush", border);
        SetBrush(resources, "Theme.BorderStrongBrush", Mix(border, isDark ? Colors.White : Colors.Black, isDark ? 0.16 : 0.08));
        SetBrush(resources, "Theme.BorderSoftBrush", Mix(border, panel, isDark ? 0.26 : 0.16));
        SetBrush(resources, "Theme.PrimaryTextBrush", primaryText);
        SetBrush(resources, "Theme.SecondaryTextBrush", secondaryText);
        SetBrush(resources, "Theme.SectionHeadingBrush", primaryText);
        SetBrush(resources, "Theme.EmphasizedSecondaryTextBrush", emphasizedSecondary);
        SetBrush(resources, "Theme.TertiaryTextBrush", Mix(secondaryText, panel, isDark ? 0.12 : 0.1));
        SetBrush(resources, "Theme.MutedTextBrush", Mix(secondaryText, panel, isDark ? 0.3 : 0.22));
        SetBrush(resources, "Theme.IconBrush", iconColor);
        SetBrush(resources, "Theme.GroupIconBrush", ParseColor(palette.GroupIconColor));
        SetBrush(resources, "Theme.ButtonNeutralBackgroundBrush", surface);
        SetBrush(resources, "Theme.ButtonNeutralBorderBrush", border);
        SetBrush(resources, "Theme.ButtonNeutralForegroundBrush", neutralButtonForeground);
        SetBrush(resources, "Theme.ButtonNeutralHoverBackgroundBrush", Mix(surface, isDark ? Colors.White : Colors.Black, isDark ? 0.08 : 0.03));
        SetBrush(resources, "Theme.ButtonPrimaryBackgroundBrush", accent);
        SetBrush(resources, "Theme.ButtonPrimaryBorderBrush", accent);
        SetBrush(resources, "Theme.ButtonPrimaryForegroundBrush", Colors.White);
        SetBrush(resources, "Theme.ButtonPrimaryHoverBackgroundBrush", Mix(accent, isDark ? Colors.White : Colors.Black, isDark ? 0.1 : 0.12));
        SetBrush(resources, "Theme.ButtonSuccessBackgroundBrush", success);
        SetBrush(resources, "Theme.ButtonSuccessBorderBrush", success);
        SetBrush(resources, "Theme.ButtonSuccessForegroundBrush", Colors.White);
        SetBrush(resources, "Theme.ButtonSuccessHoverBackgroundBrush", Mix(success, isDark ? Colors.White : Colors.Black, isDark ? 0.08 : 0.1));
        SetBrush(resources, "Theme.InputBackgroundBrush", surface);
        SetBrush(resources, "Theme.InputBorderBrush", border);
        SetBrush(resources, "Theme.InputHoverBackgroundBrush", Mix(surface, isDark ? Colors.White : Colors.Black, isDark ? 0.05 : 0.02));
        SetBrush(resources, "Theme.InputFocusBorderBrush", Mix(accent, isDark ? Colors.White : Colors.Black, isDark ? 0.1 : 0.05));
        SetBrush(resources, "Theme.ComboItemHoverBackgroundBrush", Mix(accent, panel, isDark ? 0.18 : 0.1));
        SetBrush(resources, "Theme.ComboItemSelectedBackgroundBrush", Mix(accent, panel, isDark ? 0.24 : 0.15));
        SetBrush(resources, "Theme.CheckBoxBorderBrush", Mix(border, secondaryText, isDark ? 0.1 : 0.05));
        SetBrush(resources, "Theme.CheckBoxHoverBorderBrush", Mix(accent, panel, isDark ? 0.18 : 0.35));
        SetBrush(resources, "Theme.CheckBoxHoverBackgroundBrush", Mix(accent, panel, isDark ? 0.12 : 0.06));
        SetBrush(resources, "Theme.SwitchTrackBrush", surface);
        SetBrush(resources, "Theme.SwitchBorderBrush", Mix(border, surface, isDark ? 0.1 : 0.0));
        SetBrush(resources, "Theme.SwitchThumbBrush", Colors.White);
        SetBrush(resources, "Theme.SwitchTrackOnBrush", Mix(success, accent, 0.22));
        SetBrush(resources, "Theme.WarningBrush", warning);
        SetBrush(resources, "Theme.WarningSoftBrush", Mix(warning, panel, isDark ? 0.18 : 0.1));
        SetBrush(resources, "Theme.StatusBadgeBackgroundBrush", Mix(surface, panel, isDark ? 0.35 : 0.5));
        SetBrush(resources, "Theme.StatusBadgeBorderBrush", Mix(border, panel, isDark ? 0.4 : 0.2));
        SetBrush(resources, "Theme.StatusBadgeIconBrush", statusBadgeIconColor);
        SetBrush(resources, "Theme.ScrollBarTrackBrush", Mix(panel, surface, isDark ? 0.36 : 0.12));
        SetBrush(resources, "Theme.ScrollBarThumbBrush", Mix(border, primaryText, isDark ? 0.3 : 0.18));
        SetBrush(resources, "Theme.ScrollBarThumbHoverBrush", Mix(accent, primaryText, isDark ? 0.34 : 0.24));
        SetBrush(resources, "Theme.ScrollBarThumbPressedBrush", Mix(accent, primaryText, isDark ? 0.52 : 0.42));
        SetBrush(resources, "Theme.DisabledTextBrush", Mix(secondaryText, panel, isDark ? 0.5 : 0.4));
        SetBrush(resources, "Theme.LinkBrush", accent);
        SetBrush(resources, "Theme.SelectionOutlineBrush", accent);
        SetBrush(resources, "Theme.OverlayBackdropBrush", Color.FromArgb(isDark ? (byte)0x88 : (byte)0x66, windowBackground.R, windowBackground.G, windowBackground.B));
        SetBrush(resources, "Theme.OverlayCardBackgroundBrush", Mix(surface, panel, isDark ? 0.25 : 0.08));
        SetBrush(resources, "Theme.OverlayCardBorderBrush", Mix(border, panel, isDark ? 0.3 : 0.12));
        SetBrush(resources, "Theme.OverlayTrackBrush", Mix(border, panel, isDark ? 0.25 : 0.15));
        SetBrush(resources, "Theme.OverlayAccentBrush", accent);
        return resources;
    }

    private static void SetBrush(ResourceDictionary dictionary, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        dictionary[key] = brush;
    }

    private static Color ParseColor(string value)
    {
        var normalized = ThemePalette.NormalizeColor(value, "#FFFFFFFF");
        var hex = normalized[1..];
        var a = Convert.ToByte(hex[..2], 16);
        var r = Convert.ToByte(hex.Substring(2, 2), 16);
        var g = Convert.ToByte(hex.Substring(4, 2), 16);
        var b = Convert.ToByte(hex.Substring(6, 2), 16);
        return Color.FromArgb(a, r, g, b);
    }

    private static Color Mix(Color baseColor, Color overlayColor, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            (byte)Math.Round(baseColor.A + ((overlayColor.A - baseColor.A) * amount)),
            (byte)Math.Round(baseColor.R + ((overlayColor.R - baseColor.R) * amount)),
            (byte)Math.Round(baseColor.G + ((overlayColor.G - baseColor.G) * amount)),
            (byte)Math.Round(baseColor.B + ((overlayColor.B - baseColor.B) * amount)));
    }
}
