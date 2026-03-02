using System.Globalization;
using System.IO;
using System.Text.Json;
using BossKey.App.Services;

namespace BossKey.App.Localization;

public static class Localizer
{
    private const string DefaultLanguage = "en-US";
    private const string EmbeddedDefaultLanguageResourceName = "BossKey.App.Localization.Resources.en-US.json";
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly GitHubLanguagePackService RemoteLanguageService = new(
        owner: "MayFlyOvO",
        repo: "BossKey",
        branch: "main",
        basePath: "language-packs");
    private static readonly SemaphoreSlim RemoteSyncSemaphore = new(1, 1);

    private static Dictionary<string, LanguagePack> _installedPacks = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, LanguageManifestEntry> _remoteCatalog = new(StringComparer.OrdinalIgnoreCase);
    private static bool _isInitialized;
    private static string _requestedLanguage = DefaultLanguage;
    private static string _currentLanguage = DefaultLanguage;

    public static string DefaultLanguageCode => DefaultLanguage;

    public static IReadOnlyList<LanguageOption> SupportedLanguages => GetSupportedLanguages();

    public static string CurrentLanguage
    {
        get
        {
            EnsureInitialized();
            lock (Gate)
            {
                return _currentLanguage;
            }
        }
    }

    public static event EventHandler? LanguageChanged;

    public static event EventHandler? SupportedLanguagesChanged;

    public static IReadOnlyList<LanguageOption> GetSupportedLanguages(string? includeLanguageCode = null)
    {
        EnsureInitialized();
        lock (Gate)
        {
            return BuildSupportedLanguagesNoLock(includeLanguageCode);
        }
    }

    public static bool HasLanguage(string? languageCode)
    {
        EnsureInitialized();
        lock (Gate)
        {
            return TryFindInstalledLanguageCodeNoLock(languageCode, out _);
        }
    }

    public static string NormalizeLanguage(string? languageCode)
    {
        EnsureInitialized();
        lock (Gate)
        {
            return ResolveLanguageNoLock(languageCode);
        }
    }

    public static string NormalizeStoredLanguage(string? languageCode)
    {
        EnsureInitialized();
        lock (Gate)
        {
            return NormalizeStoredLanguageNoLock(languageCode);
        }
    }

