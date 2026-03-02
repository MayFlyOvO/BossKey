using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using BossKey.App.Localization;
using BossKey.Core.Models;
using BossKey.Core.Services;
using Win32 = Microsoft.Win32;

namespace BossKey.App;

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
    private readonly ObservableCollection<GroupHotkeyOption> _groupHotkeyOptions = [];
    private AppSettings _workingCopy;
    private string _previewLanguage;
    private string? _selectedGroupHotkeyId;
    private bool _isSyncingLanguages;
    private bool _isRefreshingLanguageOptions;

    public AppSettings UpdatedSettings { get; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _workingCopy = CloneSettings(settings);
        UpdatedSettings = CloneSettings(settings);
        _previewLanguage = Localizer.NormalizeLanguage(settings.Language);

        GroupHotkeyComboBox.ItemsSource = _groupHotkeyOptions;
        Localizer.SupportedLanguagesChanged += Localizer_OnSupportedLanguagesChanged;
        Closed += SettingsWindow_OnClosed;
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
            _workingCopy.HideHotkey,
            allowEmpty: true)
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
            _workingCopy.ShowHotkey,
            allowEmpty: true)
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

    private void SetGroupHideHotkeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        var group = GetSelectedGroupHotkeyTarget();
        if (group is null)
        {
            return;
        }

        var dialog = new HotkeyCaptureWindow(
            Localizer.T("Main.GroupSetHideHotkey", _previewLanguage),
            _previewLanguage,
            group.HideHotkey,
            allowEmpty: true)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        group.HideHotkey = dialog.CapturedBinding;
        UpdateGroupHotkeyPreview();
        UpdateHotkeyConflictWarning();
    }

    private void SetGroupShowHotkeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        var group = GetSelectedGroupHotkeyTarget();
        if (group is null)
        {
            return;
        }

        var dialog = new HotkeyCaptureWindow(
            Localizer.T("Main.GroupSetShowHotkey", _previewLanguage),
            _previewLanguage,
            group.ShowHotkey,
            allowEmpty: true)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        group.ShowHotkey = dialog.CapturedBinding;
        UpdateGroupHotkeyPreview();
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
            FileName = $"bosskey-settings-{DateTime.Now:yyyyMMdd-HHmmss}.json",
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

    private async void CheckUpdatesButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Owner is MainWindow mainWindow)
        {
            var shouldClose = await mainWindow.CheckForUpdatesFromSettingsAsync(this);
            if (shouldClose && IsLoaded)
            {
                Close();
            }

            return;
        }

        System.Windows.MessageBox.Show(
            this,
            Localizer.T("Update.CheckFailed", _previewLanguage).Replace("{0}", "Main window not available."),
            Localizer.T("Update.CheckFailedTitle", _previewLanguage),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void AboutButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow(_previewLanguage)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private async void LanguageComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isRefreshingLanguageOptions || LanguageComboBox.SelectedItem is not LanguageOption option)
        {
            return;
        }

        var previousLanguage = _workingCopy.Language;
        var previousPreviewLanguage = _previewLanguage;
        var selectedLanguage = Localizer.NormalizeStoredLanguage(option.Code);

        if (!Localizer.HasLanguage(selectedLanguage))
        {
            SetLanguageControlsBusy(true);
            try
            {
                var result = await Localizer.EnsureLanguageAvailableAsync(selectedLanguage);
                if (!result.Succeeded || !Localizer.HasLanguage(selectedLanguage))
                {
                    _workingCopy.Language = previousLanguage;
                    _previewLanguage = previousPreviewLanguage;
                    RefreshLanguageOptions();
                    ApplyLocalization();
                    UpdateHotkeyConflictWarning();
                    System.Windows.MessageBox.Show(
                        this,
                        string.Format(
                            Localizer.T("Settings.DownloadLanguageFailed", previousPreviewLanguage),
                            result.ErrorMessage ?? "Unknown error."),
                        Localizer.T("Main.InitErrorTitle", previousPreviewLanguage),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
            finally
            {
                SetLanguageControlsBusy(false);
            }
        }

        _workingCopy.Language = selectedLanguage;
        _previewLanguage = Localizer.NormalizeLanguage(_workingCopy.Language);
        RefreshGroupHotkeyOptions();
        ApplyLocalization();
        UpdateHotkeyConflictWarning();
    }

    private async void SyncLanguagesButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isSyncingLanguages)
        {
            return;
        }

        _isSyncingLanguages = true;
        SetLanguageControlsBusy(true);
        try
        {
            var result = await Localizer.RefreshRemoteCatalogAsync();
            _previewLanguage = Localizer.NormalizeLanguage(_workingCopy.Language);
            RefreshLanguageOptions();
            ApplyLocalization();
            UpdateHotkeyConflictWarning();

            if (result.Succeeded)
            {
                System.Windows.MessageBox.Show(
                    this,
                    Localizer.T("Settings.SyncLanguagesSuccess", _previewLanguage),
                    Localizer.T("Main.HintTitle", _previewLanguage),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            System.Windows.MessageBox.Show(
                this,
                string.Format(
                    Localizer.T("Settings.SyncLanguagesFailed", _previewLanguage),
                    result.ErrorMessage ?? "Unknown error."),
                Localizer.T("Main.InitErrorTitle", _previewLanguage),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _isSyncingLanguages = false;
            SetLanguageControlsBusy(false);
        }
    }

    private void GroupHotkeyComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (GroupHotkeyComboBox.SelectedItem is GroupHotkeyOption option)
        {
            _selectedGroupHotkeyId = option.GroupId;
        }

        UpdateGroupHotkeyPreview();
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

    private void Localizer_OnSupportedLanguagesChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _previewLanguage = Localizer.NormalizeLanguage(_workingCopy.Language);
            RefreshLanguageOptions();
            ApplyLocalization();
            UpdateHotkeyConflictWarning();
        });
    }

    private void SettingsWindow_OnClosed(object? sender, EventArgs e)
    {
        Localizer.SupportedLanguagesChanged -= Localizer_OnSupportedLanguagesChanged;
        Closed -= SettingsWindow_OnClosed;
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
        GroupHotkeysSectionTextBlock.Text = Localizer.T("Settings.GroupHotkeys", _previewLanguage);
        GroupHotkeyTargetLabel.Text = Localizer.T("Settings.GroupTarget", _previewLanguage);
        GroupHideHotkeyLabel.Text = Localizer.T("Settings.GroupHideHotkey", _previewLanguage);
        GroupShowHotkeyLabel.Text = Localizer.T("Settings.GroupShowHotkey", _previewLanguage);
        SetGroupHideHotkeyButtonTextBlock.Text = Localizer.T("Settings.SetHotkey", _previewLanguage);
        SetGroupShowHotkeyButtonTextBlock.Text = Localizer.T("Settings.SetHotkey", _previewLanguage);
        GeneralSectionTextBlock.Text = Localizer.T("Settings.General", _previewLanguage);
        StartWithWindowsCheckBox.Content = Localizer.T("Settings.StartWithWindows", _previewLanguage);
        RunAsAdministratorCheckBox.Content = Localizer.T("Settings.RunAsAdministrator", _previewLanguage);
        MinimizeToTrayCheckBox.Content = Localizer.T("Settings.MinimizeToTray", _previewLanguage);
        AutoCheckUpdatesLabelTextBlock.Text = Localizer.T("Settings.AutoCheckUpdates", _previewLanguage);
        AutoCheckUpdatesCheckBox.Content = string.Empty;
        LanguageLabel.Text = Localizer.T("Settings.Language", _previewLanguage);
        ImportButtonTextBlock.Text = Localizer.T("Settings.Import", _previewLanguage);
        ExportButtonTextBlock.Text = Localizer.T("Settings.Export", _previewLanguage);
        CheckUpdatesButtonTextBlock.Text = Localizer.T("Settings.CheckUpdatesNow", _previewLanguage);
        SyncLanguagesButtonTextBlock.Text = Localizer.T("Settings.SyncLanguages", _previewLanguage);
        CancelButtonTextBlock.Text = Localizer.T("Settings.Cancel", _previewLanguage);
        SaveButtonTextBlock.Text = Localizer.T("Settings.Save", _previewLanguage);
        AboutButton.ToolTip = Localizer.T("Settings.AboutTooltip", _previewLanguage);
        UpdateHotkeyPreview();
    }

    private void UpdateHotkeyPreview()
    {
        HideHotkeyValueTextBlock.Text = HotkeyFormatter.Format(_workingCopy.HideHotkey);
        ShowHotkeyValueTextBlock.Text = HotkeyFormatter.Format(_workingCopy.ShowHotkey);
        UpdateGroupHotkeyPreview();
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

        var selectedGroup = GetSelectedGroupHotkeyTarget();
        if (selectedGroup is not null)
        {
            var groupHideKeys = selectedGroup.HideHotkey.GetNormalizedKeys();
            var groupShowKeys = selectedGroup.ShowHotkey.GetNormalizedKeys();
            if (groupHideKeys.Count > 0 && groupShowKeys.Count > 0 && groupHideKeys.SetEquals(groupShowKeys))
            {
                warnings.Add(Localizer.T("Settings.GroupHotkeyWarningToggle", _previewLanguage));
            }

            AppendBindingWarnings(selectedGroup.HideHotkey, Localizer.T("Settings.GroupHideHotkey", _previewLanguage), warnings);
            AppendBindingWarnings(selectedGroup.ShowHotkey, Localizer.T("Settings.GroupShowHotkey", _previewLanguage), warnings);
        }

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
        RunAsAdministratorCheckBox.IsChecked = _workingCopy.RunAsAdministrator;
        MinimizeToTrayCheckBox.IsChecked = _workingCopy.MinimizeToTray;
        AutoCheckUpdatesCheckBox.IsChecked = _workingCopy.AutoCheckForUpdates;
        RefreshLanguageOptions();
        RefreshGroupHotkeyOptions();
    }

    private void SyncControlsToWorkingCopy()
    {
        _workingCopy.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _workingCopy.RunAsAdministrator = RunAsAdministratorCheckBox.IsChecked == true;
        _workingCopy.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
        _workingCopy.AutoCheckForUpdates = AutoCheckUpdatesCheckBox.IsChecked != false;
        _workingCopy.Language = Localizer.NormalizeStoredLanguage(_workingCopy.Language);
    }

    private void RefreshLanguageOptions()
    {
        var options = Localizer.GetSupportedLanguages(_workingCopy.Language);
        _isRefreshingLanguageOptions = true;
        try
        {
            LanguageComboBox.ItemsSource = options;
            LanguageComboBox.SelectedItem = options.FirstOrDefault(option =>
                                           string.Equals(option.Code, _workingCopy.Language, StringComparison.OrdinalIgnoreCase))
                                       ?? options.FirstOrDefault(option =>
                                           string.Equals(option.Code, _previewLanguage, StringComparison.OrdinalIgnoreCase))
                                       ?? options.FirstOrDefault();
        }
        finally
        {
            _isRefreshingLanguageOptions = false;
        }
    }

    private void SetLanguageControlsBusy(bool isBusy)
    {
        LanguageComboBox.IsEnabled = !isBusy;
        SyncLanguagesButton.IsEnabled = !isBusy;
    }

    private void RefreshGroupHotkeyOptions()
    {
        var previousSelection = _selectedGroupHotkeyId;
        _groupHotkeyOptions.Clear();

        foreach (var group in _workingCopy.Groups)
        {
            _groupHotkeyOptions.Add(new GroupHotkeyOption(group.Id, GetGroupDisplayName(group)));
        }

        var targetSelection = _groupHotkeyOptions.FirstOrDefault(option =>
                                  string.Equals(option.GroupId, previousSelection, StringComparison.Ordinal))
                              ?? _groupHotkeyOptions.FirstOrDefault();
        GroupHotkeyComboBox.SelectedItem = targetSelection;
        _selectedGroupHotkeyId = targetSelection?.GroupId;
        UpdateGroupHotkeyPreview();
    }

    private void UpdateGroupHotkeyPreview()
    {
        var group = GetSelectedGroupHotkeyTarget();
        var hasSelection = group is not null;

        GroupHideHotkeyValueTextBlock.Text = hasSelection ? HotkeyFormatter.Format(group!.HideHotkey) : "-";
        GroupShowHotkeyValueTextBlock.Text = hasSelection ? HotkeyFormatter.Format(group!.ShowHotkey) : "-";
        SetGroupHideHotkeyButton.IsEnabled = hasSelection;
        SetGroupShowHotkeyButton.IsEnabled = hasSelection;
    }

    private TargetGroupConfig? GetSelectedGroupHotkeyTarget()
    {
        if (string.IsNullOrWhiteSpace(_selectedGroupHotkeyId))
        {
            return null;
        }

        return _workingCopy.Groups.FirstOrDefault(group => string.Equals(group.Id, _selectedGroupHotkeyId, StringComparison.Ordinal));
    }

    private string GetGroupDisplayName(TargetGroupConfig group)
    {
        if (!string.IsNullOrWhiteSpace(group.Name))
        {
            return group.Name;
        }

        return string.Equals(group.Id, TargetGroupConfig.DefaultGroupId, StringComparison.OrdinalIgnoreCase)
            ? Localizer.T("Main.Ungrouped", _previewLanguage)
            : Localizer.T("Main.NewGroup", _previewLanguage);
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
            RunAsAdministrator = source.RunAsAdministrator,
            MinimizeToTray = source.MinimizeToTray,
            AutoCheckForUpdates = source.AutoCheckForUpdates,
            LastUpdateCheckUtc = source.LastUpdateCheckUtc,
            IsLogPanelCollapsed = source.IsLogPanelCollapsed,
            Language = Localizer.NormalizeStoredLanguage(source.Language),
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
                    Id = target.Id,
                    ProcessName = target.ProcessName,
                    ProcessPath = target.ProcessPath,
                    Enabled = target.Enabled,
                    MuteOnHide = target.MuteOnHide,
                    FreezeOnHide = target.FreezeOnHide
                })
                .ToList(),
            Groups = source.Groups
                .Select(static group => new TargetGroupConfig
                {
                    Id = group.Id,
                    Name = group.Name,
                    HideHotkey = HotkeyBinding.FromKeys(group.HideHotkey.Keys),
                    ShowHotkey = HotkeyBinding.FromKeys(group.ShowHotkey.Keys),
                    IsCollapsed = group.IsCollapsed,
                    Targets = group.Targets
                        .Select(static target => new TargetAppConfig
                        {
                            Id = target.Id,
                            ProcessName = target.ProcessName,
                            ProcessPath = target.ProcessPath,
                            Enabled = target.Enabled,
                            MuteOnHide = target.MuteOnHide,
                            FreezeOnHide = target.FreezeOnHide
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    private static void CopyInto(AppSettings source, AppSettings destination)
    {
        destination.HideHotkey = HotkeyBinding.FromKeys(source.HideHotkey.Keys);
        destination.ShowHotkey = HotkeyBinding.FromKeys(source.ShowHotkey.Keys);
        destination.StartWithWindows = source.StartWithWindows;
        destination.RunAsAdministrator = source.RunAsAdministrator;
        destination.MinimizeToTray = source.MinimizeToTray;
        destination.AutoCheckForUpdates = source.AutoCheckForUpdates;
        destination.LastUpdateCheckUtc = source.LastUpdateCheckUtc;
        destination.IsLogPanelCollapsed = source.IsLogPanelCollapsed;
        destination.Language = Localizer.NormalizeStoredLanguage(source.Language);
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
                Id = target.Id,
                ProcessName = target.ProcessName,
                ProcessPath = target.ProcessPath,
                Enabled = target.Enabled,
                MuteOnHide = target.MuteOnHide,
                FreezeOnHide = target.FreezeOnHide
            })
            .ToList();
        destination.Groups = source.Groups
            .Select(static group => new TargetGroupConfig
            {
                Id = group.Id,
                Name = group.Name,
                HideHotkey = HotkeyBinding.FromKeys(group.HideHotkey.Keys),
                ShowHotkey = HotkeyBinding.FromKeys(group.ShowHotkey.Keys),
                IsCollapsed = group.IsCollapsed,
                Targets = group.Targets
                    .Select(static target => new TargetAppConfig
                    {
                        Id = target.Id,
                        ProcessName = target.ProcessName,
                        ProcessPath = target.ProcessPath,
                        Enabled = target.Enabled,
                        MuteOnHide = target.MuteOnHide,
                        FreezeOnHide = target.FreezeOnHide
                    })
                    .ToList()
            })
            .ToList();
    }

    private sealed record GroupHotkeyOption(string GroupId, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }
}
