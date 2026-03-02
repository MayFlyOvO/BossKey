using System.Text.Json.Serialization;

namespace BossKey.App.Localization;

public sealed class LanguageManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("languages")]
    public List<LanguageManifestEntry> Languages { get; set; } = [];
}

public sealed class LanguageManifestEntry
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("file")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }
}
