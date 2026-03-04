using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using BossKey.App.Services;
using BossKey.Core.Models;
using BossKey.Core.Services;

namespace BossKey.App;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "BossKey.App.SingleInstance";
    private const string DuplicateLaunchEventName = "BossKey.App.DuplicateLaunchEvent";
    private const string ElevatedRelaunchArgument = "--bosskey-elevated";
    private readonly RuntimeSessionService _runtimeSessionService = new();
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _duplicateLaunchEvent;
    private RegisteredWaitHandle? _duplicateLaunchRegistration;
    public bool HadUnexpectedPreviousExit { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (ShouldRelaunchAsAdministrator(e.Args) && TryRestartAsAdministrator())
        {
            Shutdown();
            return;
        }

        var createdNew = false;
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out createdNew);

        if (!createdNew)
        {
            NotifyExistingInstanceAboutDuplicateLaunch();
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        InitializeDuplicateLaunchNotifier();
        HadUnexpectedPreviousExit = _runtimeSessionService.BeginSession();
        base.OnStartup(e);
        ApplyStartupTheme();

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _runtimeSessionService.EndSessionGracefully();

        _duplicateLaunchRegistration?.Unregister(waitObject: null);
        _duplicateLaunchRegistration = null;
        _duplicateLaunchEvent?.Dispose();
        _duplicateLaunchEvent = null;

        if (_singleInstanceMutex is not null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }

    internal static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal static bool TryRestartAsAdministrator()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = ElevatedRelaunchArgument
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ShouldRelaunchAsAdministrator(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, ElevatedRelaunchArgument, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (IsRunningAsAdministrator())
        {
            return false;
        }

        try
        {
            var settings = new JsonSettingsStore().Load();
            return settings.RunAsAdministrator;
        }
        catch
        {
            return false;
        }
    }

    private void InitializeDuplicateLaunchNotifier()
    {
        _duplicateLaunchEvent = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: DuplicateLaunchEventName);

        _duplicateLaunchRegistration = ThreadPool.RegisterWaitForSingleObject(
            _duplicateLaunchEvent,
            static (state, _) =>
            {
                if (state is not App app)
                {
                    return;
                }

                app.Dispatcher.BeginInvoke(() =>
                {
                    ThemedMessageBox.Show(
                        app.MainWindow,
                        "BossKey is already running. Please do not start it twice.",
                        "BossKey",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            },
            this,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    private static void ApplyStartupTheme()
    {
        try
        {
            var settings = new JsonSettingsStore().Load();
            ThemeManager.ApplyTheme(settings.Theme);
        }
        catch
        {
            ThemeManager.ApplyTheme(ThemeSettings.CreateDefault());
        }
    }

    private static void NotifyExistingInstanceAboutDuplicateLaunch()
    {
        try
        {
            using var notifyEvent = EventWaitHandle.OpenExisting(DuplicateLaunchEventName);
            notifyEvent.Set();
        }
        catch
        {
        }
    }
}
