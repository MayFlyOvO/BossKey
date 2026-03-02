namespace BossKey.App.Localization;

public sealed record LanguageSyncResult(
    bool Succeeded,
    LanguageManifest Manifest,
    IReadOnlyList<string> DownloadedLanguageCodes,
    string? ErrorMessage)
{
    public static LanguageSyncResult Success(LanguageManifest manifest, IReadOnlyList<string> downloadedLanguageCodes)
    {
        return new LanguageSyncResult(
            Succeeded: true,
            Manifest: manifest,
            DownloadedLanguageCodes: downloadedLanguageCodes,
            ErrorMessage: null);
    }

    public static LanguageSyncResult Failed(string errorMessage)
    {
        return new LanguageSyncResult(
            Succeeded: false,
            Manifest: new LanguageManifest(),
            DownloadedLanguageCodes: Array.Empty<string>(),
            ErrorMessage: errorMessage);
    }
}
