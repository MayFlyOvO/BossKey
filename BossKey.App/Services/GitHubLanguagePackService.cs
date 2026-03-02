using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Net;
using System.Text.Json;
using BossKey.App.Localization;

namespace BossKey.App.Services;

public sealed class GitHubLanguagePackService
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _branch;
    private readonly string _basePath;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GitHubLanguagePackService(string owner, string repo, string branch, string basePath)
    {
        _owner = owner;
        _repo = repo;
        _branch = branch;
        _basePath = basePath.Trim('/').Trim();
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BossKey", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public Task<LanguageManifest> FetchCatalogAsync(CancellationToken cancellationToken = default)
    {
        return FetchManifestAsync(cancellationToken);
    }

    public async Task<bool> DownloadLanguageIfNeededAsync(
        LanguageManifestEntry entry,
        string targetDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(targetDirectory);
        if (string.IsNullOrWhiteSpace(entry.Code)
            || string.IsNullOrWhiteSpace(entry.RelativePath)
            || string.Equals(entry.Code, Localizer.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var localPath = Path.Combine(targetDirectory, $"{SanitizeFileName(entry.Code)}.json");
        if (!IsDownloadRequired(localPath, entry.Version))
        {
            return false;
        }

        var packJson = await DownloadLanguagePackJsonAsync(entry, cancellationToken).ConfigureAwait(false);
        var pack = JsonSerializer.Deserialize<LanguagePack>(packJson, _jsonSerializerOptions)
            ?? throw new InvalidDataException($"Language pack '{entry.Code}' is invalid.");
        if (!string.Equals(pack.Code, entry.Code, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Language pack code mismatch for '{entry.Code}'.");
        }

        var tempPath = $"{localPath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, packJson);
        File.Move(tempPath, localPath, overwrite: true);
        return true;
    }

    private async Task<LanguageManifest> FetchManifestAsync(CancellationToken cancellationToken)
    {
        var manifestUrl = BuildRawUrl("manifest.json");
        var manifestJson = await DownloadStringAsync(manifestUrl, allowNotFound: true, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(manifestJson))
        {
            return DeserializeManifest(manifestJson);
        }

        return await BuildManifestFromDirectoryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> DownloadLanguagePackJsonAsync(LanguageManifestEntry entry, CancellationToken cancellationToken)
    {
        var downloadUrl = !string.IsNullOrWhiteSpace(entry.DownloadUrl)
            ? entry.DownloadUrl
            : BuildRawUrl(entry.RelativePath);
        return await DownloadStringAsync(downloadUrl, allowNotFound: false, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException($"Language pack '{entry.Code}' is missing.");
    }

    private async Task<LanguageManifest> BuildManifestFromDirectoryAsync(CancellationToken cancellationToken)
    {
        var directoryUrl = BuildContentsApiUrl();
        var directoryJson = await DownloadStringAsync(directoryUrl, allowNotFound: true, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(directoryJson))
        {
            return new LanguageManifest();
        }

        var items = JsonSerializer.Deserialize<List<GitHubContentItem>>(directoryJson, _jsonSerializerOptions) ?? [];
        var manifest = new LanguageManifest();
        foreach (var item in items.Where(static item =>
                     string.Equals(item.Type, "file", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(item.Name)
                     && item.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(item.Name, "manifest.json", StringComparison.OrdinalIgnoreCase)))
        {
            var code = Path.GetFileNameWithoutExtension(item.Name);
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            manifest.Languages.Add(new LanguageManifestEntry
            {
                Code = code,
                DisplayName = GetFallbackDisplayName(code),
                Version = string.Empty,
                RelativePath = item.Name,
                DownloadUrl = !string.IsNullOrWhiteSpace(item.DownloadUrl)
                    ? item.DownloadUrl
                    : BuildRawUrl(item.Name)
            });
        }

        return manifest;
    }

    private LanguageManifest DeserializeManifest(string manifestJson)
    {
        var manifest = JsonSerializer.Deserialize<LanguageManifest>(manifestJson, _jsonSerializerOptions)
            ?? throw new InvalidDataException("Language manifest is invalid.");
        manifest.Languages ??= [];
        foreach (var entry in manifest.Languages)
        {
            entry.Code = entry.Code?.Trim() ?? string.Empty;
            entry.DisplayName = entry.DisplayName?.Trim() ?? string.Empty;
            entry.Version = entry.Version?.Trim() ?? string.Empty;
            entry.RelativePath = entry.RelativePath?.Replace('\\', '/').TrimStart('/') ?? string.Empty;
        }

        return manifest;
    }

    private async Task<string?> DownloadStringAsync(string url, bool allowNotFound, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private string BuildRawUrl(string relativePath)
    {
        var normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        return $"https://raw.githubusercontent.com/{_owner}/{_repo}/{_branch}/{_basePath}/{normalizedPath}";
    }

    private string BuildContentsApiUrl()
    {
        return $"https://api.github.com/repos/{_owner}/{_repo}/contents/{_basePath}?ref={Uri.EscapeDataString(_branch)}";
    }

    private static bool IsDownloadRequired(string localPath, string remoteVersion)
    {
        if (!File.Exists(localPath))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(remoteVersion))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(localPath);
            var localPack = JsonSerializer.Deserialize<LanguagePack>(json);
            if (localPack is null || string.IsNullOrWhiteSpace(localPack.Version))
            {
                return true;
            }

            if (Version.TryParse(localPack.Version, out var localParsedVersion)
                && Version.TryParse(remoteVersion, out var remoteParsedVersion))
            {
                return remoteParsedVersion > localParsedVersion;
            }

            return !string.Equals(localPack.Version, remoteVersion, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
    }

    private static string GetFallbackDisplayName(string languageCode)
    {
        try
        {
            return CultureInfo.GetCultureInfo(languageCode).NativeName;
        }
        catch
        {
            return languageCode;
        }
    }

    private sealed class GitHubContentItem
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
