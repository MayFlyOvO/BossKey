using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BossKey.App.Localization;
using BossKey.Core.Models;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using MediaColor = System.Windows.Media.Color;
using MediaPoint = System.Windows.Point;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace BossKey.App;

public partial class ColorPickerWindow : Window
{
    private static readonly string[] PresetColors =
    [
        "#FF264653",
        "#FF2A9D8F",
        "#FFE9C46A",
        "#FFF4A261",
        "#FFE76F51",
        "#FFE5252A",
        "#FF114B9B",
        "#FF1479BE",
        "#FF1399CF",
        "#FF14B5D4",
        "#FF4CC9F0"
    ];

    private readonly string _languageCode;
    private DragSurface _dragSurface;
    private bool _suppressHexChange;
    private double _hue;
    private double _saturation;
    private double _value;
    private double _alpha;

    public string SelectedColor { get; private set; }

    public ColorPickerWindow(string initialColor, string displayName, string languageCode)
    {
        InitializeComponent();
        _languageCode = Localizer.NormalizeLanguage(languageCode);
        HeaderTextBlock.Text = displayName;
        Title = displayName;
        CancelButton.Content = Localizer.T("Settings.Cancel", _languageCode);
        OkButton.Content = Localizer.T("Common.Ok", _languageCode);
        SelectedColor = ThemePalette.NormalizeColor(initialColor, "#FFFFFFFF");
        ApplyColor(SelectedColor);
        BuildPresetButtons();
        Loaded += (_, _) => RefreshVisuals();
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

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ThemePalette.TryNormalizeColor(HexTextBox.Text, out var normalized))
        {
            return;
        }

