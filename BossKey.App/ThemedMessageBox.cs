using System.Windows;
using BossKey.App.Localization;

namespace BossKey.App;

public static class ThemedMessageBox
{
    public static MessageBoxResult Show(
        Window? owner,
        string message,
        string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None,
        string? languageCode = null)
    {
        var dialog = new ThemedMessageBoxWindow(
            message,
            title,
            buttons,
            image,
            languageCode ?? Localizer.CurrentLanguage);

        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        dialog.ShowDialog();
        return dialog.Result;
    }
}
