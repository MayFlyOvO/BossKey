using System.Windows;
using System.Windows.Input;
using HideProcess.App.Localization;
using HideProcess.Core.Models;
using HideProcess.Core.Services;
using Win32 = Microsoft.Win32;

namespace HideProcess.App;

public partial class SettingsWindow : Window
{
    private static readonly Dictionary<string, string> ReservedHotkeys = new()
    {
        [SerializeKeys([0x12, 0x09])] = "Alt + Tab",
        [SerializeKeys([0x12, 0x1B])] = "Alt + Esc",
        [SerializeKeys([0x11, 0x1B])] = "Ctrl + Esc",
        [SerializeKeys([0x11, 0x10, 0x1B])] = "Ctrl + Shift + Esc",
        [SerializeKeys([0x5B, 0x44])] = "Win + D",
        [SerializeKeys([0x5B, 0x4C])] = "Win + L",
        [SerializeKeys([0x5B, 0x09])] = "Win + Tab"
    };

    private readonly JsonSettingsStore _settingsStore = new();
    private AppSettings _workingCopy;
    private string _previewLanguage;

    public AppSettings UpdatedSettings { get; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _workingCopy = CloneSettings(settings);
        UpdatedSettings = CloneSettings(settings);
        _previewLanguage = Localizer.NormalizeLanguage(settings.Language);

        LanguageComboBox.ItemsSource = Localizer.SupportedLanguages;
        SyncControlsFromWorkingCopy();
        ApplyLocalization();
        UpdateHotkeyPreview();
        UpdateHotkeyConflictWarning();
    }