    public static void SetLanguage(string? languageCode)
    {
        EnsureInitialized();

        var shouldRaise = false;
        lock (Gate)
        {
            _requestedLanguage = NormalizeStoredLanguageNoLock(languageCode);
            var resolved = ResolveLanguageNoLock(_requestedLanguage);
            if (string.Equals(_currentLanguage, resolved, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _currentLanguage = resolved;
            shouldRaise = true;
        }

        if (shouldRaise)
        {
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static string T(string key)
    {
        return T(key, CurrentLanguage);
    }

    public static string T(string key, string? languageCode)
    {
        EnsureInitialized();
        lock (Gate)
        {
            var resolved = ResolveLanguageNoLock(languageCode);
            if (_installedPacks.TryGetValue(resolved, out var selectedPack)
                && selectedPack.Translations.TryGetValue(key, out var selectedValue))
            {
                return selectedValue;
            }

            if (_installedPacks.TryGetValue(DefaultLanguage, out var defaultPack)
                && defaultPack.Translations.TryGetValue(key, out var defaultValue))
            {
                return defaultValue;
            }

            return key;
        }
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, T(key), args);
    }

    public static Task<LanguageSyncResult> SyncLanguagePacksAsync(CancellationToken cancellationToken = default)
    {
        return RefreshRemoteCatalogAsync(cancellationToken);
    }

    public static async Task<LanguageSyncResult> RefreshRemoteCatalogAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await RemoteSyncSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var manifest = await RemoteLanguageService.FetchCatalogAsync(cancellationToken).ConfigureAwait(false);
            return ApplyRemoteCatalogUpdate(manifest, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            return LanguageSyncResult.Failed(ex.Message);
        }
        finally
        {
            RemoteSyncSemaphore.Release();
        }
    }

    public static async Task<LanguageSyncResult> EnsureLanguageAvailableAsync(
        string? languageCode,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var normalizedCode = NormalizeStoredLanguage(languageCode);
        if (string.Equals(normalizedCode, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return LanguageSyncResult.Success(new LanguageManifest(), Array.Empty<string>());
        }

        await RemoteSyncSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LanguageManifest manifest;
            LanguageManifestEntry? remoteEntry;

            lock (Gate)
            {
                manifest = new LanguageManifest
                {
                    Languages = _remoteCatalog.Values.ToList()
                };
                remoteEntry = _remoteCatalog.Values.FirstOrDefault(entry =>
                    string.Equals(entry.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
            }

            if (remoteEntry is null)
            {
                manifest = await RemoteLanguageService.FetchCatalogAsync(cancellationToken).ConfigureAwait(false);
                ApplyRemoteCatalogUpdate(manifest, Array.Empty<string>());
                lock (Gate)
                {
                    remoteEntry = _remoteCatalog.Values.FirstOrDefault(entry =>
                        string.Equals(entry.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (remoteEntry is null)
            {
                return HasLanguage(normalizedCode)
                    ? LanguageSyncResult.Success(manifest, Array.Empty<string>())
                    : LanguageSyncResult.Failed($"Language '{normalizedCode}' was not found.");
            }

            var downloaded = await RemoteLanguageService.DownloadLanguageIfNeededAsync(
                remoteEntry,
                GetLanguageDirectoryPath(),
                cancellationToken).ConfigureAwait(false);

            return ApplyRemoteCatalogUpdate(
                manifest,
                downloaded ? new[] { normalizedCode } : Array.Empty<string>());
        }
        catch (Exception ex)
        {
            return LanguageSyncResult.Failed(ex.Message);
        }
        finally
        {
            RemoteSyncSemaphore.Release();
        }
    }

    private static void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        lock (Gate)
        {
            if (_isInitialized)
            {
                return;
            }

            ReloadInstalledPacksNoLock();
            _currentLanguage = ResolveLanguageNoLock(_requestedLanguage);
            _isInitialized = true;
        }
    }

    private static void ReloadInstalledPacksNoLock()
    {
        var packs = new Dictionary<string, LanguagePack>(StringComparer.OrdinalIgnoreCase);
        var defaultPack = LoadEmbeddedDefaultPack();
        packs[defaultPack.Code] = defaultPack;

        var languageDirectory = GetLanguageDirectoryPath();
        if (!Directory.Exists(languageDirectory))
        {
            _installedPacks = packs;
            return;
        }

        foreach (var path in Directory.EnumerateFiles(languageDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var pack = DeserializeLanguagePack(File.ReadAllText(path));
                if (string.IsNullOrWhiteSpace(pack.Code))
                {
                    continue;
                }

                pack.Code = NormalizeStoredLanguageNoLock(pack.Code);
                if (string.Equals(pack.Code, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                packs[pack.Code] = pack;
            }
            catch
            {
            }
        }

        _installedPacks = packs;
    }

    private static LanguagePack LoadEmbeddedDefaultPack()
    {
        var assembly = typeof(Localizer).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedDefaultLanguageResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded language resource '{EmbeddedDefaultLanguageResourceName}' was not found in {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var pack = DeserializeLanguagePack(json);
        pack.Code = DefaultLanguage;
        if (string.IsNullOrWhiteSpace(pack.DisplayName))
        {
            pack.DisplayName = "English";
        }

        return pack;
    }

    private static LanguagePack DeserializeLanguagePack(string json)
    {
        var pack = JsonSerializer.Deserialize<LanguagePack>(json, JsonSerializerOptions)
            ?? throw new InvalidDataException("Invalid language pack.");
        pack.Code = string.IsNullOrWhiteSpace(pack.Code) ? DefaultLanguage : pack.Code.Trim();
        pack.DisplayName = string.IsNullOrWhiteSpace(pack.DisplayName) ? pack.Code : pack.DisplayName.Trim();
        pack.Version = string.IsNullOrWhiteSpace(pack.Version) ? "1.0.0" : pack.Version.Trim();
        pack.Translations = pack.Translations
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value ?? string.Empty, StringComparer.Ordinal);
        return pack;
    }

    private static List<LanguageOption> BuildSupportedLanguagesNoLock(string? includeLanguageCode)
    {
        var options = new List<LanguageOption>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pack in _installedPacks.Values
                     .OrderBy(static pack => string.Equals(pack.Code, DefaultLanguage, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(static pack => pack.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            options.Add(new LanguageOption(pack.Code, pack.DisplayName));
            seenCodes.Add(pack.Code);
        }

        foreach (var entry in _remoteCatalog.Values
                     .OrderBy(static entry => string.Equals(entry.Code, DefaultLanguage, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(static entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            if (seenCodes.Add(entry.Code))
            {
                options.Add(new LanguageOption(entry.Code, entry.DisplayName));
            }
        }

        var normalizedIncludedCode = NormalizeStoredLanguageNoLock(includeLanguageCode);
        if (seenCodes.Add(normalizedIncludedCode))
        {
            options.Add(new LanguageOption(normalizedIncludedCode, GetLanguageDisplayNameNoLock(normalizedIncludedCode)));
        }

        return options;
    }

    private static string BuildLanguageCodeSnapshotNoLock()
    {
        return string.Join(
            "|",
            BuildSupportedLanguagesNoLock(includeLanguageCode: null)
                .Select(static option => $"{option.Code}:{option.DisplayName}"));
    }

    private static string GetLanguageDisplayNameNoLock(string languageCode)
    {
        if (_installedPacks.TryGetValue(languageCode, out var installedPack))
        {
            return installedPack.DisplayName;
        }

        if (_remoteCatalog.TryGetValue(languageCode, out var remotePack))
        {
            return remotePack.DisplayName;
        }

        return languageCode;
    }

    private static string ResolveLanguageNoLock(string? languageCode)
    {
        return TryFindInstalledLanguageCodeNoLock(languageCode, out var matchedCode)
            ? matchedCode
            : DefaultLanguage;
    }

    private static string NormalizeStoredLanguageNoLock(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return DefaultLanguage;
        }

        var trimmed = languageCode.Trim();
        if (TryFindInstalledLanguageCodeNoLock(trimmed, out var installedCode))
        {
            return installedCode;
        }

        if (TryFindRemoteLanguageCodeNoLock(trimmed, out var remoteCode))
        {
            return remoteCode;
        }

        return trimmed;
    }

    private static bool TryFindInstalledLanguageCodeNoLock(string? languageCode, out string matchedCode)
    {
        matchedCode = DefaultLanguage;
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return false;
        }

        matchedCode = _installedPacks.Keys.FirstOrDefault(code =>
            string.Equals(code, languageCode, StringComparison.OrdinalIgnoreCase)) ?? matchedCode;
        return _installedPacks.ContainsKey(matchedCode)
               && string.Equals(matchedCode, languageCode, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryFindRemoteLanguageCodeNoLock(string? languageCode, out string matchedCode)
    {
        matchedCode = DefaultLanguage;
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return false;
        }

        matchedCode = _remoteCatalog.Keys.FirstOrDefault(code =>
            string.Equals(code, languageCode, StringComparison.OrdinalIgnoreCase)) ?? matchedCode;
        return _remoteCatalog.ContainsKey(matchedCode)
               && string.Equals(matchedCode, languageCode, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLanguageDirectoryPath()
    {
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BossKey");
        return Path.Combine(appDataDirectory, "Languages");
    }

    private static LanguageSyncResult ApplyRemoteCatalogUpdate(
        LanguageManifest manifest,
        IReadOnlyList<string> downloadedLanguageCodes)
    {
        var shouldRaiseSupportedLanguagesChanged = false;
        var shouldRaiseLanguageChanged = false;

        lock (Gate)
        {
            var supportedBefore = BuildLanguageCodeSnapshotNoLock();
            var currentBefore = _currentLanguage;

            _remoteCatalog = manifest.Languages
                .Where(static entry => !string.IsNullOrWhiteSpace(entry.Code))
                .GroupBy(static entry => entry.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    static group => group.First().Code,
                    static group => group.Last(),
                    StringComparer.OrdinalIgnoreCase);

            ReloadInstalledPacksNoLock();
            var currentAfter = ResolveLanguageNoLock(_requestedLanguage);
            _currentLanguage = currentAfter;

            var supportedAfter = BuildLanguageCodeSnapshotNoLock();
            shouldRaiseSupportedLanguagesChanged = !string.Equals(supportedBefore, supportedAfter, StringComparison.Ordinal);
            shouldRaiseLanguageChanged =
                !string.Equals(currentBefore, currentAfter, StringComparison.OrdinalIgnoreCase)
                || downloadedLanguageCodes.Any(code =>
                    string.Equals(code, currentBefore, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(code, currentAfter, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(code, _requestedLanguage, StringComparison.OrdinalIgnoreCase));
        }

        if (shouldRaiseSupportedLanguagesChanged)
        {
            SupportedLanguagesChanged?.Invoke(null, EventArgs.Empty);
        }

        if (shouldRaiseLanguageChanged)
        {
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        return LanguageSyncResult.Success(manifest, downloadedLanguageCodes);
    }
}
