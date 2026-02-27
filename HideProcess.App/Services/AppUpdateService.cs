using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace HideProcess.App.Services;

public sealed class AppUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repo;

    public AppUpdateService(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HideProcess", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        var requestUri = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return UpdateCheckResult.Failed($"HTTP {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        var tagName = GetString(root, "tag_name");
        if (!TryParseVersion(tagName, out var latestVersion))
        {
            return UpdateCheckResult.Failed("Invalid release tag.");
        }

        var releasePageUrl = GetString(root, "html_url");
        var releaseNotes = GetString(root, "body");
        var downloadUrl = ResolveInstallerDownloadUrl(root);

        if (latestVersion <= currentVersion)
        {
            return UpdateCheckResult.NoUpdate(currentVersion, latestVersion);
        }

        return UpdateCheckResult.Available(
            currentVersion,
            latestVersion,
            tagName ?? latestVersion.ToString(),
            releasePageUrl,
            releaseNotes,
            downloadUrl);
    }

    public async Task<string> DownloadInstallerAsync(string downloadUrl, string versionTag, CancellationToken cancellationToken = default)
    {
        var updatesDir = Path.Combine(Path.GetTempPath(), "HideProcess", "Updates");
        Directory.CreateDirectory(updatesDir);

        var extension = ".exe";
        if (Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
        {
            var name = Path.GetFileName(uri.LocalPath);
            var ext = Path.GetExtension(name);
            if (!string.IsNullOrWhiteSpace(ext))
            {
                extension = ext;
            }
        }

        var safeTag = SanitizeFileName(versionTag);
        var filePath = Path.Combine(updatesDir, $"HideProcess-Setup-{safeTag}{extension}");

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(filePath);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        return filePath;
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static bool TryParseVersion(string? rawTag, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(rawTag))
        {
            return false;
        }

        var normalized = rawTag.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            normalized = normalized[..suffixIndex];
        }

        if (Version.TryParse(normalized, out var parsed) && parsed is not null)
        {
            version = parsed;
            return true;
        }

        return false;
    }

    private static string? ResolveInstallerDownloadUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? fallbackExe = null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = GetString(asset, "name");
            var url = GetString(asset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                fallbackExe ??= url;
                if (name.Contains("setup", StringComparison.OrdinalIgnoreCase))
                {
                    return url;
                }
            }
        }

        return fallbackExe;
    }

    private static string SanitizeFileName(string input)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = input.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }
}

public enum UpdateCheckStatus
{
    NoUpdate,
    UpdateAvailable,
    Failed
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version CurrentVersion,
    Version LatestVersion,
    string ReleaseTag,
    string? ReleasePageUrl,
    string? ReleaseNotes,
    string? InstallerDownloadUrl,
    string? ErrorMessage)
{
    public static UpdateCheckResult NoUpdate(Version currentVersion, Version latestVersion)
    {
        return new UpdateCheckResult(
            UpdateCheckStatus.NoUpdate,
            currentVersion,
            latestVersion,
            latestVersion.ToString(),
            null,
            null,
            null,
            null);
    }

    public static UpdateCheckResult Available(
        Version currentVersion,
        Version latestVersion,
        string releaseTag,
        string? releasePageUrl,
        string? releaseNotes,
        string? installerDownloadUrl)
    {
        return new UpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            currentVersion,
            latestVersion,
            releaseTag,
            releasePageUrl,
            releaseNotes,
            installerDownloadUrl,
            null);
    }

    public static UpdateCheckResult Failed(string errorMessage)
    {
        return new UpdateCheckResult(
            UpdateCheckStatus.Failed,
            new Version(0, 0, 0, 0),
            new Version(0, 0, 0, 0),
            "n/a",
            null,
            null,
            null,
            errorMessage);
    }
}
