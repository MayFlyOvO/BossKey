using System.Threading;
using System.Windows;
using BossKey.App.Services;

namespace BossKey.App;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "BossKey.App.SingleInstance";
    private const string DuplicateLaunchEventName = "BossKey.App.DuplicateLaunchEvent";
    private readonly RuntimeSessionService _runtimeSessionService = new();
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _duplicateLaunchEvent;
    private RegisteredWaitHandle? _duplicateLaunchRegistration;
    public bool HadUnexpectedPreviousExit { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
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
                    System.Windows.MessageBox.Show(
                        app.MainWindow,
                        "BossKey is already running. Please do not start it twice.\n程序已在运行，不能重复启动。",
                        "BossKey",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            },
            this,
            Timeout.Infinite,
            executeOnlyOnce: false);
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
