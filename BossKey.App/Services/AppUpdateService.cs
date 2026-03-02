using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;

namespace BossKey.App.Services;

public sealed class AppUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repo;
    private readonly UpdatePackageType _packageType;

    public AppUpdateService(string owner, string repo, UpdatePackageType packageType)
    {
        _owner = owner;
        _repo = repo;
        _packageType = packageType;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BossKey", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        var requestUri = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return await CheckForUpdatesByRedirectAsync(currentVersion, cancellationToken).ConfigureAwait(false);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return await CheckForUpdatesFromReleaseListAsync(currentVersion, cancellationToken).ConfigureAwait(false);
        }

        if (!response.IsSuccessStatusCode)
        {
            return UpdateCheckResult.Failed($"HTTP {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return BuildResultFromRelease(currentVersion, document.RootElement, _packageType);
    }

    private async Task<UpdateCheckResult> CheckForUpdatesFromReleaseListAsync(Version currentVersion, CancellationToken cancellationToken)
    {
        var requestUri = $"https://api.github.com/repos/{_owner}/{_repo}/releases?per_page=20";
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return await CheckForUpdatesByRedirectAsync(currentVersion, cancellationToken).ConfigureAwait(false);
        }

        if (!response.IsSuccessStatusCode)
        {
            return UpdateCheckResult.Failed($"HTTP {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return UpdateCheckResult.Failed("Unexpected releases payload.");
        }

        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (release.TryGetProperty("draft", out var draftElement) && draftElement.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            var candidate = BuildResultFromRelease(currentVersion, release, _packageType);
            if (candidate.Status is UpdateCheckStatus.UpdateAvailable or UpdateCheckStatus.NoUpdate)
            {
                return candidate;
            }
        }

        return UpdateCheckResult.NoUpdate(currentVersion, currentVersion);
    }

    private async Task<UpdateCheckResult> CheckForUpdatesByRedirectAsync(Version currentVersion, CancellationToken cancellationToken)
    {
        var requestUri = $"https://github.com/{_owner}/{_repo}/releases/latest";
        using var response = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return UpdateCheckResult.Failed($"HTTP {(int)response.StatusCode}");
        }

        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is null)
        {
            return UpdateCheckResult.Failed("Cannot resolve latest release URL.");
        }

        var releasePageUrl = finalUri.ToString();
        var tagName = ExtractTagFromReleaseUrl(finalUri);
        if (!TryParseVersion(tagName, out var latestVersion))
        {
            return UpdateCheckResult.Failed("Invalid release tag.");
        }

        if (latestVersion <= currentVersion)
        {
            return UpdateCheckResult.NoUpdate(currentVersion, latestVersion);
        }

        return UpdateCheckResult.Available(
            currentVersion,
            latestVersion,
            tagName ?? latestVersion.ToString(),
            releasePageUrl,
            null,
            BuildDeterministicDownloadUrl(tagName ?? latestVersion.ToString(), _packageType));
    }

    private static UpdateCheckResult BuildResultFromRelease(Version currentVersion, JsonElement release, UpdatePackageType packageType)
    {
        var tagName = GetString(release, "tag_name");
        if (!TryParseVersion(tagName, out var latestVersion))
        {
            return UpdateCheckResult.Failed("Invalid release tag.");
        }

        var releasePageUrl = GetString(release, "html_url");
        var releaseNotes = GetString(release, "body");
        var downloadUrl = ResolveDownloadUrl(release, packageType);

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

    public async Task<string> DownloadInstallerAsync(
        string downloadUrl,
        string versionTag,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var updatesDir = Path.Combine(Path.GetTempPath(), "BossKey", "Updates");
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
        var packageName = _packageType == UpdatePackageType.SingleFile
            ? "BossKey-SingleFile"
            : "BossKey-Setup";
        var filePath = Path.Combine(updatesDir, $"{packageName}-{safeTag}{extension}");

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(filePath);
        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;
        progress?.Report(0d);
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalRead += bytesRead;
            if (totalBytes is > 0)
            {
                progress?.Report(Math.Clamp((double)totalRead / totalBytes.Value, 0d, 1d));
            }
        }

        progress?.Report(1d);
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

    private static string? ExtractTagFromReleaseUrl(Uri uri)
    {
        var marker = "/releases/tag/";
        var path = uri.AbsolutePath;
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var tagPart = path[(index + marker.Length)..];
        if (string.IsNullOrWhiteSpace(tagPart))
        {
            return null;
        }

        return Uri.UnescapeDataString(tagPart);
    }

    private string BuildDeterministicDownloadUrl(string tagName, UpdatePackageType packageType)
    {
        var assetName = packageType == UpdatePackageType.SingleFile
            ? "BossKey-SingleFile.exe"
            : "BossKey-Setup.exe";
        var encodedTag = Uri.EscapeDataString(tagName);
        return $"https://github.com/{_owner}/{_repo}/releases/download/{encodedTag}/{assetName}";
    }

    private static string? ResolveDownloadUrl(JsonElement root, UpdatePackageType packageType)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? installerAsset = null;
        string? singleFileAsset = null;
        string? fallbackExe = null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = GetString(asset, "name");
            var url = GetString(asset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            fallbackExe ??= url;
            if (IsInstallerAsset(name))
            {
                installerAsset ??= url;
                continue;
            }

            singleFileAsset ??= url;
        }

        return packageType switch
        {
            UpdatePackageType.SingleFile => singleFileAsset ?? installerAsset ?? fallbackExe,
            _ => installerAsset ?? singleFileAsset ?? fallbackExe
        };
    }

    private static bool IsInstallerAsset(string assetName)
    {
        return assetName.Contains("setup", StringComparison.OrdinalIgnoreCase)
               || assetName.Contains("installer", StringComparison.OrdinalIgnoreCase);
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

public enum UpdatePackageType
{
    Installer,
    SingleFile
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
