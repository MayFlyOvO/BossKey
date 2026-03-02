using System.Windows;
using System.Windows.Input;
using BossKey.App.Localization;
using BossKey.Core.Models;
using BossKey.Core.Native;
using BossKey.Core.Services;

namespace BossKey.App;

public partial class HotkeyCaptureWindow : Window
{
    private readonly HashSet<int> _pressedKeys = [];
    private readonly string _languageCode;
    private readonly bool _allowEmpty;
    private HashSet<int> _capturedKeys = [];

    public HotkeyBinding CapturedBinding { get; private set; } = new();

    public HotkeyCaptureWindow(string title, string languageCode, HotkeyBinding initialBinding, bool allowEmpty = false)
    {
        InitializeComponent();
        Title = title;
        _languageCode = Localizer.NormalizeLanguage(languageCode);
        _allowEmpty = allowEmpty;
        _capturedKeys = initialBinding.GetNormalizedKeys();
        ApplyLocalization();
        UpdatePreview();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var virtualKey = VirtualKeyCodes.Normalize(KeyInterop.VirtualKeyFromKey(key));
        if (virtualKey <= 0)
        {
            return;
        }

        _pressedKeys.Add(virtualKey);
        _capturedKeys = _pressedKeys.ToHashSet();
        UpdatePreview();
        e.Handled = true;
    }

    private void Window_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var virtualKey = VirtualKeyCodes.Normalize(KeyInterop.VirtualKeyFromKey(key));
        if (virtualKey > 0)
        {
            _pressedKeys.Remove(virtualKey);
        }

        e.Handled = true;
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        _pressedKeys.Clear();
        _capturedKeys.Clear();
        UpdatePreview();
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_capturedKeys.Count == 0)
        {
            if (_allowEmpty)
            {
                CapturedBinding = new HotkeyBinding();
                DialogResult = true;
                Close();
                return;
            }

            System.Windows.MessageBox.Show(
                this,
                Localizer.T("Hotkey.Empty", _languageCode),
                Localizer.T("Main.HintTitle", _languageCode),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        CapturedBinding = HotkeyBinding.FromKeys(_capturedKeys);
        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ApplyLocalization()
    {
        InstructionTextBlock.Text = Localizer.T("Hotkey.Instruction", _languageCode);
        CurrentLabelTextBlock.Text = Localizer.T("Hotkey.Current", _languageCode);
        ClearButton.Content = Localizer.T("Hotkey.Clear", _languageCode);
        OkButton.Content = Localizer.T("Hotkey.Ok", _languageCode);
        CancelButton.Content = Localizer.T("Hotkey.Cancel", _languageCode);
    }

    private void UpdatePreview()
    {
        HotkeyPreviewTextBlock.Text = HotkeyFormatter.Format(HotkeyBinding.FromKeys(_capturedKeys));
    }
}
