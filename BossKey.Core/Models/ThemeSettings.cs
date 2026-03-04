namespace BossKey.Core.Models;

public static class ThemeModes
{
    public const string Light = "light";
    public const string Dark = "dark";

    public static string Normalize(string? mode)
    {
        return string.Equals(mode, Dark, StringComparison.OrdinalIgnoreCase)
            ? Dark
            : Light;
    }
}

public sealed class ThemeSettings
{
    public string ActiveMode { get; set; } = ThemeModes.Light;
    public ThemePalette LightPalette { get; set; } = ThemePalette.CreateDefaultLight();
    public ThemePalette DarkPalette { get; set; } = ThemePalette.CreateDefaultDark();

    public ThemeSettings Clone()
    {
        return new ThemeSettings
        {
            ActiveMode = ActiveMode,
            LightPalette = LightPalette.Clone(),
            DarkPalette = DarkPalette.Clone()
        };
    }

    public ThemePalette GetActivePalette()
    {
        return string.Equals(ThemeModes.Normalize(ActiveMode), ThemeModes.Dark, StringComparison.Ordinal)
            ? DarkPalette
            : LightPalette;
    }

    public ThemePalette GetPalette(string mode)
    {
        return string.Equals(ThemeModes.Normalize(mode), ThemeModes.Dark, StringComparison.Ordinal)
            ? DarkPalette
            : LightPalette;
    }

    public static ThemeSettings CreateDefault()
    {
        return new ThemeSettings
        {
            ActiveMode = ThemeModes.Light,
            LightPalette = ThemePalette.CreateDefaultLight(),
            DarkPalette = ThemePalette.CreateDefaultDark()
        };
    }

    public static ThemeSettings Normalize(ThemeSettings? settings)
    {
        var defaults = CreateDefault();
        if (settings is null)
        {
            return defaults;
        }

        return new ThemeSettings
        {
            ActiveMode = ThemeModes.Normalize(settings.ActiveMode),
            LightPalette = ThemePalette.Normalize(settings.LightPalette, defaults.LightPalette),
            DarkPalette = ThemePalette.Normalize(settings.DarkPalette, defaults.DarkPalette)
        };
    }
}
