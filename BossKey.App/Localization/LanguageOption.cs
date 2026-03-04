namespace BossKey.App.Localization;

public sealed record LanguageOption(string Code, string DisplayName, string Version)
{
    public string DisplayText => string.IsNullOrWhiteSpace(Version)
        ? DisplayName
        : $"{DisplayName} ({Version})";

    public override string ToString()
    {
        return DisplayText;
    }
}
