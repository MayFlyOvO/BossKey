using Microsoft.Win32;

namespace BossKey.Core.Services;

public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled(string appName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(appName) is string;
    }

    public void SetEnabled(string appName, string executablePath, bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(appName, $"\"{executablePath}\"");
        }
        else
        {
            key.DeleteValue(appName, throwOnMissingValue: false);
        }
    }
}
