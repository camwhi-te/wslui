using System.Windows;
using wslUI.Services;
using wslUI.ViewModels;
using wslUI.Views;

namespace wslUI;

public partial class App : Application
{
    private const string InstanceEventName = "wslUI.SingleInstance.Show";
    private EventWaitHandle? _instanceEvent;
    private TrayIcon? _tray;
    private MainViewModel? _vm;

    public bool IsExiting { get; private set; }

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Single instance: a second launch just brings the first window forward.
        _instanceEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceEventName, out var created);
        if (!created)
        {
            _instanceEvent.Set();
            Shutdown();
            return;
        }
        var signal = _instanceEvent;
        new Thread(() =>
        {
            while (signal.WaitOne()) Dispatcher.BeginInvoke(ShowMainWindow);
        }) { IsBackground = true, Name = "single-instance" }.Start();

        DispatcherUnhandledException += (_, ex) =>
        {
            Dialogs.Error(ex.Exception.Message, "Unexpected error");
            ex.Handled = true;
        };

        base.OnStartup(e);

        _vm = new MainViewModel();
        var window = new MainWindow { DataContext = _vm };
        MainWindow = window;
        _tray = new TrayIcon(_vm);

        var minimized = _vm.Settings.StartMinimized || e.Args.Contains("--minimized");
        if (!minimized) window.Show();
        else _vm.IsWindowVisible = false;

        _ = _vm.InitializeAsync();
    }

    public void ShowMainWindow()
    {
        if (MainWindow is not { } w) return;
        w.Show();
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate();
        if (_vm is not null) _vm.IsWindowVisible = true;
    }

    public void ExitApp()
    {
        IsExiting = true;
        _tray?.Dispose();
        MainWindow?.Close();
        Shutdown();
    }

    public void ShowTrayHint()
    {
        if (_vm is null || _vm.Settings.TrayHintShown) return;
        _vm.Settings.TrayHintShown = true;
        _vm.Settings.Save();
        _tray?.ShowBalloon("wslUI is still running", "It's in the system tray. Right-click the icon for quick actions, or turn this off in System → App settings.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
