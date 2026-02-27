using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using HideProcess.App.Services;
using HideProcess.App.Localization;
using HideProcess.Core.Models;
using HideProcess.Core.Services;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace HideProcess.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string AppName = "HideProcess.App";
    private const int MaxLogEntries = 300;
    private static readonly TimeSpan AutoUpdateCheckInterval = TimeSpan.FromHours(12);
    private const string UpdateRepositoryOwner = "MayFlyOvO";
    private const string UpdateRepositoryName = "HideProcess";

    private readonly JsonSettingsStore _settingsStore = new();
    private readonly AutoStartService _autoStartService = new();
    private readonly ProcessWindowService _processWindowService = new();
    private readonly GlobalHotkeyService _globalHotkeyService = new();
    private readonly AppUpdateService _appUpdateService;
    private readonly ObservableCollection<RunningTargetItem> _runningTargets = [];
    private readonly ObservableCollection<TargetAppConfig> _selectedTargets = [];
    private readonly ObservableCollection<string> _logs = [];

    private HwndSource? _hwndSource;
    private AppSettings _settings = new();
    private Forms.NotifyIcon? _notifyIcon;
    private Drawing.Icon? _trayIcon;
    private bool _allowClose;
    private bool _isCheckingUpdates;
    private bool _isLogCollapsed;
    private string _removeButtonText = "Remove";

    public MainWindow()
    {
        InitializeComponent();
        _appUpdateService = new AppUpdateService(
            UpdateRepositoryOwner,
            UpdateRepositoryName,
            ResolveUpdatePackageType());

        DataContext = this;
        RunningTargetsComboBox.ItemsSource = _runningTargets;
        SelectedTargetsDataGrid.ItemsSource = _selectedTargets;

        Loaded += MainWindow_OnLoaded;
        SourceInitialized += MainWindow_OnSourceInitialized;
        StateChanged += MainWindow_OnStateChanged;
        Closing += MainWindow_OnClosing;
        Closed += MainWindow_OnClosed;
        Localizer.LanguageChanged += Localizer_OnLanguageChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string RemoveButtonText
    {
        get => _removeButtonText;
        private set
        {
            if (string.Equals(_removeButtonText, value, StringComparison.Ordinal))
            {
                return;
            }

            _removeButtonText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemoveButtonText)));
        }
    }

    public ObservableCollection<string> LogEntries => _logs;

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = _settingsStore.Load();
            _settings.Language = Localizer.NormalizeLanguage(_settings.Language);
            _isLogCollapsed = _settings.IsLogPanelCollapsed;
            ApplySavedWindowPlacement();

            Localizer.SetLanguage(_settings.Language);
            SyncTargetsFromSettings();
            RefreshRunningTargets();

            _globalHotkeyService.UpdateBindings(_settings.HideHotkey, _settings.ShowHotkey);
            _globalHotkeyService.HotkeyTriggered += GlobalHotkeyService_OnHotkeyTriggered;
            _globalHotkeyService.Start();

            InitializeTrayIcon();
            ApplyLocalization();
            PersistSettings();
            UpdateMaximizeRestoreGlyph();
            UpdateLogPanelState();
            SetStatus(Localizer.T("Main.StatusReady"));

            if (System.Windows.Application.Current is App app && app.HadUnexpectedPreviousExit)
            {
                var warning = Localizer.T("Main.PreviousUncleanExit");
                AppendLog(warning);
                System.Windows.MessageBox.Show(
                    this,
                    warning,
                    Localizer.T("Main.HintTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            _ = CheckForUpdatesAsync(manualCheck: false);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                Localizer.Format("Main.InitErrorText", ex.Message),
                Localizer.T("Main.InitErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void MainWindow_OnStateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeRestoreGlyph();
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
        {
            HideToTray();
        }
    }

    private void MainWindow_OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose && _settings.MinimizeToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _processWindowService.ShowHiddenTargets(bringToFront: false);
        _globalHotkeyService.Stop();
        SaveCurrentWindowPlacement();
        PersistSettings();
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
        Localizer.LanguageChanged -= Localizer_OnLanguageChanged;
        _globalHotkeyService.Dispose();

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private void GlobalHotkeyService_OnHotkeyTriggered(object? sender, HotkeyAction action)
    {
        Dispatcher.Invoke(() =>
        {
            if (action == HotkeyAction.Toggle)
            {
                if (_processWindowService.HasHiddenWindows)
                {
                    ShowTargets();
                }
                else
                {
                    HideTargets();
                }
            }
            else if (action == HotkeyAction.Hide)
            {
                HideTargets();
            }
            else
            {
                ShowTargets();
            }
        });
    }

    private void Localizer_OnLanguageChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(ApplyLocalization);
    }

    private void RefreshTargetsButton_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshRunningTargets();
        SetStatus(Localizer.T("Main.StatusRefreshed"));
    }

    private void AddTargetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (RunningTargetsComboBox.SelectedItem is not RunningTargetItem selected)
        {
            System.Windows.MessageBox.Show(
                this,
                Localizer.T("Main.SelectRunningTarget"),
                Localizer.T("Main.HintTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var alreadyExists = _selectedTargets.Any(target =>
            (!string.IsNullOrWhiteSpace(target.ProcessPath)
             && !string.IsNullOrWhiteSpace(selected.ProcessPath)
             && string.Equals(target.ProcessPath, selected.ProcessPath, StringComparison.OrdinalIgnoreCase))
            || string.Equals(target.ProcessName, selected.ProcessName, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            SetStatus(Localizer.T("Main.StatusDuplicate"));
            return;
        }

        _selectedTargets.Add(new TargetAppConfig
        {
            ProcessName = selected.ProcessName,
            ProcessPath = selected.ProcessPath,
            Enabled = true,
            MuteOnHide = false
        });

        PersistSettings();
        SetStatus(Localizer.Format("Main.StatusAdded", selected.ProcessName));
    }

    private void RemoveTargetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetAppConfig target)
        {
            return;
        }

        _selectedTargets.Remove(target);
        PersistSettings();
        SetStatus(Localizer.Format("Main.StatusRemoved", target.ProcessName));
    }

    private void HideNowButton_OnClick(object sender, RoutedEventArgs e)
    {
        HideTargets();
    }

    private void ShowNowButton_OnClick(object sender, RoutedEventArgs e)
    {
        ShowTargets();
    }

    private void OpenSettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        PersistSettings();
        var dialog = new SettingsWindow(_settings) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _settings.HideHotkey = HotkeyBinding.FromKeys(dialog.UpdatedSettings.HideHotkey.Keys);
        _settings.ShowHotkey = HotkeyBinding.FromKeys(dialog.UpdatedSettings.ShowHotkey.Keys);
        _settings.StartWithWindows = dialog.UpdatedSettings.StartWithWindows;
        _settings.MinimizeToTray = dialog.UpdatedSettings.MinimizeToTray;
        _settings.AutoCheckForUpdates = dialog.UpdatedSettings.AutoCheckForUpdates;
        _settings.LastUpdateCheckUtc = dialog.UpdatedSettings.LastUpdateCheckUtc;
        _settings.Language = Localizer.NormalizeLanguage(dialog.UpdatedSettings.Language);
        _settings.IsLogPanelCollapsed = dialog.UpdatedSettings.IsLogPanelCollapsed;
        _isLogCollapsed = _settings.IsLogPanelCollapsed;
        _settings.Targets = dialog.UpdatedSettings.Targets
            .Select(static target => new TargetAppConfig
            {
                ProcessName = target.ProcessName,
                ProcessPath = target.ProcessPath,
                Enabled = target.Enabled,
                MuteOnHide = target.MuteOnHide
            })
            .ToList();

        _globalHotkeyService.UpdateBindings(_settings.HideHotkey, _settings.ShowHotkey);
        Localizer.SetLanguage(_settings.Language);
        SyncTargetsFromSettings();
        ApplyLocalization();
        PersistSettings();
        SetStatus(Localizer.T("Main.StatusSettingsApplied"));
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ClearLogsButton_OnClick(object sender, RoutedEventArgs e)
    {
        _logs.Clear();
        SetStatus(Localizer.T("Main.LogCleared"));
    }

    private void ToggleLogsButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isLogCollapsed = !_isLogCollapsed;
        _settings.IsLogPanelCollapsed = _isLogCollapsed;
        UpdateLogPanelState();
        PersistSettings();
    }

    private void RefreshRunningTargets()
    {
        _runningTargets.Clear();
        var targets = _processWindowService.GetRunningTargets();
        foreach (var target in targets)
        {
            _runningTargets.Add(new RunningTargetItem(
                target.ProcessName,
                target.ProcessId,
                target.WindowTitle,
                target.ProcessPath));
        }

        RunningTargetsComboBox.SelectedIndex = _runningTargets.Count > 0 ? 0 : -1;
    }

    private void HideTargets()
    {
        if (_selectedTargets.Count == 0)
        {
            SetStatus(Localizer.T("Main.StatusNoTargets"));
            return;
        }

        var hiddenCount = _processWindowService.HideTargets(_selectedTargets);
        SetStatus(hiddenCount > 0
            ? Localizer.Format("Main.StatusHidden", hiddenCount)
            : Localizer.T("Main.StatusNoMatched"));
    }

    private int ShowTargets()
    {
        var restoredCount = _processWindowService.ShowHiddenTargets();
        SetStatus(restoredCount > 0
            ? Localizer.Format("Main.StatusShown", restoredCount)
            : Localizer.T("Main.StatusNoHidden"));
        return restoredCount;
    }

    private void SyncTargetsFromSettings()
    {
        _selectedTargets.Clear();
        foreach (var target in _settings.Targets)
        {
            _selectedTargets.Add(new TargetAppConfig
            {
                ProcessName = target.ProcessName,
                ProcessPath = target.ProcessPath,
                Enabled = target.Enabled,
                MuteOnHide = target.MuteOnHide
            });
        }
    }

    private void PersistSettings()
    {
        _settings.Targets = _selectedTargets
            .Select(static target => new TargetAppConfig
            {
                ProcessName = target.ProcessName,
                ProcessPath = target.ProcessPath,
                Enabled = target.Enabled,
                MuteOnHide = target.MuteOnHide
            })
            .ToList();

        try
        {
            _settingsStore.Save(_settings);

            var executablePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                _autoStartService.SetEnabled(AppName, executablePath, _settings.StartWithWindows);
            }
        }
        catch (Exception ex)
        {
            SetStatus(Localizer.Format("Main.StatusSaveFailed", ex.Message));
        }
    }

    private void InitializeTrayIcon()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        _trayIcon ??= LoadTrayIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "HideProcess",
            Visible = true
        };

        BuildTrayMenu();
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void BuildTrayMenu()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Localizer.T("Tray.ShowMain"), null, (_, _) => ShowFromTray());
        menu.Items.Add(Localizer.T("Tray.HideTargets"), null, (_, _) => Dispatcher.Invoke(HideTargets));
        menu.Items.Add(Localizer.T("Tray.ShowTargets"), null, (_, _) => Dispatcher.Invoke(() => ShowTargets()));
        menu.Items.Add(Localizer.T("Tray.CheckUpdates"), null, (_, _) => Dispatcher.Invoke(() => _ = CheckForUpdatesAsync(manualCheck: true)));
        menu.Items.Add("-");
        menu.Items.Add(Localizer.T("Tray.Exit"), null, (_, _) =>
        {
            _allowClose = true;
            Dispatcher.Invoke(Close);
        });

        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.ContextMenuStrip = menu;
    }

    private void HideToTray()
    {
        Hide();
        _notifyIcon?.ShowBalloonTip(
            1200,
            Localizer.T("Tray.MinimizedTitle"),
            Localizer.T("Tray.MinimizedText"),
            Forms.ToolTipIcon.Info);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                using var associatedIcon = Drawing.Icon.ExtractAssociatedIcon(processPath);
                if (associatedIcon is not null)
                {
                    return (Drawing.Icon)associatedIcon.Clone();
                }
            }
        }
        catch
        {
        }

        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }

    private void ApplyLocalization()
    {
        Title = Localizer.T("Main.WindowTitle");
        AppTitleTextBlock.Text = Localizer.T("Main.Title");
        RunningLabelTextBlock.Text = Localizer.T("Main.RunningLabel");
        SelectedTargetsLabelTextBlock.Text = Localizer.T("Main.SelectedTargets");
        RefreshButtonTextBlock.Text = Localizer.T("Main.Refresh");
        AddTargetButtonTextBlock.Text = Localizer.T("Main.AddTarget");
        HideNowButtonTextBlock.Text = Localizer.T("Main.HideNow");
        ShowNowButtonTextBlock.Text = Localizer.T("Main.ShowNow");
        OpenSettingsButtonTextBlock.Text = Localizer.T("Main.OpenSettings");
        LogTitleTextBlock.Text = Localizer.T("Main.LogTitle");
        ClearLogsButtonTextBlock.Text = Localizer.T("Main.ClearLogs");
        EnabledColumn.Header = Localizer.T("Main.EnabledColumn");
        ProcessColumn.Header = Localizer.T("Main.ProcessColumn");
        PathColumn.Header = Localizer.T("Main.PathColumn");
        MuteOnHideColumn.Header = string.Equals(Localizer.CurrentLanguage, "en-US", StringComparison.OrdinalIgnoreCase)
            ? "Mute"
            : "静音";
        ActionColumn.Header = Localizer.T("Main.ActionColumn");
        RemoveButtonText = Localizer.T("Main.Remove");

        var hideKeys = _settings.HideHotkey.GetNormalizedKeys();
        var showKeys = _settings.ShowHotkey.GetNormalizedKeys();
        var hideText = HotkeyFormatter.Format(_settings.HideHotkey);
        var showText = HotkeyFormatter.Format(_settings.ShowHotkey);
        HotkeyHintTextBlock.Text = hideKeys.Count > 0 && hideKeys.SetEquals(showKeys)
            ? Localizer.Format("Main.ToggleHint", hideText)
            : Localizer.Format("Main.HotkeyHint", hideText, showText);

        UpdateLogPanelState();
        BuildTrayMenu();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        UpdateMaximizeRestoreGlyph();
    }

    private void UpdateMaximizeRestoreGlyph()
    {
        MaximizeRestoreGlyphTextBlock.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = $"{DateTime.Now:HH:mm:ss}  {message}";
        AppendLog(message);
    }

    private void UpdateLogPanelState()
    {
        if (LogListBox is null || StatusTextBlock is null)
        {
            return;
        }

        LogListBox.Visibility = _isLogCollapsed ? Visibility.Collapsed : Visibility.Visible;
        StatusTextBlock.Visibility = _isLogCollapsed ? Visibility.Visible : Visibility.Collapsed;
        ToggleLogsGlyphTextBlock.Text = _isLogCollapsed ? "\uE70D" : "\uE70E";
        ToggleLogsButtonTextBlock.Text = _isLogCollapsed
            ? Localizer.T("Main.ExpandLogs")
            : Localizer.T("Main.CollapseLogs");
    }

    private void AppendLog(string message)
    {
        _logs.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        while (_logs.Count > MaxLogEntries)
        {
            _logs.RemoveAt(_logs.Count - 1);
        }
    }

    internal Task CheckForUpdatesFromSettingsAsync(Window owner)
    {
        return CheckForUpdatesAsync(manualCheck: true, owner);
    }

    private async Task CheckForUpdatesAsync(bool manualCheck, Window? dialogOwner = null)
    {
        if (_isCheckingUpdates)
        {
            return;
        }

        if (!manualCheck)
        {
            if (!_settings.AutoCheckForUpdates)
            {
                return;
            }

            if (_settings.LastUpdateCheckUtc is DateTime lastCheckUtc
                && DateTime.UtcNow - lastCheckUtc < AutoUpdateCheckInterval)
            {
                return;
            }
        }

        _isCheckingUpdates = true;
        try
        {
            if (manualCheck)
            {
                SetStatus(Localizer.T("Update.StatusChecking"));
            }

            var currentVersion = GetCurrentAppVersion();
            UpdateCheckResult result;
            try
            {
                result = await _appUpdateService.CheckForUpdatesAsync(currentVersion);
            }
            catch (Exception ex)
            {
                result = UpdateCheckResult.Failed(ex.Message);
            }

            _settings.LastUpdateCheckUtc = DateTime.UtcNow;
            PersistSettings();

            if (result.Status == UpdateCheckStatus.NoUpdate)
            {
                if (manualCheck)
                {
                    SetStatus(Localizer.T("Update.StatusNoUpdate"));
                    System.Windows.MessageBox.Show(
                        dialogOwner ?? this,
                        Localizer.Format("Update.NoUpdateMessage", currentVersion),
                        Localizer.T("Update.NoUpdateTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return;
            }

            if (result.Status == UpdateCheckStatus.Failed)
            {
                var error = result.ErrorMessage ?? "Unknown error.";
                SetStatus(Localizer.Format("Update.StatusCheckFailed", error));
                if (manualCheck)
                {
                    System.Windows.MessageBox.Show(
                        dialogOwner ?? this,
                        Localizer.Format("Update.CheckFailed", error),
                        Localizer.T("Update.CheckFailedTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                return;
            }

            SetStatus(Localizer.Format("Update.StatusAvailable", result.LatestVersion));
            var releaseNotes = result.ReleaseNotes?.Trim();
            if (!string.IsNullOrWhiteSpace(releaseNotes) && releaseNotes.Length > 420)
            {
                releaseNotes = $"{releaseNotes[..420]}...";
            }

            var message = Localizer.Format(
                "Update.AvailablePrompt",
                result.CurrentVersion,
                result.LatestVersion);

            if (!string.IsNullOrWhiteSpace(releaseNotes))
            {
                message += $"{Environment.NewLine}{Environment.NewLine}{Localizer.T("Update.ReleaseNotesLabel")}{Environment.NewLine}{releaseNotes}";
            }

            var choice = System.Windows.MessageBox.Show(
                dialogOwner ?? this,
                message,
                Localizer.T("Update.AvailableTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (choice != MessageBoxResult.Yes)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(result.InstallerDownloadUrl))
            {
                if (!string.IsNullOrWhiteSpace(result.ReleasePageUrl))
                {
                    Process.Start(new ProcessStartInfo(result.ReleasePageUrl) { UseShellExecute = true });
                    SetStatus(Localizer.T("Update.StatusOpenedReleasePage"));
                    return;
                }

                System.Windows.MessageBox.Show(
                    dialogOwner ?? this,
                    Localizer.T("Update.NoInstallerAsset"),
                    Localizer.T("Update.CheckFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                SetStatus(Localizer.T("Update.StatusDownloading"));
                var installerPath = await _appUpdateService.DownloadInstallerAsync(result.InstallerDownloadUrl, result.ReleaseTag);
                Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
                SetStatus(Localizer.T("Update.StatusStartingInstaller"));
                _allowClose = true;
                Close();
            }
            catch (Exception ex)
            {
                SetStatus(Localizer.Format("Update.StatusDownloadFailed", ex.Message));
                System.Windows.MessageBox.Show(
                    dialogOwner ?? this,
                    Localizer.Format("Update.DownloadFailed", ex.Message),
                    Localizer.T("Update.CheckFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        finally
        {
            _isCheckingUpdates = false;
        }
    }

    private static Version GetCurrentAppVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        if (version is null)
        {
            return new Version(0, 0, 0, 0);
        }

        var build = version.Build < 0 ? 0 : version.Build;
        var revision = version.Revision < 0 ? 0 : version.Revision;
        return new Version(version.Major, version.Minor, build, revision);
    }

    private static UpdatePackageType ResolveUpdatePackageType()
    {
        var metadataType = GetAssemblyMetadataValue("UpdateChannel");
        if (string.Equals(metadataType, "singlefile", StringComparison.OrdinalIgnoreCase))
        {
            return UpdatePackageType.SingleFile;
        }

        if (string.Equals(metadataType, "installer", StringComparison.OrdinalIgnoreCase))
        {
            return UpdatePackageType.Installer;
        }

        if (AppContext.GetData("IsSingleFile") is bool isSingleFile && isSingleFile)
        {
            return UpdatePackageType.SingleFile;
        }

        return UpdatePackageType.Installer;
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

    private void SaveCurrentWindowPlacement()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _settings.MainWindowPlacement = new WindowPlacementSettings
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            WindowState = WindowState == WindowState.Minimized ? WindowState.Normal.ToString() : WindowState.ToString()
        };
    }

    private void ApplySavedWindowPlacement()
    {
        var placement = _settings.MainWindowPlacement;
        if (placement is null || !IsPlacementValid(placement))
        {
            return;
        }

        Width = Math.Max(MinWidth, placement.Width);
        Height = Math.Max(MinHeight, placement.Height);
        Left = placement.Left;
        Top = placement.Top;

        if (Enum.TryParse<WindowState>(placement.WindowState, true, out var state)
            && state is WindowState.Normal or WindowState.Maximized)
        {
            WindowState = state;
        }
    }

    private static bool IsPlacementValid(WindowPlacementSettings placement)
    {
        if (placement.Width < 300 || placement.Height < 240)
        {
            return false;
        }

        var virtualRect = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        var candidate = new Rect(placement.Left, placement.Top, placement.Width, placement.Height);
        return candidate.IntersectsWith(virtualRect);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfoMessage)
        {
            UpdateMaximizedWorkArea(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void UpdateMaximizedWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var rcWork = monitorInfo.rcWork;
        var rcMonitor = monitorInfo.rcMonitor;

        minMaxInfo.ptMaxPosition.x = Math.Abs(rcWork.left - rcMonitor.left);
        minMaxInfo.ptMaxPosition.y = Math.Abs(rcWork.top - rcMonitor.top);
        minMaxInfo.ptMaxSize.x = Math.Abs(rcWork.right - rcWork.left);
        minMaxInfo.ptMaxSize.y = Math.Abs(rcWork.bottom - rcWork.top);

        Marshal.StructureToPtr(minMaxInfo, lParam, fDeleteOld: true);
    }

    private const int WmGetMinMaxInfoMessage = 0x0024;
    private const int MonitorDefaultToNearest = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint ptReserved;
        public NativePoint ptMaxSize;
        public NativePoint ptMaxPosition;
        public NativePoint ptMinTrackSize;
        public NativePoint ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public NativeRect rcMonitor;
        public NativeRect rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    private struct NativeRect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private sealed record RunningTargetItem(
        string ProcessName,
        int ProcessId,
        string WindowTitle,
        string? ProcessPath)
    {
        public string DisplayText =>
            $"{ProcessName} ({ProcessId}) - {WindowTitle}" +
            (string.IsNullOrWhiteSpace(ProcessPath) ? string.Empty : $"  [{Path.GetFileName(ProcessPath)}]");

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
