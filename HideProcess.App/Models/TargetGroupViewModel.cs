using System.Collections.ObjectModel;
using System.ComponentModel;
using HideProcess.Core.Models;
using HideProcess.Core.Services;

namespace HideProcess.App.Models;

public sealed class TargetGroupViewModel : INotifyPropertyChanged
{
    private readonly TargetGroupConfig _config;
    private bool _isEditingName;
    private string _displayName = string.Empty;
    private string _editName = string.Empty;
    private string _hideHotkeyText = string.Empty;
    private string _showHotkeyText = string.Empty;

    public TargetGroupViewModel(TargetGroupConfig config, IEnumerable<TargetTileViewModel>? tiles = null)
    {
        _config = config;
        Targets = tiles is null
            ? []
            : new ObservableCollection<TargetTileViewModel>(tiles);
        _editName = config.Name;
        RefreshHotkeyText();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TargetGroupConfig Config => _config;
    public ObservableCollection<TargetTileViewModel> Targets { get; }
    public string Id => _config.Id;
    public bool IsDefaultGroup => string.Equals(_config.Id, TargetGroupConfig.DefaultGroupId, StringComparison.OrdinalIgnoreCase);

    public string Name
    {
        get => _config.Name;
        set
        {
            if (string.Equals(_config.Name, value, StringComparison.Ordinal))
            {
                return;
            }

            _config.Name = value;
            _editName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (string.Equals(_displayName, value, StringComparison.Ordinal))
            {
                return;
            }

            _displayName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }
    }

    public string EditName
    {
        get => _editName;
        set
        {
            if (string.Equals(_editName, value, StringComparison.Ordinal))
            {
                return;
            }

            _editName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditName)));
        }
    }

    public bool IsEditingName
    {
        get => _isEditingName;
        set
        {
            if (_isEditingName == value)
            {
                return;
            }

            _isEditingName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditingName)));
        }
    }

    public bool IsCollapsed
    {
        get => _config.IsCollapsed;
        set
        {
            if (_config.IsCollapsed == value)
            {
                return;
            }

            _config.IsCollapsed = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCollapsed)));
        }
    }

    public HotkeyBinding HideHotkey
    {
        get => _config.HideHotkey;
        set
        {
            _config.HideHotkey = value;
            RefreshHotkeyText();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HideHotkey)));
        }
    }

    public HotkeyBinding ShowHotkey
    {
        get => _config.ShowHotkey;
        set
        {
            _config.ShowHotkey = value;
            RefreshHotkeyText();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowHotkey)));
        }
    }

    public string HideHotkeyText
    {
        get => _hideHotkeyText;
        private set
        {
            if (string.Equals(_hideHotkeyText, value, StringComparison.Ordinal))
            {
                return;
            }

            _hideHotkeyText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HideHotkeyText)));
        }
    }

    public string ShowHotkeyText
    {
        get => _showHotkeyText;
        private set
        {
            if (string.Equals(_showHotkeyText, value, StringComparison.Ordinal))
            {
                return;
            }

            _showHotkeyText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowHotkeyText)));
        }
    }

    public int TargetCount => Targets.Count;

    public void RefreshHotkeyText()
    {
        HideHotkeyText = HotkeyFormatter.Format(_config.HideHotkey);
        ShowHotkeyText = HotkeyFormatter.Format(_config.ShowHotkey);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetCount)));
    }
}
