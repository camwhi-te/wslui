using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using wslUI.Infrastructure;
using wslUI.Models;
using wslUI.Services;

namespace wslUI.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly TextBuffer _log;
    private readonly DispatcherTimer _listTimer;
    private readonly DispatcherTimer _statsTimer;
    private bool _refreshing, _statsRunning;

    public MainViewModel()
    {
        Settings = AppSettings.Load();
        _log = new TextBuffer(t => LogText = t);
        Wsl.Cli.Logged += _log.AppendLine;
        var ui = Dispatcher.CurrentDispatcher;
        Wsl.Cli.ActiveChanged += n => ui.InvokeAsync(() => ActiveCommands = n);

        DistrosPage = new DistrosPageViewModel(this);
        InstallPage = new InstallPageViewModel(this);
        BackupPage = new BackupPageViewModel(this);
        DisksPage = new DisksPageViewModel(this);
        WslConfigPage = new WslConfigPageViewModel(this);
        DistroConfigPage = new DistroConfigPageViewModel(this);
        ConsolePage = new ConsolePageViewModel(this);
        SystemPage = new SystemPageViewModel(this);
        Pages = [DistrosPage, InstallPage, BackupPage, DisksPage, WslConfigPage, DistroConfigPage, ConsolePage, SystemPage];
        _selectedPage = DistrosPage;

        ClearLogCommand = new RelayCommand(() => _log.Clear());
        CancelAllCommand = new RelayCommand(() => Wsl.Cli.CancelAll(), () => ActiveCommands > 0);
        RefreshCommand = new AsyncCommand(RefreshDistrosAsync);

        _listTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _listTimer.Tick += async (_, _) => { if (IsWindowVisible) await RefreshDistrosAsync(); };
        _statsTimer = new DispatcherTimer();
        _statsTimer.Tick += async (_, _) => await RefreshStatsAsync();
        ApplyStatsInterval();
    }

    public WslService Wsl { get; } = new();
    public AppSettings Settings { get; }
    public ObservableCollection<Distro> Distros { get; } = [];
    public IReadOnlyList<PageViewModel> Pages { get; }

    public DistrosPageViewModel DistrosPage { get; }
    public InstallPageViewModel InstallPage { get; }
    public BackupPageViewModel BackupPage { get; }
    public DisksPageViewModel DisksPage { get; }
    public WslConfigPageViewModel WslConfigPage { get; }
    public DistroConfigPageViewModel DistroConfigPage { get; }
    public ConsolePageViewModel ConsolePage { get; }
    public SystemPageViewModel SystemPage { get; }

    public ICommand ClearLogCommand { get; }
    public ICommand CancelAllCommand { get; }
    public ICommand RefreshCommand { get; }

    private PageViewModel _selectedPage;
    public PageViewModel SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (value is null || !Set(ref _selectedPage, value)) return;
            value.OnNavigatedTo();
        }
    }

    public void Navigate(PageViewModel page) => SelectedPage = page;

    private string _logText = "";
    public string LogText { get => _logText; private set => Set(ref _logText, value); }

    private int _activeCommands;
    public int ActiveCommands
    {
        get => _activeCommands;
        private set
        {
            if (!Set(ref _activeCommands, value)) return;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(StatusText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsBusy => _activeCommands > 0;
    public string StatusText => _activeCommands switch
    {
        0 => "Ready",
        1 => "Running 1 command…",
        var n => $"Running {n} commands…",
    };

    private string _vmInfo = "";
    /// <summary>WSL 2 VM resource summary (memory/swap/load), from any running distro.</summary>
    public string VmInfo { get => _vmInfo; private set => Set(ref _vmInfo, value); }

    private string _wslVersion = "";
    public string WslVersion { get => _wslVersion; set => Set(ref _wslVersion, value); }

    public bool IsWindowVisible { get; set; } = true;

    public void Log(string line) => Wsl.Cli.Log(line);

    public void ApplyStatsInterval()
    {
        _statsTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(Settings.StatsIntervalSeconds, 2, 300));
    }

    public async Task InitializeAsync()
    {
        if (!File.Exists(WslCli.WslExe))
        {
            Dialogs.Error("wsl.exe was not found. Use System → Install WSL components, or run 'wsl --install' once.");
        }
        await RefreshDistrosAsync();
        SelectedPage.OnNavigatedTo();
        _listTimer.Start();
        _statsTimer.Start();
        _ = RefreshStatsAsync();
        var v = await Wsl.Cli.RunAsync(["--version"], new RunOptions { Quiet = true });
        WslVersion = v.StdOut.Split('\n').FirstOrDefault()?.Trim() ?? "";
    }

    /// <summary>Re-reads `wsl -l -v` + registry and merges into <see cref="Distros"/> (keeps object identity for selection).</summary>
    public async Task RefreshDistrosAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var list = await Wsl.ListAsync();
            var reg = await Task.Run(() =>
            {
                var r = LxssRegistry.Read();
                return r.ToDictionary(kv => kv.Key, kv => (kv.Value, Size: TryFileSize(kv.Value.VhdPath)), StringComparer.OrdinalIgnoreCase);
            });

            foreach (var gone in Distros.Where(d => list.All(i => !i.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase))).ToList())
                Distros.Remove(gone);

            for (var idx = 0; idx < list.Count; idx++)
            {
                var item = list[idx];
                var d = Distros.FirstOrDefault(x => x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
                if (d is null)
                {
                    d = new Distro(item.Name);
                    Distros.Insert(Math.Min(idx, Distros.Count), d);
                }
                d.State = item.State;
                d.Version = item.Version;
                d.IsDefault = item.IsDefault;
                if (reg.TryGetValue(item.Name, out var info))
                {
                    d.Guid = info.Value.Guid;
                    d.BasePath = info.Value.BasePath;
                    d.VhdPath = info.Value.VhdPath;
                    d.DefaultUid = info.Value.DefaultUid;
                    d.VhdSize = info.Size;
                }
            }
            if (!Distros.Any(d => d.IsRunning)) VmInfo = "WSL VM not running";
        }
        finally { _refreshing = false; }
    }

    private static long? TryFileSize(string path)
    {
        try { return path.Length > 0 && File.Exists(path) ? new FileInfo(path).Length : null; }
        catch { return null; }
    }

    private async Task RefreshStatsAsync()
    {
        if (_statsRunning || !Settings.LiveStats || !IsWindowVisible) return;
        _statsRunning = true;
        try
        {
            var running = Distros.Where(d => d.IsRunning).ToList();
            var results = await Task.WhenAll(running.Select(async d => (d, s: await Wsl.GetStatsAsync(d.Name))));
            DistroStats? vm = null;
            foreach (var (d, s) in results)
            {
                if (s is null) continue;
                d.Stats = s;
                // docker-desktop and other appliance distros report the same VM; prefer a "normal" one.
                if (vm is null || d.IsDefault) vm = s;
            }
            if (vm is not null)
            {
                var used = (vm.MemTotalKb - vm.MemAvailKb) * 1024;
                var swapUsed = (vm.SwapTotalKb - vm.SwapFreeKb) * 1024;
                VmInfo = $"VM memory {Format.Bytes(used)} / {Format.Bytes(vm.MemTotalKb * 1024)}  ·  swap {Format.Bytes(swapUsed)} / {Format.Bytes(vm.SwapTotalKb * 1024)}  ·  load {vm.Load1:0.00}";
            }
            else if (running.Count == 0) VmInfo = "WSL VM not running";
        }
        finally { _statsRunning = false; }
    }

    /// <summary>Waits until a distro reports the given running state (or the timeout elapses).</summary>
    public async Task WaitForStateAsync(string name, bool running, int timeoutMs = 20000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until)
        {
            await RefreshDistrosAsync();
            var d = Distros.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (d is not null && d.IsRunning == running) return;
            await Task.Delay(700);
        }
    }

    public Distro? FindDistro(string name) =>
        Distros.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
