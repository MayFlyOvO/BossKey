using System.Text.RegularExpressions;

namespace BossKey.Core.Models;

public sealed class TargetGroupConfig
{
    public const string DefaultGroupId = "default";
    public const string DefaultGroupIconColor = "#FFF5F6F8";
    public const string StandardGroupIconColor = "#FFF0F5FB";
    private static readonly Regex HexColorPattern = new("^#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.Compiled);
    private static readonly string[] LightIconColors =
    [
        "#FFFFE4E6",
        "#FFFFEDD5",
        "#FFFDECC8",
        "#FFECFCCB",
        "#FFDCFCE7",
        "#FFE0F2FE",
        "#FFE0F7FA",
        "#FFEDE9FE",
        "#FFF3E8FF",
        "#FFFCE7F3"
    ];

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string IconColor { get; set; } = StandardGroupIconColor;
    public HotkeyBinding HideHotkey { get; set; } = new();
    public HotkeyBinding ShowHotkey { get; set; } = new();
    public bool IsCollapsed { get; set; }
    public List<TargetAppConfig> Targets { get; set; } = [];

    public static string CreateRandomLightIconColor()
    {
        return LightIconColors[Random.Shared.Next(LightIconColors.Length)];
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
}
