using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using BossKey.App.Localization;
using BossKey.App.Services;

namespace BossKey.App;

public partial class AboutWindow : Window
{
    private const string AuthorName = "MayFlyOvO";
    private const string GithubUrl = "https://github.com/MayFlyOvO/BossKey";
    private const string MaterialDesignIconsUrl = "https://github.com/google/material-design-icons";
    private const string MaterialDesignIconsDisplay = "Material Icons Round (Apache 2.0)";

    private readonly string _languageCode;

    public AboutWindow(string languageCode)
    {
        InitializeComponent();
        _languageCode = Localizer.NormalizeLanguage(languageCode);
        ApplyLocalization();
        LoadBuildInfo();
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void GithubLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri is not null)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }

        e.Handled = true;
    }

    private void ApplyLocalization()
    {
        Title = Localizer.T("About.Title", _languageCode);
        HeaderTextBlock.Text = Localizer.T("About.Title", _languageCode);
        DescriptionTextBlock.Text = Localizer.T("About.Description", _languageCode);
        VersionLabelTextBlock.Text = Localizer.T("About.CurrentVersion", _languageCode);
        PackageLabelTextBlock.Text = Localizer.T("About.PackageType", _languageCode);
        AuthorLabelTextBlock.Text = Localizer.T("About.Author", _languageCode);
        GithubLabelTextBlock.Text = Localizer.T("About.Github", _languageCode);
        ThirdPartyLabelTextBlock.Text = Localizer.T("About.ThirdParty", _languageCode);
        GithubLink.NavigateUri = new Uri(GithubUrl, UriKind.Absolute);
        GithubLinkRun.Text = GithubUrl;
        ThirdPartyLink.NavigateUri = new Uri(MaterialDesignIconsUrl, UriKind.Absolute);
        ThirdPartyLinkRun.Text = MaterialDesignIconsDisplay;
    }

    private void LoadBuildInfo()
    {
        VersionValueTextBlock.Text = GetDisplayVersion();
        PackageValueTextBlock.Text = GetPackageTypeDisplay();
        AuthorValueTextBlock.Text = AuthorName;
    }

    private string GetPackageTypeDisplay()
    {
        var metadataType = GetAssemblyMetadataValue("UpdateChannel");
        if (string.Equals(metadataType, "singlefile", StringComparison.OrdinalIgnoreCase))
        {
            return Localizer.T("About.PackageSingleFile", _languageCode);
        }

        if (string.Equals(metadataType, "installer", StringComparison.OrdinalIgnoreCase))
        {
            return Localizer.T("About.PackageInstaller", _languageCode);
        }

        if (AppContext.GetData("IsSingleFile") is bool isSingleFile && isSingleFile)
        {
            return Localizer.T("About.PackageSingleFile", _languageCode);
        }

        return Localizer.T("About.PackageInstaller", _languageCode);
    }

    private static string? GetAssemblyMetadataValue(string key)
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly is null)
        {
            return null;
        }

        foreach (var attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (string.Equals(attribute.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return attribute.Value;
            }
        }

        return null;
    }

    private static string GetDisplayVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly is null)
        {
            return "Unknown";
        }

        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var plusIndex = informationalVersion.IndexOf('+');
            return plusIndex > 0 ? informationalVersion[..plusIndex] : informationalVersion;
        }

        var version = assembly.GetName().Version;
        if (version is null)
        {
            return "Unknown";
        }

        var build = version.Build < 0 ? 0 : version.Build;
        return $"{version.Major}.{version.Minor}.{build}";
    }
}
