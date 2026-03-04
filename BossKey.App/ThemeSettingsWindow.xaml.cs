using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BossKey.App.Localization;
using BossKey.App.Services;
using BossKey.Core.Models;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace BossKey.App;

public partial class ThemeSettingsWindow : Window
{
    private readonly string _languageCode;
    private readonly ThemeSettings _originalTheme;
    private readonly ObservableCollection<ThemeColorEditorItem> _colorItems = [];
    private ThemeSettings _workingTheme;
    private bool _isCommitted;

    public ThemeSettings UpdatedTheme { get; private set; }

    public ThemeSettingsWindow(ThemeSettings theme, string languageCode)
    {
        InitializeComponent();
        _languageCode = Localizer.NormalizeLanguage(languageCode);
        _originalTheme = ThemeSettings.Normalize(theme).Clone();
        _workingTheme = _originalTheme.Clone();
        UpdatedTheme = _originalTheme.Clone();
        ColorItemsControl.ItemsSource = _colorItems;
        ApplyLocalization();
        RebuildColorItems();
        ApplyWorkingTheme();
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void LightModeButton_OnClick(object sender, RoutedEventArgs e)
    {
        SelectMode(ThemeModes.Light);
    }

    private void DarkModeButton_OnClick(object sender, RoutedEventArgs e)
    {
        SelectMode(ThemeModes.Dark);
    }

    private void ResetCurrentModeButton_OnClick(object sender, RoutedEventArgs e)
    {
        var mode = ThemeModes.Normalize(_workingTheme.ActiveMode);
        var defaults = string.Equals(mode, ThemeModes.Dark, StringComparison.Ordinal)
            ? ThemePalette.CreateDefaultDark()
            : ThemePalette.CreateDefaultLight();

        if (string.Equals(mode, ThemeModes.Dark, StringComparison.Ordinal))
        {
            _workingTheme.DarkPalette = defaults;
        }
        else
        {
            _workingTheme.LightPalette = defaults;
        }

        RebuildColorItems();
        ApplyWorkingTheme();
    }

    private void ResetAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        var activeMode = ThemeModes.Normalize(_workingTheme.ActiveMode);
        _workingTheme = ThemeSettings.CreateDefault();
        _workingTheme.ActiveMode = activeMode;
        RebuildColorItems();
        ApplyWorkingTheme();
    }

