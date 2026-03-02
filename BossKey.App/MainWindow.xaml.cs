using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BossKey.App.Services;
using BossKey.App.Localization;
using BossKey.App.Models;
using BossKey.Core.Models;
using BossKey.Core.Services;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace BossKey.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string AppName = "BossKey.App";
    private const string AllTargetsRouteId = "__all__";
    private const string TargetTileDragFormat = "BossKey.TargetTile";
    private const double TargetTileAutoScrollEdgeThreshold = 72d;
    private const double TargetTileAutoScrollMaxSpeed = 18d;
    private const int MaxLogEntries = 300;
    private static readonly TimeSpan AutoUpdateCheckInterval = TimeSpan.FromHours(12);
    private const string UpdateRepositoryOwner = "MayFlyOvO";
    private const string UpdateRepositoryName = "BossKey";

    private readonly JsonSettingsStore _settingsStore = new();
    private readonly AutoStartService _autoStartService = new();
    private readonly ProcessWindowService _processWindowService = new();
    private readonly WindowPickerService _windowPickerService;
    private readonly WindowPickerHighlightWindow _windowPickerHighlightWindow = new();
    private readonly GlobalHotkeyService _globalHotkeyService = new();
    private readonly UpdatePackageType _currentPackageType;
    private readonly AppUpdateService _appUpdateService;
    private readonly AppIconService _appIconService = new();
    private readonly ObservableCollection<RunningTargetItem> _runningTargets = [];
    private readonly ObservableCollection<TargetGroupViewModel> _groupCards = [];
    private readonly ObservableCollection<TargetGroupViewModel> _visibleGroupCards = [];
    private readonly ObservableCollection<TargetTileViewModel> _emptyUngroupedTargets = [];
    private readonly ObservableCollection<string> _logs = [];

    private HwndSource? _hwndSource;
    private AppSettings _settings = new();
    private Forms.NotifyIcon? _notifyIcon;
    private Drawing.Icon? _trayIcon;
    private bool _allowClose;
    private bool _isClosing;
    private bool _isCheckingUpdates;
    private bool _isLogCollapsed;
    private TargetGroupViewModel? _defaultGroupCard;
    private Visibility _updateDownloadOverlayVisibility = Visibility.Collapsed;
    private string _updateDownloadProgressText = "0%";
    private string _updateDownloadArcData = string.Empty;
    private string _updateDownloadTitleText = string.Empty;
    private string _removeButtonText = "Remove";
    private System.Windows.Point? _dragStartPoint;
    private System.Windows.Point? _dragScrollViewerPosition;
    private string _newGroupButtonText = "New Group";
    private string _targetEnabledText = "Enabled";
    private string _targetMuteOnHideText = "Mute on hide";
    private string _targetFreezeOnHideText = "Freeze on hide";
    private string _renameGroupText = "Rename group";
    private string _deleteGroupText = "Delete group";
    private string _toggleGroupText = "Collapse or expand group";
    private string _setGroupHideHotkeyText = "Set group hide hotkey";
    private string _setGroupShowHotkeyText = "Set group show hotkey";
    private string _emptyGroupHintText = "Drag apps here";
    private string _ungroupedDropHintText = "Drop here to remove from group";
    private readonly DispatcherTimer _targetTileAutoScrollTimer;
    private TargetTileDragPreviewWindow? _targetTileDragPreviewWindow;

    public MainWindow()
    {
        InitializeComponent();
        _windowPickerService = new WindowPickerService(_processWindowService);
        _currentPackageType = ResolveUpdatePackageType();
        _appUpdateService = new AppUpdateService(
            UpdateRepositoryOwner,
            UpdateRepositoryName,
            _currentPackageType);

        DataContext = this;
        RunningTargetsComboBox.ItemsSource = _runningTargets;
        _targetTileAutoScrollTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Input,
            TargetTileAutoScrollTimer_OnTick,
            Dispatcher);

        Loaded += MainWindow_OnLoaded;
        SourceInitialized += MainWindow_OnSourceInitialized;
        StateChanged += MainWindow_OnStateChanged;
        Closing += MainWindow_OnClosing;
        Closed += MainWindow_OnClosed;
        Localizer.LanguageChanged += Localizer_OnLanguageChanged;
        _windowPickerService.HoverTargetChanged += WindowPickerService_OnHoverTargetChanged;
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

    public string NewGroupButtonText
    {
        get => _newGroupButtonText;
        private set
        {
            if (string.Equals(_newGroupButtonText, value, StringComparison.Ordinal))
            {
                return;
            }

            _newGroupButtonText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewGroupButtonText)));
        }
    }

    public ObservableCollection<string> LogEntries => _logs;
    public ObservableCollection<TargetGroupViewModel> GroupCards => _groupCards;
    public ObservableCollection<TargetGroupViewModel> VisibleGroupCards => _visibleGroupCards;
    public ObservableCollection<TargetTileViewModel> UngroupedTargets => _defaultGroupCard?.Targets ?? _emptyUngroupedTargets;

    public string TargetEnabledText
    {
        get => _targetEnabledText;
        private set
        {
            if (string.Equals(_targetEnabledText, value, StringComparison.Ordinal))
            {
                return;
            }

            _targetEnabledText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetEnabledText)));
        }
    }

    public string TargetMuteOnHideText
    {
        get => _targetMuteOnHideText;
        private set
        {
            if (string.Equals(_targetMuteOnHideText, value, StringComparison.Ordinal))
            {
                return;
            }

            _targetMuteOnHideText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetMuteOnHideText)));
        }
    }

    public string TargetFreezeOnHideText
    {
        get => _targetFreezeOnHideText;
        private set
        {
            if (string.Equals(_targetFreezeOnHideText, value, StringComparison.Ordinal))
            {
                return;
            }

            _targetFreezeOnHideText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetFreezeOnHideText)));
        }
    }

    public string RenameGroupText
    {
        get => _renameGroupText;
        private set
        {
            if (string.Equals(_renameGroupText, value, StringComparison.Ordinal))
            {
                return;
            }

            _renameGroupText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RenameGroupText)));
        }
    }

    public string DeleteGroupText
    {
        get => _deleteGroupText;
        private set
        {
            if (string.Equals(_deleteGroupText, value, StringComparison.Ordinal))
            {
                return;
            }

            _deleteGroupText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeleteGroupText)));
        }
    }

    public string ToggleGroupText
    {
        get => _toggleGroupText;
        private set
        {
            if (string.Equals(_toggleGroupText, value, StringComparison.Ordinal))
            {
                return;
            }

            _toggleGroupText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToggleGroupText)));
        }
    }

    public string SetGroupHideHotkeyText
    {
        get => _setGroupHideHotkeyText;
        private set
        {
            if (string.Equals(_setGroupHideHotkeyText, value, StringComparison.Ordinal))
            {
                return;
            }

            _setGroupHideHotkeyText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SetGroupHideHotkeyText)));
        }
    }

    public string SetGroupShowHotkeyText
    {
        get => _setGroupShowHotkeyText;
        private set
        {
            if (string.Equals(_setGroupShowHotkeyText, value, StringComparison.Ordinal))
            {
                return;
            }

            _setGroupShowHotkeyText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SetGroupShowHotkeyText)));
        }
    }

    public string EmptyGroupHintText
    {
        get => _emptyGroupHintText;
        private set
        {
            if (string.Equals(_emptyGroupHintText, value, StringComparison.Ordinal))
            {
                return;
            }

            _emptyGroupHintText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmptyGroupHintText)));
        }
    }

    public string UngroupedDropHintText
    {
        get => _ungroupedDropHintText;
        private set
        {
            if (string.Equals(_ungroupedDropHintText, value, StringComparison.Ordinal))
            {
                return;
            }

            _ungroupedDropHintText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UngroupedDropHintText)));
        }
    }

    public Visibility UpdateDownloadOverlayVisibility
    {
        get => _updateDownloadOverlayVisibility;
        private set
        {
            if (_updateDownloadOverlayVisibility == value)
            {
                return;
            }

            _updateDownloadOverlayVisibility = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateDownloadOverlayVisibility)));
        }
    }

    public string UpdateDownloadProgressText
    {
        get => _updateDownloadProgressText;
        private set
        {
            if (string.Equals(_updateDownloadProgressText, value, StringComparison.Ordinal))
            {
                return;
            }

            _updateDownloadProgressText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateDownloadProgressText)));
        }
    }

    public string UpdateDownloadArcData
    {
        get => _updateDownloadArcData;
        private set
        {
            if (string.Equals(_updateDownloadArcData, value, StringComparison.Ordinal))
            {
                return;
            }

            _updateDownloadArcData = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateDownloadArcData)));
        }
    }

    public string UpdateDownloadTitleText
    {
        get => _updateDownloadTitleText;
        private set
        {
            if (string.Equals(_updateDownloadTitleText, value, StringComparison.Ordinal))
            {
                return;
            }

            _updateDownloadTitleText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateDownloadTitleText)));
        }
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = _settingsStore.Load();
            _settings.Language = Localizer.NormalizeStoredLanguage(_settings.Language);
            _isLogCollapsed = _settings.IsLogPanelCollapsed;
            ApplySavedWindowPlacement();

            Localizer.SetLanguage(_settings.Language);
            SyncGroupsFromSettings();
            RefreshRunningTargets();

            _globalHotkeyService.UpdateBindings(BuildHotkeyRoutes());
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

            _ = RefreshLanguageCatalogInBackgroundAsync();
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

        _isClosing = true;
        _windowPickerService.Cancel();
        _windowPickerHighlightWindow.HideHighlight();
        EndTargetTileDrag();
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
        _windowPickerService.HoverTargetChanged -= WindowPickerService_OnHoverTargetChanged;
        _windowPickerService.Dispose();
        _windowPickerHighlightWindow.Close();
        _targetTileAutoScrollTimer.Stop();
        _targetTileDragPreviewWindow?.Close();
        _targetTileDragPreviewWindow = null;
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

    private void GlobalHotkeyService_OnHotkeyTriggered(object? sender, HotkeyTriggeredEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_windowPickerService.IsPicking)
            {
                return;
            }

            if (string.Equals(e.RouteId, AllTargetsRouteId, StringComparison.Ordinal))
            {
                if (e.Action == HotkeyAction.Toggle)
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
                else if (e.Action == HotkeyAction.Hide)
                {
                    HideTargets();
                }
                else
                {
                    ShowTargets();
                }

                return;
            }

            if (e.Action == HotkeyAction.Toggle)
            {
                if (_processWindowService.HasHiddenWindowsInGroup(e.RouteId))
                {
                    ShowTargets(e.RouteId);
                }
                else
                {
                    HideTargets(e.RouteId);
                }
            }
            else if (e.Action == HotkeyAction.Hide)
            {
                HideTargets(e.RouteId);
            }
            else
            {
                ShowTargets(e.RouteId);
            }
        });
    }

    private void Localizer_OnLanguageChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(ApplyLocalization);
    }

    private void WindowPickerService_OnHoverTargetChanged(object? sender, WindowPickerHoverChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing || e.HoverTarget is null)
            {
                _windowPickerHighlightWindow.HideHighlight();
                return;
            }

            _windowPickerHighlightWindow.ShowHighlight(
                e.HoverTarget.Left,
                e.HoverTarget.Top,
                e.HoverTarget.Width,
                e.HoverTarget.Height,
                e.HoverTarget.Target.ProcessName);
        });
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

        AddTarget(selected.ProcessName, selected.ProcessPath);
    }

    private async void PickWindowButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_windowPickerService.IsPicking)
        {
            SetStatus(Localizer.T("Main.StatusPickerBusy"));
            return;
        }

        Task<WindowPickResult> pickTask;
        try
        {
            pickTask = _windowPickerService.PickAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                Localizer.Format("Main.InitErrorText", ex.Message),
                Localizer.T("Main.InitErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        SetStatus(Localizer.T("Main.StatusPickerStarted"));
        _notifyIcon?.ShowBalloonTip(
            1800,
            Localizer.T("Main.PickWindow"),
            Localizer.T("Main.PickWindowHint"),
            Forms.ToolTipIcon.Info);

        var restoreState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
        Hide();

        var result = await pickTask;
        if (_isClosing)
        {
            return;
        }

        _windowPickerHighlightWindow.HideHighlight();
        Show();
        WindowState = restoreState;
        Activate();

        if (result.IsCanceled || result.Target is null)
        {
            SetStatus(Localizer.T("Main.StatusPickerCanceled"));
            return;
        }

        RefreshRunningTargets();
        AddTarget(result.Target.ProcessName, result.Target.ProcessPath);
    }

    private void RemoveTargetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetTileViewModel target)
        {
            return;
        }

        var group = GetOwningGroup(target);
        if (group is null)
        {
            return;
        }

        _processWindowService.ShowHiddenTargets([target.Config], group.Id, bringToFront: false);
        group.Targets.Remove(target);
        group.RefreshHotkeyText();
        PersistSettings();
        SetStatus(Localizer.Format("Main.StatusRemoved", target.ProcessName));
    }

    private TargetGroupViewModel? GetOwningGroup(TargetTileViewModel tile)
    {
        return _groupCards.FirstOrDefault(candidate => candidate.Targets.Contains(tile));
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
        try
        {
            PersistSettings();
            var dialog = new SettingsWindow(_settings) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var previousRunAsAdministrator = _settings.RunAsAdministrator;

            _settings.HideHotkey = HotkeyBinding.FromKeys(dialog.UpdatedSettings.HideHotkey.Keys);
            _settings.ShowHotkey = HotkeyBinding.FromKeys(dialog.UpdatedSettings.ShowHotkey.Keys);
            _settings.StartWithWindows = dialog.UpdatedSettings.StartWithWindows;
            _settings.RunAsAdministrator = dialog.UpdatedSettings.RunAsAdministrator;
            _settings.MinimizeToTray = dialog.UpdatedSettings.MinimizeToTray;
            _settings.AutoCheckForUpdates = dialog.UpdatedSettings.AutoCheckForUpdates;
            _settings.LastUpdateCheckUtc = dialog.UpdatedSettings.LastUpdateCheckUtc;
            _settings.Language = Localizer.NormalizeStoredLanguage(dialog.UpdatedSettings.Language);
            _settings.IsLogPanelCollapsed = dialog.UpdatedSettings.IsLogPanelCollapsed;
            _isLogCollapsed = _settings.IsLogPanelCollapsed;
            _settings.Targets = dialog.UpdatedSettings.Targets
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
            _settings.Groups = dialog.UpdatedSettings.Groups
                .Select(group => new TargetGroupConfig
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

            SyncGroupsFromSettings();
            _globalHotkeyService.UpdateBindings(BuildHotkeyRoutes());
            Localizer.SetLanguage(_settings.Language);
            ApplyLocalization();
            PersistSettings();

            if (previousRunAsAdministrator != _settings.RunAsAdministrator)
            {
                System.Windows.MessageBox.Show(
                    this,
                    Localizer.T("Settings.RunAsAdministratorChanged"),
                    Localizer.T("Main.HintTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            SetStatus(Localizer.T("Main.StatusSettingsApplied"));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                Localizer.T("Main.InitErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            SetStatus(Localizer.Format("Main.InitErrorText", ex.Message));
        }
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
        var targets = EnumerateAllTargetConfigs().ToList();
        if (targets.Count == 0)
        {
            SetStatus(Localizer.T("Main.StatusNoTargets"));
            return;
        }

        var hiddenCount = _processWindowService.HideTargets(targets);
        SetStatus(hiddenCount > 0
            ? Localizer.Format("Main.StatusHidden", hiddenCount)
            : Localizer.T("Main.StatusNoMatched"));
    }

    private void HideTargets(string groupId)
    {
        var group = _groupCards.FirstOrDefault(item => string.Equals(item.Id, groupId, StringComparison.Ordinal));
        if (group is null)
        {
            return;
        }

        var targets = group.Targets.Select(static tile => tile.Config).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var hiddenCount = _processWindowService.HideTargets(targets, groupId);
        SetStatus(hiddenCount > 0
            ? Localizer.Format("Main.StatusHidden", hiddenCount)
            : Localizer.T("Main.StatusNoMatched"));
    }

    private int ShowTargets(string? groupId = null)
    {
        var restoredCount = _processWindowService.ShowHiddenTargets(groupId);
        SetStatus(restoredCount > 0
            ? Localizer.Format("Main.StatusShown", restoredCount)
            : Localizer.T("Main.StatusNoHidden"));
        return restoredCount;
    }

    private void SyncGroupsFromSettings()
    {
        _groupCards.Clear();
        _visibleGroupCards.Clear();
        _defaultGroupCard = null;

        foreach (var group in _settings.Groups)
        {
            var viewModel = BuildGroupViewModel(group);
            _groupCards.Add(viewModel);
            if (viewModel.IsDefaultGroup)
            {
                _defaultGroupCard = viewModel;
            }
            else
            {
                _visibleGroupCards.Add(viewModel);
            }
        }

        FindOrCreateDefaultGroup();
        RefreshGroupDisplayNames();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UngroupedTargets)));
    }

    private void PersistSettings()
    {
        _settings.Groups = _groupCards
            .Select(group => new TargetGroupConfig
            {
                Id = group.Id,
                Name = group.Name,
                HideHotkey = HotkeyBinding.FromKeys(group.HideHotkey.Keys),
                ShowHotkey = HotkeyBinding.FromKeys(group.ShowHotkey.Keys),
                IsCollapsed = group.IsCollapsed,
                Targets = group.Targets
                    .Select(static tile => new TargetAppConfig
                    {
                        Id = tile.Config.Id,
                        ProcessName = tile.Config.ProcessName,
                        ProcessPath = tile.Config.ProcessPath,
                        Enabled = tile.Config.Enabled,
                        MuteOnHide = tile.Config.MuteOnHide,
                        FreezeOnHide = tile.Config.FreezeOnHide
                    })
                    .ToList()
            })
            .ToList();
        _settings.Targets = _settings.Groups
            .SelectMany(static group => group.Targets)
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
            Text = "BossKey",
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

    private void AddTarget(string processName, string? processPath)
    {
        var alreadyExists = EnumerateAllTargetConfigs().Any(target =>
            (!string.IsNullOrWhiteSpace(target.ProcessPath)
             && !string.IsNullOrWhiteSpace(processPath)
             && string.Equals(target.ProcessPath, processPath, StringComparison.OrdinalIgnoreCase))
            || string.Equals(target.ProcessName, processName, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            SetStatus(Localizer.T("Main.StatusDuplicate"));
            return;
        }

        var defaultGroup = FindOrCreateDefaultGroup();
        defaultGroup.Targets.Add(new TargetTileViewModel(
            new TargetAppConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                ProcessName = processName,
                ProcessPath = processPath,
                Enabled = true,
                MuteOnHide = false,
                FreezeOnHide = false
            },
            _appIconService.GetIcon(processPath)));
        defaultGroup.RefreshHotkeyText();

        PersistSettings();
        SetStatus(Localizer.Format("Main.StatusAdded", processName));
    }

    private IEnumerable<TargetAppConfig> EnumerateAllTargetConfigs()
    {
        return _groupCards.SelectMany(static group => group.Targets.Select(tile => tile.Config));
    }

    private IEnumerable<HotkeyRouteBinding> BuildHotkeyRoutes()
    {
        var bindings = _groupCards
            .Select(group => new HotkeyRouteBinding(group.Id, group.HideHotkey, group.ShowHotkey))
            .ToList();
        bindings.Add(new HotkeyRouteBinding(AllTargetsRouteId, _settings.HideHotkey, _settings.ShowHotkey));
        return bindings;
    }

    private TargetGroupViewModel BuildGroupViewModel(TargetGroupConfig group)
    {
        var tiles = group.Targets.Select(target => new TargetTileViewModel(
            new TargetAppConfig
            {
                Id = target.Id,
                ProcessName = target.ProcessName,
                ProcessPath = target.ProcessPath,
                Enabled = target.Enabled,
                MuteOnHide = target.MuteOnHide,
                FreezeOnHide = target.FreezeOnHide
            },
            _appIconService.GetIcon(target.ProcessPath)));
        return new TargetGroupViewModel(group, tiles);
    }

    private TargetGroupViewModel FindOrCreateDefaultGroup()
    {
        var defaultGroup = _defaultGroupCard ?? _groupCards.FirstOrDefault(group => group.IsDefaultGroup);
        if (defaultGroup is not null)
        {
            _defaultGroupCard = defaultGroup;
            return defaultGroup;
        }

        defaultGroup = new TargetGroupViewModel(new TargetGroupConfig
        {
            Id = TargetGroupConfig.DefaultGroupId,
            Name = string.Empty
        });
        _groupCards.Insert(0, defaultGroup);
        _defaultGroupCard = defaultGroup;
        RefreshGroupDisplayNames();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UngroupedTargets)));
        return defaultGroup;
    }

    private TargetGroupViewModel CreateNewGroup()
    {
        var group = new TargetGroupViewModel(new TargetGroupConfig
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = string.Empty
        });
        _groupCards.Add(group);
        _visibleGroupCards.Add(group);
        return group;
    }

    private void NewGroupButton_OnClick(object sender, RoutedEventArgs e)
    {
        var group = CreateNewGroup();
        RefreshGroupDisplayNames();
        _globalHotkeyService.UpdateBindings(BuildHotkeyRoutes());
        PersistSettings();
        SetStatus(Localizer.T("Main.StatusGroupCreated"));
        BeginGroupRename(group);
    }

    private void ToggleGroupCollapseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetGroupViewModel group)
        {
            return;
        }

        ToggleGroupCollapse(group);
    }

    private void RenameGroupButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetGroupViewModel group)
        {
            return;
        }

        BeginGroupRename(group);
    }

    private void GroupFolderBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2
            || sender is not FrameworkElement element
            || element.DataContext is not TargetGroupViewModel group)
        {
            return;
        }

        ToggleGroupCollapse(group);
        e.Handled = true;
    }

    private void GroupNameHost_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2
            || sender is not FrameworkElement element
            || element.DataContext is not TargetGroupViewModel group
            || group.IsEditingName)
        {
            return;
        }

        BeginGroupRename(group);
        e.Handled = true;
    }

    private void GroupNameTextBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        FocusGroupNameTextBox(sender);
    }

    private void GroupNameTextBox_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            FocusGroupNameTextBox(sender);
        }
    }

    private static void FocusGroupNameTextBox(object sender)
    {
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        textBox.Dispatcher.BeginInvoke(() =>
        {
            textBox.Focus();
            textBox.SelectAll();
        });
    }

    private void GroupNameTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetGroupViewModel group)
        {
            return;
        }

        CommitGroupRename(group);
    }

    private void GroupNameTextBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetGroupViewModel group)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitGroupRename(group);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            group.EditName = group.Name;
            group.IsEditingName = false;
            e.Handled = true;
        }
    }

    private void DeleteGroupButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetGroupViewModel group || group.IsDefaultGroup)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            Localizer.Format("Main.GroupDeletePrompt", group.DisplayName),
            Localizer.T("Main.GroupDeleteTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var defaultGroup = FindOrCreateDefaultGroup();
        _processWindowService.ShowHiddenTargets(group.Targets.Select(static tile => tile.Config), group.Id, bringToFront: false);
        foreach (var tile in group.Targets.ToList())
        {
            group.Targets.Remove(tile);
            defaultGroup.Targets.Add(tile);
        }

        _groupCards.Remove(group);
        _visibleGroupCards.Remove(group);
        defaultGroup.RefreshHotkeyText();
        RefreshGroupDisplayNames();
        _globalHotkeyService.UpdateBindings(BuildHotkeyRoutes());
        PersistSettings();
        SetStatus(Localizer.T("Main.StatusGroupDeleted"));
    }

    private void SetGroupHideHotkeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetGroupViewModel group)
        {
            return;
        }

        var dialog = new HotkeyCaptureWindow(
            Localizer.T("Main.GroupSetHideHotkey"),
            Localizer.CurrentLanguage,
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
        _globalHotkeyService.UpdateBindings(BuildHotkeyRoutes());
        PersistSettings();
    }

    private void SetGroupShowHotkeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetGroupViewModel group)
        {
            return;
        }

        var dialog = new HotkeyCaptureWindow(
            Localizer.T("Main.GroupSetShowHotkey"),
            Localizer.CurrentLanguage,
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
        _globalHotkeyService.UpdateBindings(BuildHotkeyRoutes());
        PersistSettings();
    }

    private void TargetTile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
    }

    private void TargetTile_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragStartPoint is null)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStartPoint.Value.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStartPoint.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is not FrameworkElement element || element.DataContext is not TargetTileViewModel tile)
        {
            return;
        }

        _dragStartPoint = null;
        BeginTargetTileDrag(element, tile);
    }

    private void GroupDropTarget_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        UpdateTargetTileAutoScroll(e);

        if (sender is not FrameworkElement element
            || element.DataContext is not TargetGroupViewModel group
            || !e.Data.GetDataPresent(TargetTileDragFormat))
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(TargetTileDragFormat) is not TargetTileViewModel tile
            || _groupCards.FirstOrDefault(candidate => candidate.Targets.Contains(tile)) == group)
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;
    }

    private void GroupDropTarget_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not FrameworkElement element
            || element.DataContext is not TargetGroupViewModel group
            || e.Data.GetData(TargetTileDragFormat) is not TargetTileViewModel tile)
        {
            return;
        }

        var sourceGroup = GetOwningGroup(tile);
        if (sourceGroup is null || sourceGroup == group)
        {
            return;
        }

        _processWindowService.ShowHiddenTargets([tile.Config], sourceGroup.Id, bringToFront: false);
        sourceGroup.Targets.Remove(tile);
        group.Targets.Add(tile);
        sourceGroup.RefreshHotkeyText();
        group.RefreshHotkeyText();
        PersistSettings();
        SetStatus(Localizer.Format("Main.StatusMovedToGroup", tile.ProcessName, group.DisplayName));
    }

    private void UngroupedDropTarget_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        UpdateTargetTileAutoScroll(e);

        if (!e.Data.GetDataPresent(TargetTileDragFormat))
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(TargetTileDragFormat) is not TargetTileViewModel tile
            || GetOwningGroup(tile)?.IsDefaultGroup == true)
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;
    }

    private void UngroupedDropTarget_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(TargetTileDragFormat) is not TargetTileViewModel tile)
        {
            return;
        }

        var sourceGroup = GetOwningGroup(tile);
        var defaultGroup = FindOrCreateDefaultGroup();
        if (sourceGroup is null || sourceGroup == defaultGroup)
        {
            return;
        }

        _processWindowService.ShowHiddenTargets([tile.Config], sourceGroup.Id, bringToFront: false);
        sourceGroup.Targets.Remove(tile);
        defaultGroup.Targets.Add(tile);
        sourceGroup.RefreshHotkeyText();
        defaultGroup.RefreshHotkeyText();
        PersistSettings();
        SetStatus(Localizer.Format("Main.StatusMovedToGroup", tile.ProcessName, Localizer.T("Main.Ungrouped")));
    }

    private void TargetsScrollViewer_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        UpdateTargetTileAutoScroll(e);
    }

    private void TargetsScrollViewer_PreviewDragLeave(object sender, System.Windows.DragEventArgs e)
    {
        _dragScrollViewerPosition = null;
    }

    private void BeginTargetTileDrag(FrameworkElement sourceElement, TargetTileViewModel tile)
    {
        System.Windows.GiveFeedbackEventHandler feedbackHandler = TargetTileDragSource_GiveFeedback;
        System.Windows.QueryContinueDragEventHandler queryContinueDragHandler = TargetTileDragSource_QueryContinueDrag;

        sourceElement.GiveFeedback += feedbackHandler;
        sourceElement.QueryContinueDrag += queryContinueDragHandler;
        _dragScrollViewerPosition = null;
        ShowTargetTileDragPreview(sourceElement);
        _targetTileAutoScrollTimer.Start();

        try
        {
            DragDrop.DoDragDrop(
                sourceElement,
                new System.Windows.DataObject(TargetTileDragFormat, tile),
                System.Windows.DragDropEffects.Move);
        }
        finally
        {
            sourceElement.GiveFeedback -= feedbackHandler;
            sourceElement.QueryContinueDrag -= queryContinueDragHandler;
            EndTargetTileDrag();
        }
    }

    private void TargetTileDragSource_GiveFeedback(object? sender, System.Windows.GiveFeedbackEventArgs e)
    {
        UpdateTargetTileDragPreviewPosition();
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    private void TargetTileDragSource_QueryContinueDrag(object? sender, System.Windows.QueryContinueDragEventArgs e)
    {
        UpdateTargetTileDragPreviewPosition();
    }

    private void ShowTargetTileDragPreview(FrameworkElement sourceElement)
    {
        var previewSource = CreateTargetTileDragPreview(sourceElement);
        if (previewSource is null)
        {
            return;
        }

        _targetTileDragPreviewWindow?.Close();
        _targetTileDragPreviewWindow = new TargetTileDragPreviewWindow(
            previewSource,
            sourceElement.ActualWidth,
            sourceElement.ActualHeight);
        _targetTileDragPreviewWindow.Show();
        UpdateTargetTileDragPreviewPosition();
    }

    private void UpdateTargetTileDragPreviewPosition()
    {
        if (_targetTileDragPreviewWindow is null)
        {
            return;
        }

        var cursorPosition = Forms.Cursor.Position;
        _targetTileDragPreviewWindow.UpdatePosition(cursorPosition.X, cursorPosition.Y);
    }

    private void EndTargetTileDrag()
    {
        _targetTileAutoScrollTimer.Stop();
        _dragScrollViewerPosition = null;

        if (_targetTileDragPreviewWindow is not null)
        {
            _targetTileDragPreviewWindow.Close();
            _targetTileDragPreviewWindow = null;
        }
    }

    private void TargetTileAutoScrollTimer_OnTick(object? sender, EventArgs e)
    {
        if (_dragScrollViewerPosition is not System.Windows.Point dragPosition || TargetsScrollViewer is null)
        {
            return;
        }

        AutoScrollTargetsScrollViewer(TargetsScrollViewer, dragPosition);
        UpdateTargetTileDragPreviewPosition();
    }

    private void UpdateTargetTileAutoScroll(System.Windows.DragEventArgs e)
    {
        if (TargetsScrollViewer is null || !e.Data.GetDataPresent(TargetTileDragFormat))
        {
            _dragScrollViewerPosition = null;
            return;
        }

        _dragScrollViewerPosition = e.GetPosition(TargetsScrollViewer);
        AutoScrollTargetsScrollViewer(TargetsScrollViewer, _dragScrollViewerPosition.Value);
        UpdateTargetTileDragPreviewPosition();
    }

    private static void AutoScrollTargetsScrollViewer(ScrollViewer scrollViewer, System.Windows.Point cursorPosition)
    {
        if (scrollViewer.ScrollableHeight <= 0 || scrollViewer.ActualHeight <= 0)
        {
            return;
        }

        double delta = 0d;
        if (cursorPosition.Y < TargetTileAutoScrollEdgeThreshold)
        {
            var intensity = (TargetTileAutoScrollEdgeThreshold - Math.Max(0d, cursorPosition.Y)) / TargetTileAutoScrollEdgeThreshold;
            delta = -Math.Max(2d, intensity * TargetTileAutoScrollMaxSpeed);
        }
        else if (cursorPosition.Y > scrollViewer.ActualHeight - TargetTileAutoScrollEdgeThreshold)
        {
            var distanceToBottom = Math.Max(0d, scrollViewer.ActualHeight - cursorPosition.Y);
            var intensity = (TargetTileAutoScrollEdgeThreshold - distanceToBottom) / TargetTileAutoScrollEdgeThreshold;
            delta = Math.Max(2d, intensity * TargetTileAutoScrollMaxSpeed);
        }

        if (Math.Abs(delta) < double.Epsilon)
        {
            return;
        }

        var nextOffset = Math.Clamp(scrollViewer.VerticalOffset + delta, 0d, scrollViewer.ScrollableHeight);
        scrollViewer.ScrollToVerticalOffset(nextOffset);
    }

    private static ImageSource? CreateTargetTileDragPreview(FrameworkElement sourceElement)
    {
        if (sourceElement.ActualWidth <= 0 || sourceElement.ActualHeight <= 0)
        {
            return null;
        }

        var size = new System.Windows.Size(sourceElement.ActualWidth, sourceElement.ActualHeight);
        var dpi = VisualTreeHelper.GetDpi(sourceElement);
        var renderTarget = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(size.Width * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(size.Height * dpi.DpiScaleY)),
            96d * dpi.DpiScaleX,
            96d * dpi.DpiScaleY,
            PixelFormats.Pbgra32);

        var drawingVisual = new DrawingVisual();
        using (var drawingContext = drawingVisual.RenderOpen())
        {
            drawingContext.DrawRectangle(new VisualBrush(sourceElement), null, new Rect(new System.Windows.Point(0, 0), size));
        }

        renderTarget.Render(drawingVisual);
        renderTarget.Freeze();
        return renderTarget;
    }

    private void ToggleTargetEnabledMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetTileViewModel tile)
        {
            return;
        }

        if (element is System.Windows.Controls.MenuItem menuItem)
        {
            tile.Enabled = menuItem.IsChecked;
        }

        PersistSettings();
    }

    private void ToggleTargetMuteMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetTileViewModel tile)
        {
            return;
        }

        if (element is System.Windows.Controls.MenuItem menuItem)
        {
            tile.MuteOnHide = menuItem.IsChecked;
        }

        PersistSettings();
    }

    private void ToggleTargetFreezeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetTileViewModel tile)
        {
            return;
        }

        if (element is System.Windows.Controls.MenuItem menuItem)
        {
            tile.FreezeOnHide = menuItem.IsChecked;
        }

        PersistSettings();
    }

    private void RemoveTargetMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TargetTileViewModel tile)
        {
            return;
        }

        var sourceGroup = GetOwningGroup(tile);
        if (sourceGroup is null)
        {
            return;
        }

        _processWindowService.ShowHiddenTargets([tile.Config], sourceGroup.Id, bringToFront: false);
        sourceGroup.Targets.Remove(tile);
        sourceGroup.RefreshHotkeyText();
        PersistSettings();
        SetStatus(Localizer.Format("Main.StatusRemoved", tile.ProcessName));
    }

    private void BeginGroupRename(TargetGroupViewModel group)
    {
        group.EditName = !string.IsNullOrWhiteSpace(group.Name)
            ? group.Name
            : group.DisplayName;
        group.IsEditingName = true;
    }

    private void ToggleGroupCollapse(TargetGroupViewModel group)
    {
        group.IsCollapsed = !group.IsCollapsed;
        PersistSettings();
    }

    private void CommitGroupRename(TargetGroupViewModel group)
    {
        var newName = group.EditName.Trim();
        group.Name = newName;
        group.IsEditingName = false;
        RefreshGroupDisplayNames();
        PersistSettings();
        SetStatus(Localizer.T("Main.StatusGroupRenamed"));
    }

    private void RefreshGroupDisplayNames()
    {
        foreach (var group in _groupCards)
        {
            group.DisplayName = !string.IsNullOrWhiteSpace(group.Name)
                ? group.Name
                : group.IsDefaultGroup
                    ? Localizer.T("Main.Ungrouped")
                    : Localizer.T("Main.NewGroup");
        }
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
        PickWindowButtonTextBlock.Text = Localizer.T("Main.PickWindow");
        HideNowButtonTextBlock.Text = Localizer.T("Main.HideNow");
        ShowNowButtonTextBlock.Text = Localizer.T("Main.ShowNow");
        OpenSettingsButtonTextBlock.Text = Localizer.T("Main.OpenSettings");
        LogTitleTextBlock.Text = Localizer.T("Main.LogTitle");
        ClearLogsButtonTextBlock.Text = Localizer.T("Main.ClearLogs");
        RemoveButtonText = Localizer.T("Main.Remove");
        NewGroupButtonText = Localizer.T("Main.NewGroup");
        TargetEnabledText = Localizer.T("Main.TargetEnabled");
        TargetMuteOnHideText = Localizer.T("Main.TargetMuteOnHide");
        TargetFreezeOnHideText = Localizer.T("Main.TargetFreezeOnHide");
        RenameGroupText = Localizer.T("Main.GroupRename");
        DeleteGroupText = Localizer.T("Main.GroupDelete");
        ToggleGroupText = Localizer.T("Main.GroupToggle");
        SetGroupHideHotkeyText = Localizer.T("Main.GroupSetHideHotkey");
        SetGroupShowHotkeyText = Localizer.T("Main.GroupSetShowHotkey");
        EmptyGroupHintText = Localizer.T("Main.EmptyGroupHint");
        UngroupedDropHintText = Localizer.T("Main.UngroupedDropHint");
        UpdateDownloadTitleText = Localizer.T("Update.ProgressTitle");

        var hideKeys = _settings.HideHotkey.GetNormalizedKeys();
        var showKeys = _settings.ShowHotkey.GetNormalizedKeys();
        var hideText = HotkeyFormatter.Format(_settings.HideHotkey);
        var showText = HotkeyFormatter.Format(_settings.ShowHotkey);
        HotkeyHintTextBlock.Text = hideKeys.Count > 0 && hideKeys.SetEquals(showKeys)
            ? Localizer.Format("Main.ToggleHint", hideText)
            : Localizer.Format("Main.HotkeyHint", hideText, showText);

        RefreshGroupDisplayNames();
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

    private async Task RefreshLanguageCatalogInBackgroundAsync()
    {
        await Localizer.RefreshRemoteCatalogAsync();
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

    internal Task<bool> CheckForUpdatesFromSettingsAsync(Window owner)
    {
        return CheckForUpdatesAsync(manualCheck: true, owner);
    }

    private async Task<bool> CheckForUpdatesAsync(bool manualCheck, Window? dialogOwner = null)
    {
        if (_isCheckingUpdates)
        {
            return false;
        }

        if (!manualCheck)
        {
            if (!_settings.AutoCheckForUpdates)
            {
                return false;
            }

            if (_settings.LastUpdateCheckUtc is DateTime lastCheckUtc
                && DateTime.UtcNow - lastCheckUtc < AutoUpdateCheckInterval)
            {
                return false;
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

                return false;
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

                return false;
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
                return false;
            }

            if (string.IsNullOrWhiteSpace(result.InstallerDownloadUrl))
            {
                if (!string.IsNullOrWhiteSpace(result.ReleasePageUrl))
                {
                    Process.Start(new ProcessStartInfo(result.ReleasePageUrl) { UseShellExecute = true });
                    SetStatus(Localizer.T("Update.StatusOpenedReleasePage"));
                    return false;
                }

                System.Windows.MessageBox.Show(
                    dialogOwner ?? this,
                    Localizer.T("Update.NoInstallerAsset"),
                    Localizer.T("Update.CheckFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            try
            {
                if (dialogOwner is not null && dialogOwner != this)
                {
                    dialogOwner.Close();
                }

                SetStatus(Localizer.T("Update.StatusDownloading"));
                ShowUpdateDownloadOverlay();
                var progress = new Progress<double>(UpdateDownloadProgress);
                var installerPath = await _appUpdateService.DownloadInstallerAsync(result.InstallerDownloadUrl, result.ReleaseTag, progress);
                if (_currentPackageType == UpdatePackageType.SingleFile)
                {
                    BeginSingleFileSelfUpdate(installerPath);
                    SetStatus(Localizer.T("Update.StatusApplyingSingleFile"));
                    _allowClose = true;
                    Close();
                    return true;
                }

                Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
                SetStatus(Localizer.T("Update.StatusStartingInstaller"));
                _allowClose = true;
                Close();
                return true;
            }
            catch (Exception ex)
            {
                HideUpdateDownloadOverlay();
                SetStatus(Localizer.Format("Update.StatusDownloadFailed", ex.Message));
                System.Windows.MessageBox.Show(
                    dialogOwner ?? this,
                    Localizer.Format("Update.DownloadFailed", ex.Message),
                    Localizer.T("Update.CheckFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
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

    private void ShowUpdateDownloadOverlay()
    {
        UpdateDownloadProgress(0d);
        UpdateDownloadOverlayVisibility = Visibility.Visible;
    }

    private void HideUpdateDownloadOverlay()
    {
        UpdateDownloadOverlayVisibility = Visibility.Collapsed;
        UpdateDownloadProgress(0d);
    }

    private void UpdateDownloadProgress(double value)
    {
        var progress = Math.Clamp(value, 0d, 1d);
        UpdateDownloadProgressText = $"{Math.Round(progress * 100):0}%";
        UpdateDownloadArcData = BuildProgressArcData(progress);
    }

    private static string BuildProgressArcData(double progress)
    {
        const double center = 60d;
        const double radius = 52d;

        if (progress <= 0d)
        {
            return string.Empty;
        }

        if (progress >= 0.9999d)
        {
            return $"M {center},{center - radius} A {radius},{radius} 0 1 1 {center},{center + radius} A {radius},{radius} 0 1 1 {center},{center - radius}";
        }

        var angle = -90d + progress * 360d;
        var radians = angle * Math.PI / 180d;
        var endX = center + radius * Math.Cos(radians);
        var endY = center + radius * Math.Sin(radians);
        var largeArcFlag = progress >= 0.5d ? 1 : 0;

        return $"M {center},{center - radius} A {radius},{radius} 0 {largeArcFlag} 1 {endX:F3},{endY:F3}";
    }

    private static void BeginSingleFileSelfUpdate(string downloadedExecutablePath)
    {
        var currentExecutablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExecutablePath))
        {
            throw new InvalidOperationException("Current executable path is unavailable.");
        }

        var updatesDirectory = Path.Combine(Path.GetTempPath(), "BossKey", "Updates");
        Directory.CreateDirectory(updatesDirectory);

        var scriptPath = Path.Combine(updatesDirectory, $"apply-update-{Guid.NewGuid():N}.cmd");
        var scriptContent = string.Join(
            Environment.NewLine,
            "@echo off",
            "setlocal",
            $"set \"SOURCE={EscapeBatchValue(downloadedExecutablePath)}\"",
            $"set \"TARGET={EscapeBatchValue(currentExecutablePath)}\"",
            ":replace",
            "move /Y \"%SOURCE%\" \"%TARGET%\" >nul 2>&1",
            "if errorlevel 1 (",
            "  timeout /t 1 /nobreak >nul",
            "  goto replace",
            ")",
            "start \"\" \"%TARGET%\"",
            "del \"%~f0\"");

        File.WriteAllText(scriptPath, scriptContent);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"\"{scriptPath}\"\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static string EscapeBatchValue(string value)
    {
        return value.Replace("%", "%%", StringComparison.Ordinal);
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