        SelectedColor = normalized;
        DialogResult = true;
        Close();
    }

    private void ColorFieldHost_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragSurface = DragSurface.ColorField;
        ColorFieldHost.CaptureMouse();
        UpdateColorField(e.GetPosition(ColorFieldHost));
    }

    private void ColorFieldHost_OnMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_dragSurface == DragSurface.ColorField && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateColorField(e.GetPosition(ColorFieldHost));
        }
    }

    private void HueSliderHost_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragSurface = DragSurface.HueSlider;
        HueSliderHost.CaptureMouse();
        UpdateHue(e.GetPosition(HueSliderHost));
    }

    private void HueSliderHost_OnMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_dragSurface == DragSurface.HueSlider && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateHue(e.GetPosition(HueSliderHost));
        }
    }

    private void AlphaSliderHost_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragSurface = DragSurface.AlphaSlider;
        AlphaSliderHost.CaptureMouse();
        UpdateAlpha(e.GetPosition(AlphaSliderHost));
    }

    private void AlphaSliderHost_OnMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_dragSurface == DragSurface.AlphaSlider && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateAlpha(e.GetPosition(AlphaSliderHost));
        }
    }

    private void PointerSurface_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseActiveCapture();
    }

    private void PointerSurface_OnLostMouseCapture(object sender, WpfMouseEventArgs e)
    {
        _dragSurface = DragSurface.None;
    }

    private void InteractiveSurface_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshSelectorPositions();
    }

    private void HexTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressHexChange)
        {
            return;
        }

        if (!ThemePalette.TryNormalizeColor(HexTextBox.Text, out var normalized))
        {
            HexInputBorder.BorderBrush = (WpfBrush)FindResource("Theme.WarningBrush");
            OkButton.IsEnabled = false;
            return;
        }

        HexInputBorder.BorderBrush = (WpfBrush)FindResource("Theme.InputFocusBorderBrush");
        OkButton.IsEnabled = true;
        ApplyColor(normalized);
    }

    private void PresetSwatchButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string color })
        {
            ApplyColor(ThemePalette.NormalizeColor(color, SelectedColor));
        }
    }

    private void ApplyColor(string normalizedColor)
    {
        var color = ParseColor(normalizedColor);
        _alpha = color.A / 255d;
        ToHsv(color, out _hue, out _saturation, out _value);
        SelectedColor = normalizedColor;
        RefreshVisuals();
    }

    private void UpdateColorField(MediaPoint point)
    {
        var width = Math.Max(1d, ColorFieldHost.ActualWidth);
        var height = Math.Max(1d, ColorFieldHost.ActualHeight);
        _saturation = Clamp01(point.X / width);
        _value = Clamp01(1d - (point.Y / height));
        CommitHsvaChange();
    }

    private void UpdateHue(MediaPoint point)
    {
        var width = Math.Max(1d, HueSliderHost.ActualWidth);
        _hue = Clamp01(point.X / width) * 360d;
        CommitHsvaChange();
    }

    private void UpdateAlpha(MediaPoint point)
    {
        var width = Math.Max(1d, AlphaSliderHost.ActualWidth);
        _alpha = Clamp01(point.X / width);
        CommitHsvaChange();
    }

    private void CommitHsvaChange()
    {
        SelectedColor = ToHex(FromHsva(_hue, _saturation, _value, _alpha));
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        var hueColor = FromHsva(_hue, 1d, 1d, 1d);
        var opaqueColor = FromHsva(_hue, _saturation, _value, 1d);
        var selectedColor = FromHsva(_hue, _saturation, _value, _alpha);
        ColorFieldHueLayer.Fill = new SolidColorBrush(hueColor);
        AlphaGradientBorder.Background = new LinearGradientBrush(
            MediaColor.FromArgb(0, opaqueColor.R, opaqueColor.G, opaqueColor.B),
            MediaColor.FromArgb(255, opaqueColor.R, opaqueColor.G, opaqueColor.B),
            0d);
        PreviewEllipse.Fill = new SolidColorBrush(selectedColor);

        _suppressHexChange = true;
        HexTextBox.Text = ToHex(selectedColor);
        _suppressHexChange = false;
        HexInputBorder.BorderBrush = (WpfBrush)FindResource("Theme.InputBorderBrush");
        OkButton.IsEnabled = true;
        RefreshSelectorPositions();
    }

    private void RefreshSelectorPositions()
    {
        PositionSelector(ColorFieldSelector, ColorFieldHost.ActualWidth * _saturation, ColorFieldHost.ActualHeight * (1d - _value));
        PositionSelector(HueSelector, HueSliderHost.ActualWidth * (_hue / 360d), HueSliderHost.ActualHeight / 2d);
        PositionSelector(AlphaSelector, AlphaSliderHost.ActualWidth * _alpha, AlphaSliderHost.ActualHeight / 2d);
    }

    private static void PositionSelector(FrameworkElement selector, double centerX, double centerY)
    {
        Canvas.SetLeft(selector, centerX - (selector.Width / 2d));
        Canvas.SetTop(selector, centerY - (selector.Height / 2d));
    }

    private void BuildPresetButtons()
    {
        PresetSwatchPanel.Children.Clear();
        foreach (var color in PresetColors)
        {
            var swatch = ParseColor(color);
            var button = new WpfButton
            {
                Tag = color,
                Style = (Style)FindResource("PresetSwatchButtonStyle"),
                Margin = new Thickness(2),
                ToolTip = color
            };
            button.Click += PresetSwatchButton_OnClick;
            button.Content = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = new SolidColorBrush(swatch),
                BorderThickness = new Thickness(1.5),
                BorderBrush = new SolidColorBrush(WpfColor.Multiply(Colors.White, 0.56f))
            };
            PresetSwatchPanel.Children.Add(button);
        }
    }

    private void ReleaseActiveCapture()
    {
        switch (_dragSurface)
        {
            case DragSurface.ColorField:
                ColorFieldHost.ReleaseMouseCapture();
                break;
            case DragSurface.HueSlider:
                HueSliderHost.ReleaseMouseCapture();
                break;
            case DragSurface.AlphaSlider:
                AlphaSliderHost.ReleaseMouseCapture();
                break;
        }

        _dragSurface = DragSurface.None;
    }

    private static void ToHsv(MediaColor color, out double hue, out double saturation, out double value)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        value = max;
        saturation = max <= 0d ? 0d : delta / max;

        if (delta <= 0d)
        {
            hue = 0d;
            return;
        }

        if (Math.Abs(max - r) < double.Epsilon)
        {
            hue = 60d * (((g - b) / delta) % 6d);
        }
        else if (Math.Abs(max - g) < double.Epsilon)
        {
            hue = 60d * (((b - r) / delta) + 2d);
        }
        else
        {
            hue = 60d * (((r - g) / delta) + 4d);
        }

        if (hue < 0d)
        {
            hue += 360d;
        }
    }

    private static MediaColor FromHsva(double hue, double saturation, double value, double alpha)
    {
        hue %= 360d;
        if (hue < 0d)
        {
            hue += 360d;
        }

        saturation = Clamp01(saturation);
        value = Clamp01(value);
        alpha = Clamp01(alpha);

        var c = value * saturation;
        var x = c * (1d - Math.Abs(((hue / 60d) % 2d) - 1d));
        var m = value - c;

        (double R, double G, double B) = hue switch
        {
            >= 0d and < 60d => (c, x, 0d),
            >= 60d and < 120d => (x, c, 0d),
            >= 120d and < 180d => (0d, c, x),
            >= 180d and < 240d => (0d, x, c),
            >= 240d and < 300d => (x, 0d, c),
            _ => (c, 0d, x)
        };

        return MediaColor.FromArgb(
            (byte)Math.Round(alpha * 255d),
            (byte)Math.Round((R + m) * 255d),
            (byte)Math.Round((G + m) * 255d),
            (byte)Math.Round((B + m) * 255d));
    }

    private static MediaColor ParseColor(string color)
    {
        var normalized = ThemePalette.NormalizeColor(color, "#FFFFFFFF");
        var hex = normalized[1..];
        return MediaColor.FromArgb(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16),
            Convert.ToByte(hex.Substring(6, 2), 16));
    }

    private static string ToHex(MediaColor color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static double Clamp01(double value)
    {
        return Math.Max(0d, Math.Min(1d, value));
    }

    private enum DragSurface
    {
        None,
        ColorField,
        HueSlider,
        AlphaSlider
    }
}
