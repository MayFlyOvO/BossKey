using System.Text.Json.Serialization;

namespace BossKey.App.Localization;

public sealed class LanguagePack
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("translations")]
    public Dictionary<string, string> Translations { get; set; } = new(StringComparer.Ordinal);
}