    private void PickColorButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ThemeColorEditorItem item })
        {
            return;
        }

        var originalValue = ThemePalette.NormalizeColor(item.Value, item.DefaultValue);
        var pickerWindow = new ColorPickerWindow(originalValue, item.DisplayName, _languageCode)
        {
            Owner = this
        };

        if (pickerWindow.ShowDialog() == true)
        {
            item.Value = pickerWindow.SelectedColor;
        }
    }

    private void ResetColorButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ThemeColorEditorItem item })
        {
            item.Value = item.DefaultValue;
        }
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_colorItems.Any(static item => !item.IsValid))
        {
            return;
        }

        NormalizeEditorValues();
        _workingTheme = ThemeSettings.Normalize(_workingTheme);
        UpdatedTheme = _workingTheme.Clone();
        _isCommitted = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        CancelAndClose();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        CancelAndClose();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_isCommitted)
        {
            ThemeManager.ApplyTheme(_originalTheme);
        }

        base.OnClosed(e);
    }

    private void ApplyLocalization()
    {
        Title = Localizer.T("Theme.Title", _languageCode);
        HeaderTextBlock.Text = Localizer.T("Theme.Header", _languageCode);
        ThemeModeLabelTextBlock.Text = Localizer.T("Theme.Mode", _languageCode);
        LightModeButton.Content = Localizer.T("Theme.Light", _languageCode);
        DarkModeButton.Content = Localizer.T("Theme.Dark", _languageCode);
        ThemeHintTextBlock.Text = Localizer.T("Theme.Hint", _languageCode);
        ThemeSubHintTextBlock.Text = Localizer.T("Theme.SubHint", _languageCode);
        ResetCurrentModeButton.Content = Localizer.T("Theme.ResetCurrent", _languageCode);
        ResetAllButton.Content = Localizer.T("Theme.ResetAll", _languageCode);
        ColorColumnLabelTextBlock.Text = Localizer.T("Theme.ColumnColor", _languageCode);
        SwatchColumnLabelTextBlock.Text = Localizer.T("Theme.ColumnSwatch", _languageCode);
        HexColumnLabelTextBlock.Text = Localizer.T("Theme.ColumnHex", _languageCode);
        ValidationTextBlock.Text = Localizer.T("Theme.InvalidColor", _languageCode);
        PreviewLabelTextBlock.Text = Localizer.T("Theme.Preview", _languageCode);
        PreviewHintTextBlock.Text = Localizer.T("Theme.PreviewHint", _languageCode);
        PreviewWindowTitleTextBlock.Text = Localizer.T("Theme.PreviewWindowTitle", _languageCode);
        PreviewCardTitleTextBlock.Text = Localizer.T("Theme.PreviewCardTitle", _languageCode);
        PreviewCardSubtitleTextBlock.Text = Localizer.T("Theme.PreviewCardBody", _languageCode);
        PreviewNeutralButton.Content = Localizer.T("Theme.PreviewNeutralButton", _languageCode);
        PreviewPrimaryButton.Content = Localizer.T("Theme.PreviewPrimaryButton", _languageCode);
        PreviewWarningBadgeTextBlock.Text = Localizer.T("Theme.PreviewWarningBadge", _languageCode);
        PreviewInputTextBlock.Text = "Not Set";
        PreviewTileStatusBadgeBorder.ToolTip = $"{Localizer.T("Theme.PreviewFreezeBadge", _languageCode)} / {Localizer.T("Theme.PreviewTopMostBadge", _languageCode)}";
        FooterHintTextBlock.Text = Localizer.T("Theme.FooterHint", _languageCode);
        CancelButton.Content = Localizer.T("Settings.Cancel", _languageCode);
        SaveButton.Content = Localizer.T("Settings.Save", _languageCode);
    }

    private void SelectMode(string mode)
    {
        var normalized = ThemeModes.Normalize(mode);
        if (string.Equals(_workingTheme.ActiveMode, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _workingTheme.ActiveMode = normalized;
        RebuildColorItems();
        ApplyWorkingTheme();
    }

    private void RebuildColorItems()
    {
        foreach (var item in _colorItems)
        {
            item.PropertyChanged -= ColorItem_OnPropertyChanged;
        }

        _colorItems.Clear();
        var mode = ThemeModes.Normalize(_workingTheme.ActiveMode);
        var palette = _workingTheme.GetPalette(mode);
        var defaults = string.Equals(mode, ThemeModes.Dark, StringComparison.Ordinal)
            ? ThemePalette.CreateDefaultDark()
            : ThemePalette.CreateDefaultLight();

        AddColorItem(nameof(ThemePalette.WindowBackgroundColor), Localizer.T("Theme.Color.WindowBackground", _languageCode), Localizer.T("Theme.Color.WindowBackgroundDesc", _languageCode), palette.WindowBackgroundColor, defaults.WindowBackgroundColor);
        AddColorItem(nameof(ThemePalette.ChromeBackgroundColor), Localizer.T("Theme.Color.ChromeBackground", _languageCode), Localizer.T("Theme.Color.ChromeBackgroundDesc", _languageCode), palette.ChromeBackgroundColor, defaults.ChromeBackgroundColor);
        AddColorItem(nameof(ThemePalette.SurfaceBackgroundColor), Localizer.T("Theme.Color.SurfaceBackground", _languageCode), Localizer.T("Theme.Color.SurfaceBackgroundDesc", _languageCode), palette.SurfaceBackgroundColor, defaults.SurfaceBackgroundColor);
        AddColorItem(nameof(ThemePalette.PanelBackgroundColor), Localizer.T("Theme.Color.PanelBackground", _languageCode), Localizer.T("Theme.Color.PanelBackgroundDesc", _languageCode), palette.PanelBackgroundColor, defaults.PanelBackgroundColor);
        AddColorItem(nameof(ThemePalette.BorderColor), Localizer.T("Theme.Color.Border", _languageCode), Localizer.T("Theme.Color.BorderDesc", _languageCode), palette.BorderColor, defaults.BorderColor);
        AddColorItem(nameof(ThemePalette.PrimaryTextColor), Localizer.T("Theme.Color.PrimaryText", _languageCode), Localizer.T("Theme.Color.PrimaryTextDesc", _languageCode), palette.PrimaryTextColor, defaults.PrimaryTextColor);
        AddColorItem(nameof(ThemePalette.SecondaryTextColor), Localizer.T("Theme.Color.SecondaryText", _languageCode), Localizer.T("Theme.Color.SecondaryTextDesc", _languageCode), palette.SecondaryTextColor, defaults.SecondaryTextColor);
        AddColorItem(nameof(ThemePalette.AccentColor), Localizer.T("Theme.Color.Accent", _languageCode), Localizer.T("Theme.Color.AccentDesc", _languageCode), palette.AccentColor, defaults.AccentColor);
        AddColorItem(nameof(ThemePalette.GroupIconColor), Localizer.T("Theme.Color.GroupIcon", _languageCode), Localizer.T("Theme.Color.GroupIconDesc", _languageCode), palette.GroupIconColor, defaults.GroupIconColor);
        AddColorItem(nameof(ThemePalette.SuccessColor), Localizer.T("Theme.Color.Success", _languageCode), Localizer.T("Theme.Color.SuccessDesc", _languageCode), palette.SuccessColor, defaults.SuccessColor);
        AddColorItem(nameof(ThemePalette.WarningColor), Localizer.T("Theme.Color.Warning", _languageCode), Localizer.T("Theme.Color.WarningDesc", _languageCode), palette.WarningColor, defaults.WarningColor);

        UpdateModeButtons();
        UpdateValidationState();
    }

    private void AddColorItem(string propertyName, string displayName, string description, string value, string defaultValue)
    {
        var item = new ThemeColorEditorItem(
            propertyName,
            displayName,
            description,
            ThemePalette.NormalizeColor(value, defaultValue),
            defaultValue,
            Localizer.T("Theme.Pick", _languageCode),
            Localizer.T("Theme.Reset", _languageCode));
        item.PropertyChanged += ColorItem_OnPropertyChanged;
        _colorItems.Add(item);
    }

    private void ColorItem_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ThemeColorEditorItem item || !string.Equals(e.PropertyName, nameof(ThemeColorEditorItem.Value), StringComparison.Ordinal))
        {
            return;
        }

        if (!ThemePalette.TryNormalizeColor(item.Value, out var normalized))
        {
            item.IsValid = false;
            UpdateValidationState();
            return;
        }

        item.IsValid = true;
        item.PreviewBrush = CreateBrush(normalized);
        SetPaletteValue(_workingTheme.GetPalette(_workingTheme.ActiveMode), item.PropertyName, normalized);
        ApplyWorkingTheme();
    }

    private void ApplyWorkingTheme()
    {
        ThemeManager.ApplyTheme(_workingTheme);
        UpdateModeButtons();
        UpdateValidationState();
    }

    private void UpdateModeButtons()
    {
        var isDark = string.Equals(ThemeModes.Normalize(_workingTheme.ActiveMode), ThemeModes.Dark, StringComparison.Ordinal);
        LightModeButton.Style = (Style)FindResource(isDark ? "NeutralButtonStyle" : "PrimaryButtonStyle");
        DarkModeButton.Style = (Style)FindResource(isDark ? "PrimaryButtonStyle" : "NeutralButtonStyle");
        LightModeButton.FontWeight = FontWeights.SemiBold;
        DarkModeButton.FontWeight = FontWeights.SemiBold;
    }

    private void UpdateValidationState()
    {
        var hasInvalidColor = _colorItems.Any(static item => !item.IsValid);
        ValidationTextBlock.Visibility = hasInvalidColor ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = !hasInvalidColor;
    }

    private void NormalizeEditorValues()
    {
        foreach (var item in _colorItems)
        {
            if (!ThemePalette.TryNormalizeColor(item.Value, out var normalized))
            {
                continue;
            }

            item.Value = normalized;
        }
    }

    private void CancelAndClose()
    {
        _isCommitted = false;
        DialogResult = false;
        Close();
    }

    private static void SetPaletteValue(ThemePalette palette, string propertyName, string value)
    {
        switch (propertyName)
        {
            case nameof(ThemePalette.WindowBackgroundColor):
                palette.WindowBackgroundColor = value;
                break;
            case nameof(ThemePalette.ChromeBackgroundColor):
                palette.ChromeBackgroundColor = value;
                break;
            case nameof(ThemePalette.SurfaceBackgroundColor):
                palette.SurfaceBackgroundColor = value;
                break;
            case nameof(ThemePalette.PanelBackgroundColor):
                palette.PanelBackgroundColor = value;
                break;
            case nameof(ThemePalette.BorderColor):
                palette.BorderColor = value;
                break;
            case nameof(ThemePalette.PrimaryTextColor):
                palette.PrimaryTextColor = value;
                break;
            case nameof(ThemePalette.SecondaryTextColor):
                palette.SecondaryTextColor = value;
                break;
            case nameof(ThemePalette.AccentColor):
                palette.AccentColor = value;
                break;
            case nameof(ThemePalette.GroupIconColor):
                palette.GroupIconColor = value;
                break;
            case nameof(ThemePalette.SuccessColor):
                palette.SuccessColor = value;
                break;
            case nameof(ThemePalette.WarningColor):
                palette.WarningColor = value;
                break;
        }
    }

    private static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(color)!);
        brush.Freeze();
        return brush;
    }

    private sealed class ThemeColorEditorItem : INotifyPropertyChanged
    {
        private string _value;
        private SolidColorBrush _previewBrush;
        private bool _isValid = true;

        public ThemeColorEditorItem(string propertyName, string displayName, string description, string value, string defaultValue, string pickText, string resetText)
        {
            PropertyName = propertyName;
            DisplayName = displayName;
            Description = description;
            _value = value;
            DefaultValue = defaultValue;
            PickText = pickText;
            ResetText = resetText;
            _previewBrush = CreateBrush(value);
        }

        public string PropertyName { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string DefaultValue { get; }
        public string PickText { get; }
        public string ResetText { get; }

        public string Value
        {
            get => _value;
            set
            {
                if (string.Equals(_value, value, StringComparison.Ordinal))
                {
                    return;
                }

                _value = value;
                OnPropertyChanged();
            }
        }

        public SolidColorBrush PreviewBrush
        {
            get => _previewBrush;
            set
            {
                _previewBrush = value;
                OnPropertyChanged();
            }
        }

        public bool IsValid
        {
            get => _isValid;
            set
            {
                if (_isValid == value)
                {
                    return;
                }

                _isValid = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