    private void SetHideHotkeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new HotkeyCaptureWindow(
            Localizer.T("Hotkey.TitleHide", _previewLanguage),
            _previewLanguage,
            _workingCopy.HideHotkey)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _workingCopy.HideHotkey = dialog.CapturedBinding;
        UpdateHotkeyPreview();
        UpdateHotkeyConflictWarning();
    }

    private void SetShowHotkeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new HotkeyCaptureWindow(
            Localizer.T("Hotkey.TitleShow", _previewLanguage),
            _previewLanguage,
            _workingCopy.ShowHotkey)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _workingCopy.ShowHotkey = dialog.CapturedBinding;
        UpdateHotkeyPreview();
        UpdateHotkeyConflictWarning();
    }

    private void ImportButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Win32.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _workingCopy = CloneSettings(_settingsStore.ImportFromPath(dialog.FileName));
            _previewLanguage = Localizer.NormalizeLanguage(_workingCopy.Language);
            SyncControlsFromWorkingCopy();
            ApplyLocalization();
            UpdateHotkeyPreview();
            UpdateHotkeyConflictWarning();
            System.Windows.MessageBox.Show(
                this,
                Localizer.T("Settings.ImportSuccess", _previewLanguage),
                Localizer.T("Main.HintTitle", _previewLanguage),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                string.Format(Localizer.T("Settings.ImportFailed", _previewLanguage), ex.Message),
                Localizer.T("Main.InitErrorTitle", _previewLanguage),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ExportButton_OnClick(object sender, RoutedEventArgs e)
    {
        SyncControlsToWorkingCopy();

        var dialog = new Win32.SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"hideprocess-settings-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            AddExtension = true,
            DefaultExt = "json"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _settingsStore.ExportToPath(_workingCopy, dialog.FileName);
            System.Windows.MessageBox.Show(
                this,
                Localizer.T("Settings.ExportSuccess", _previewLanguage),
                Localizer.T("Main.HintTitle", _previewLanguage),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                string.Format(Localizer.T("Settings.ExportFailed", _previewLanguage), ex.Message),
                Localizer.T("Main.InitErrorTitle", _previewLanguage),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void LanguageComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is not LanguageOption option)
        {
            return;
        }

        _previewLanguage = option.Code;
        ApplyLocalization();
        UpdateHotkeyConflictWarning();
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        SyncControlsToWorkingCopy();
        CopyInto(_workingCopy, UpdatedSettings);
        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
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
        DialogResult = false;
        Close();
    }

    private void ApplyLocalization()
    {
        Title = Localizer.T("Settings.Title", _previewLanguage);
        HeaderTextBlock.Text = Localizer.T("Settings.Header", _previewLanguage);
        HotkeysSectionTextBlock.Text = Localizer.T("Settings.Hotkeys", _previewLanguage);
        HideHotkeyLabel.Text = Localizer.T("Settings.HideHotkey", _previewLanguage);
        ShowHotkeyLabel.Text = Localizer.T("Settings.ShowHotkey", _previewLanguage);
        SetHideHotkeyButtonTextBlock.Text = Localizer.T("Settings.SetHotkey", _previewLanguage);
        SetShowHotkeyButtonTextBlock.Text = Localizer.T("Settings.SetHotkey", _previewLanguage);
        GeneralSectionTextBlock.Text = Localizer.T("Settings.General", _previewLanguage);
        StartWithWindowsCheckBox.Content = Localizer.T("Settings.StartWithWindows", _previewLanguage);
        MinimizeToTrayCheckBox.Content = Localizer.T("Settings.MinimizeToTray", _previewLanguage);
        AutoCheckUpdatesCheckBox.Content = Localizer.T("Settings.AutoCheckUpdates", _previewLanguage);
        LanguageLabel.Text = Localizer.T("Settings.Language", _previewLanguage);
        ImportButtonTextBlock.Text = Localizer.T("Settings.Import", _previewLanguage);
        ExportButtonTextBlock.Text = Localizer.T("Settings.Export", _previewLanguage);
        CancelButtonTextBlock.Text = Localizer.T("Settings.Cancel", _previewLanguage);
        SaveButtonTextBlock.Text = Localizer.T("Settings.Save", _previewLanguage);
        UpdateHotkeyPreview();
    }

    private void UpdateHotkeyPreview()
    {
        HideHotkeyValueTextBlock.Text = HotkeyFormatter.Format(_workingCopy.HideHotkey);
        ShowHotkeyValueTextBlock.Text = HotkeyFormatter.Format(_workingCopy.ShowHotkey);
    }

    private void UpdateHotkeyConflictWarning()
    {
        var warnings = new List<string>();
        var hideKeys = _workingCopy.HideHotkey.GetNormalizedKeys();
        var showKeys = _workingCopy.ShowHotkey.GetNormalizedKeys();

        if (hideKeys.Count > 0 && showKeys.Count > 0 && hideKeys.SetEquals(showKeys))
        {
            warnings.Add(Localizer.T("Settings.HotkeyWarningToggle", _previewLanguage));
        }

        AppendBindingWarnings(_workingCopy.HideHotkey, Localizer.T("Settings.HideHotkey", _previewLanguage), warnings);
        AppendBindingWarnings(_workingCopy.ShowHotkey, Localizer.T("Settings.ShowHotkey", _previewLanguage), warnings);

        if (warnings.Count == 0)
        {
            HotkeyConflictTextBlock.Visibility = Visibility.Collapsed;
            HotkeyConflictTextBlock.Text = string.Empty;
            return;
        }

        HotkeyConflictTextBlock.Visibility = Visibility.Visible;
        HotkeyConflictTextBlock.Text = string.Join(Environment.NewLine, warnings.Select(static warning => $"* {warning}"));
    }

    private void AppendBindingWarnings(HotkeyBinding binding, string label, List<string> warnings)
    {
        var keys = binding.GetNormalizedKeys();
        if (keys.Count == 0)
        {
            return;
        }

        if (!ContainsModifier(keys))
        {
            warnings.Add(string.Format(Localizer.T("Settings.HotkeyWarningNoModifier", _previewLanguage), label));
        }

        var key = SerializeKeys(keys);
        if (ReservedHotkeys.TryGetValue(key, out var reserved))
        {
            warnings.Add(string.Format(Localizer.T("Settings.HotkeyWarningReserved", _previewLanguage), label, reserved));
        }
    }

    private void SyncControlsFromWorkingCopy()
    {
        StartWithWindowsCheckBox.IsChecked = _workingCopy.StartWithWindows;
        MinimizeToTrayCheckBox.IsChecked = _workingCopy.MinimizeToTray;
        AutoCheckUpdatesCheckBox.IsChecked = _workingCopy.AutoCheckForUpdates;
        LanguageComboBox.SelectedItem = Localizer.SupportedLanguages.FirstOrDefault(
            option => string.Equals(option.Code, _previewLanguage, StringComparison.OrdinalIgnoreCase))
            ?? Localizer.SupportedLanguages[0];
    }

    private void SyncControlsToWorkingCopy()
    {
        _workingCopy.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _workingCopy.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
        _workingCopy.AutoCheckForUpdates = AutoCheckUpdatesCheckBox.IsChecked != false;
        _workingCopy.Language = _previewLanguage;
    }

    private static bool ContainsModifier(HashSet<int> keys)
    {
        return keys.Contains(0x10) || keys.Contains(0x11) || keys.Contains(0x12) || keys.Contains(0x5B);
    }

    private static string SerializeKeys(IEnumerable<int> keys)
    {
        return string.Join("+", keys.OrderBy(static key => key).Select(static key => key.ToString("X2")));
    }

    private static AppSettings CloneSettings(AppSettings source)
    {
        return new AppSettings
        {
            HideHotkey = HotkeyBinding.FromKeys(source.HideHotkey.Keys),
            ShowHotkey = HotkeyBinding.FromKeys(source.ShowHotkey.Keys),
            StartWithWindows = source.StartWithWindows,
            MinimizeToTray = source.MinimizeToTray,
            AutoCheckForUpdates = source.AutoCheckForUpdates,
            LastUpdateCheckUtc = source.LastUpdateCheckUtc,
            IsLogPanelCollapsed = source.IsLogPanelCollapsed,
            Language = Localizer.NormalizeLanguage(source.Language),
            MainWindowPlacement = source.MainWindowPlacement is null
                ? null
                : new WindowPlacementSettings
                {
                    Left = source.MainWindowPlacement.Left,
                    Top = source.MainWindowPlacement.Top,
                    Width = source.MainWindowPlacement.Width,
                    Height = source.MainWindowPlacement.Height,
                    WindowState = source.MainWindowPlacement.WindowState
                },
            Targets = source.Targets
                .Select(static target => new TargetAppConfig
                {
                    ProcessName = target.ProcessName,
                    ProcessPath = target.ProcessPath,
                    Enabled = target.Enabled,
                    MuteOnHide = target.MuteOnHide
                })
                .ToList()
        };
    }

    private static void CopyInto(AppSettings source, AppSettings destination)
    {
        destination.HideHotkey = HotkeyBinding.FromKeys(source.HideHotkey.Keys);
        destination.ShowHotkey = HotkeyBinding.FromKeys(source.ShowHotkey.Keys);
        destination.StartWithWindows = source.StartWithWindows;
        destination.MinimizeToTray = source.MinimizeToTray;
        destination.AutoCheckForUpdates = source.AutoCheckForUpdates;
        destination.LastUpdateCheckUtc = source.LastUpdateCheckUtc;
        destination.IsLogPanelCollapsed = source.IsLogPanelCollapsed;
        destination.Language = source.Language;
        destination.MainWindowPlacement = source.MainWindowPlacement is null
            ? null
            : new WindowPlacementSettings
            {
                Left = source.MainWindowPlacement.Left,
                Top = source.MainWindowPlacement.Top,
                Width = source.MainWindowPlacement.Width,
                Height = source.MainWindowPlacement.Height,
                WindowState = source.MainWindowPlacement.WindowState
            };
        destination.Targets = source.Targets
            .Select(static target => new TargetAppConfig
            {
                ProcessName = target.ProcessName,
                ProcessPath = target.ProcessPath,
                Enabled = target.Enabled,
                MuteOnHide = target.MuteOnHide
            })
            .ToList();
    }
}
