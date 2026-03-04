using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BossKey.App.Localization;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;

namespace BossKey.App;

public partial class ThemedMessageBoxWindow : Window
{
    private readonly string _languageCode;
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public ThemedMessageBoxWindow(
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image,
        string languageCode)
    {
        InitializeComponent();
        _languageCode = Localizer.NormalizeLanguage(languageCode);
        HeaderTextBlock.Text = title;
        MessageTitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        ConfigureIcon(image);
        BuildButtons(buttons);
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
        CloseWithResult(GetCloseResult());
    }

    private void DialogButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is MessageBoxResult result)
        {
            CloseWithResult(result);
        }
    }

    private void BuildButtons(MessageBoxButton buttons)
    {
        ButtonsPanel.Children.Clear();

        switch (buttons)
        {
            case MessageBoxButton.OK:
                AddButton(Localizer.T("Common.Ok", _languageCode), MessageBoxResult.OK, isPrimary: true, isDefault: true, isCancel: true);
                break;
            case MessageBoxButton.OKCancel:
                AddButton(Localizer.T("Settings.Cancel", _languageCode), MessageBoxResult.Cancel, isPrimary: false, isDefault: false, isCancel: true);
                AddButton(Localizer.T("Common.Ok", _languageCode), MessageBoxResult.OK, isPrimary: true, isDefault: true, isCancel: false);
                break;
            case MessageBoxButton.YesNo:
                AddButton(Localizer.T("Common.No", _languageCode), MessageBoxResult.No, isPrimary: false, isDefault: false, isCancel: true);
                AddButton(Localizer.T("Common.Yes", _languageCode), MessageBoxResult.Yes, isPrimary: true, isDefault: true, isCancel: false);
                break;
            case MessageBoxButton.YesNoCancel:
                AddButton(Localizer.T("Settings.Cancel", _languageCode), MessageBoxResult.Cancel, isPrimary: false, isDefault: false, isCancel: true);
                AddButton(Localizer.T("Common.No", _languageCode), MessageBoxResult.No, isPrimary: false, isDefault: false, isCancel: false);
                AddButton(Localizer.T("Common.Yes", _languageCode), MessageBoxResult.Yes, isPrimary: true, isDefault: true, isCancel: false);
                break;
            default:
                AddButton(Localizer.T("Common.Ok", _languageCode), MessageBoxResult.OK, isPrimary: true, isDefault: true, isCancel: true);
                break;
        }
    }

    private void AddButton(string text, MessageBoxResult result, bool isPrimary, bool isDefault, bool isCancel)
    {
        var button = new Button
        {
            Content = text,
            Tag = result,
            Margin = new Thickness(8, 0, 0, 0),
            Style = (Style)FindResource(isPrimary ? "PrimaryButtonStyle" : "NeutralButtonStyle"),
            IsDefault = isDefault,
            IsCancel = isCancel
        };
        button.Click += DialogButton_OnClick;
        ButtonsPanel.Children.Add(button);
    }

    private void ConfigureIcon(MessageBoxImage image)
    {
        string glyph;
        Brush foreground;
        Brush badgeBackground;

        switch (image)
        {
            case MessageBoxImage.Warning:
                glyph = "\uE002";
                foreground = (Brush)FindResource("Theme.WarningBrush");
                badgeBackground = (Brush)FindResource("Theme.WarningSoftBrush");
                break;
            case MessageBoxImage.Error:
                glyph = "\uE000";
                foreground = (Brush)FindResource("Theme.WarningBrush");
                badgeBackground = (Brush)FindResource("Theme.WarningSoftBrush");
                break;
            case MessageBoxImage.Question:
                glyph = "\uE887";
                foreground = (Brush)FindResource("Theme.ButtonPrimaryBackgroundBrush");
                badgeBackground = (Brush)FindResource("Theme.SubtleBackgroundStrongBrush");
                break;
            case MessageBoxImage.Information:
                glyph = "\uE88E";
                foreground = (Brush)FindResource("Theme.ButtonPrimaryBackgroundBrush");
                badgeBackground = (Brush)FindResource("Theme.SubtleBackgroundStrongBrush");
                break;
            default:
                glyph = "\uE88E";
                foreground = (Brush)FindResource("Theme.IconBrush");
                badgeBackground = (Brush)FindResource("Theme.SubtleBackgroundStrongBrush");
                break;
        }

        IconGlyphTextBlock.Text = glyph;
        IconGlyphTextBlock.Foreground = foreground;
        IconBadgeBorder.Background = badgeBackground;
    }

    private MessageBoxResult GetCloseResult()
    {
        var cancelButton = ButtonsPanel.Children
            .OfType<Button>()
            .FirstOrDefault(static button => button.IsCancel);
        return cancelButton?.Tag is MessageBoxResult result
            ? result
            : MessageBoxResult.None;
    }

    private void CloseWithResult(MessageBoxResult result)
    {
        Result = result;
        DialogResult = true;
        Close();
    }
}
