using System.ComponentModel;
using System.Windows.Media;
using BossKey.Core.Models;

namespace BossKey.App.Models;

public sealed class TargetTileViewModel : INotifyPropertyChanged
{
    private readonly TargetAppConfig _config;
    private ImageSource _iconSource;

    public TargetTileViewModel(TargetAppConfig config, ImageSource iconSource)
    {
        _config = config;
        _iconSource = iconSource;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TargetAppConfig Config => _config;
    public string Id => _config.Id;
    public string ProcessName => _config.ProcessName;
    public string? ProcessPath => _config.ProcessPath;

    public bool Enabled
    {
        get => _config.Enabled;
        set
        {
            if (_config.Enabled == value)
            {
                return;
            }

            _config.Enabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
        }
    }

    public bool MuteOnHide
    {
        get => _config.MuteOnHide;
        set
        {
            if (_config.MuteOnHide == value)
            {
                return;
            }

            _config.MuteOnHide = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MuteOnHide)));
        }
    }

    public ImageSource IconSource
    {
        get => _iconSource;
        set
        {
            if (ReferenceEquals(_iconSource, value))
            {
                return;
            }

            _iconSource = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconSource)));
        }
    }
}
