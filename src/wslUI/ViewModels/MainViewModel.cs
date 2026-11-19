using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
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
    private readonly List<double> _vmCpuHistory = [];
    private readonly List<double> _vmMemHistory = [];
    private DistroStats? _vmPrev;
    private readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;
    private bool _refreshing, _statsRunning;

    public MainViewModel()
    {
        Settings = AppSettings.Load();
        _log = new TextBuffer(t => LogText = t);
        Wsl.Cli.Logged += line =>
        {
            _log.AppendLine(line);
            if (line.StartsWith('[')) LastCommand = line;
        };
        Wsl.Cli.ActiveChanged += n => _ui.InvokeAsync(() => ActiveCommands = n);

        DistrosPage = new DistrosPageViewModel(this);
        InstallPage = new InstallPageViewModel(this);
        BackupPage = new BackupPageViewModel(this);
        DisksPage = new DisksPageViewModel(this);
        WslConfigPage = new WslConfigPageViewModel(this);
        DistroConfigPage = new DistroConfigPageViewModel(this);
        ConsolePage = new ConsolePageViewModel(this);
        SystemPage = new SystemPageViewModel(this);
        Pages = [DistrosPage, InstallPage, BackupPage, DisksPage, WslConfigPage, DistroConfigPage, ConsolePage, SystemPage];
        PagesView = new ListCollectionView(Pages.ToList());
        PagesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PageViewModel.Group)));
        _selectedPage = DistrosPage;

        ClearLogCommand = new RelayCommand(() => _log.Clear());
        CancelAllCommand = new RelayCommand(() => Wsl.Cli.CancelAll(), () => ActiveCommands > 0);
        RefreshCommand = new AsyncCommand(RefreshDistrosAsync);
        ToggleLogCommand = new RelayCommand(() => LogVisible = !LogVisible);

        Distros.CollectionChanged += (_, _) => RaiseCounts();

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
    public ICollectionView PagesView { get; }

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
    public ICommand ToggleLogCommand { get; }

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

    // ---------- Command log ----------

    private string _logText = "";
    public string LogText { get => _logText; private set => Set(ref _logText, value); }

    private string _lastCommand = "";
    public string LastCommand
    {
        get => _lastCommand;
        private set => _ui.InvokeAsync(() => { _lastCommand = value; OnPropertyChanged(nameof(LastCommand)); }, DispatcherPriority.Background);
    }

    public bool LogVisible
    {
        get => Settings.LogVisible;
        set
        {
            if (Settings.LogVisible == value) return;
            Settings.LogVisible = value;
            Settings.Save();
            OnPropertyChanged();
        }
    }

    // ---------- Busy state ----------

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

    // ---------- VM-wide metrics ----------

    private double? _vmCpu;
    public double? VmCpuPercent { get => _vmCpu; private set => Set(ref _vmCpu, value); }

    private DistroStats? _vm;
    /// <summary>Latest VM-wide sample (memory, swap, cores, load) from any running distro.</summary>
    public DistroStats? Vm
    {
        get => _vm;
        private set
        {
            if (!Set(ref _vm, value)) return;
            OnPropertyChanged(nameof(VmMemPercent));
            OnPropertyChanged(nameof(VmMemText));
            OnPropertyChanged(nameof(VmMemSubText));
            OnPropertyChanged(nameof(VmCpuSubText));
            OnPropertyChanged(nameof(VmSwapPercent));
            OnPropertyChanged(nameof(VmSwapText));
            OnPropertyChanged(nameof(VmSwapSubText));
            OnPropertyChanged(nameof(VmOnline));
            OnPropertyChanged(nameof(VmInfo));
        }
    }

    public bool VmOnline => _vm is not null;
    public string VmCpuText => _vmCpu is { } c ? $"{c:0}%" : "—";
    public string VmCpuSubText => _vm is { } v ? $"{v.Cores} cores  ·  load {v.Load1:0.00}" : "VM not running";

    private static long Used(DistroStats v) => (v.MemTotalKb - v.MemAvailKb) * 1024;
    public double VmMemPercent => _vm is { MemTotalKb: > 0 } v ? 100.0 * Used(v) / (v.MemTotalKb * 1024.0) : 0;
    public string VmMemText => _vm is { } v ? Format.Bytes(Used(v)) : "—";
    public string VmMemSubText => _vm is { } v ? $"of {Format.Bytes(v.MemTotalKb * 1024)}  ·  {VmMemPercent:0}%" : "VM not running";

    public double VmSwapPercent => _vm is { SwapTotalKb: > 0 } v ? 100.0 * (v.SwapTotalKb - v.SwapFreeKb) / v.SwapTotalKb : 0;
    public string VmSwapText => _vm is { } v ? Format.Bytes((v.SwapTotalKb - v.SwapFreeKb) * 1024) : "—";
    public string VmSwapSubText => _vm is { } v ? $"of {Format.Bytes(v.SwapTotalKb * 1024)}  ·  VM up {Format.Duration(v.Uptime)}" : "VM not running";

    private double[] _vmCpuHistoryView = [];
    public double[] VmCpuHistory { get => _vmCpuHistoryView; private set => Set(ref _vmCpuHistoryView, value); }

    private double[] _vmMemHistoryView = [];
    public double[] VmMemHistory { get => _vmMemHistoryView; private set => Set(ref _vmMemHistoryView, value); }

    /// <summary>Compact one-liner for the status bar.</summary>
    public string VmInfo => _vm is null ? "WSL VM stopped" : $"CPU {VmCpuText}  ·  RAM {VmMemText} / {Format.Bytes(_vm.MemTotalKb * 1024)}";

    public int RunningCount => Distros.Count(d => d.IsRunning);
    public int TotalCount => Distros.Count;
    public string RunningText => $"{RunningCount} / {TotalCount}";
    public bool HasDistros => Distros.Count > 0;

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(RunningCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(RunningText));
        OnPropertyChanged(nameof(HasDistros));
    }

    private string _wslVersion = "";
    public string WslVersion { get => _wslVersion; set => Set(ref _wslVersion, value); }

    public bool IsWindowVisible { get; set; } = true;

    public void Log(string line) => Wsl.Cli.Log(line);

    public void ApplyStatsInterval()
    {
        _statsTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(Settings.StatsIntervalSeconds, 1, 300));
    }

    public async Task InitializeAsync()
    {
        if (!File.Exists(WslCli.WslExe))
            Dialogs.Error("wsl.exe was not found. Use System → Install WSL components, or run 'wsl --install' once.");
        await RefreshDistrosAsync();
        SelectedPage.OnNavigatedTo();
        _listTimer.Start();
        _statsTimer.Start();
        _ = RefreshStatsAsync();
        var v = await Wsl.Cli.RunAsync(["--version"], new RunOptions { Quiet = true });
        var first = v.StdOut.Split('\n').FirstOrDefault()?.Trim() ?? "";
        WslVersion = first.Contains(':') ? "WSL " + first[(first.IndexOf(':') + 1)..].Trim() : first;
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
                LxssRegistry.Read().ToDictionary(kv => kv.Key, kv => (kv.Value, Size: TryFileSize(kv.Value.VhdPath)), StringComparer.OrdinalIgnoreCase));

            foreach (var gone in Distros.Where(d => list.All(i => !i.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase))).ToList())
                Distros.Remove(gone);

            for (var idx = 0; idx < list.Count; idx++)
            {
                var item = list[idx];
                var d = FindDistro(item.Name);
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
            RaiseCounts();
            if (!Distros.Any(d => d.IsRunning)) ResetVm();
        }
        finally { _refreshing = false; }
    }

    private static long? TryFileSize(string path)
    {
        try { return path.Length > 0 && File.Exists(path) ? new FileInfo(path).Length : null; }
        catch { return null; }
    }

    private void ResetVm()
    {
        _vmPrev = null;
        Vm = null;
        VmCpuPercent = null;
        OnPropertyChanged(nameof(VmCpuText));
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
                if (s is null || !d.IsRunning) continue;
                d.AddSample(s);
                if (vm is null || d.IsDefault) vm = s;
            }
            if (vm is null) { if (running.Count == 0) ResetVm(); return; }

            if (_vmPrev is { } p && vm.CpuTotalJiffies > p.CpuTotalJiffies)
            {
                var dTotal = vm.CpuTotalJiffies - p.CpuTotalJiffies;
                var dIdle = vm.CpuIdleJiffies - p.CpuIdleJiffies;
                VmCpuPercent = Math.Clamp(100.0 * (dTotal - dIdle) / dTotal, 0, 100);
                Push(_vmCpuHistory, VmCpuPercent.Value);
                VmCpuHistory = [.. _vmCpuHistory];
            }
            _vmPrev = vm;
            Vm = vm;
            Push(_vmMemHistory, VmMemPercent);
            VmMemHistory = [.. _vmMemHistory];
            OnPropertyChanged(nameof(VmCpuText));
            OnPropertyChanged(nameof(VmInfo));
        }
        finally { _statsRunning = false; }
    }

    private static void Push(List<double> list, double v)
    {
        list.Add(v);
        if (list.Count > Distro.HistoryLength) list.RemoveAt(0);
    }

    /// <summary>Waits until a distro reports the given running state (or the timeout elapses).</summary>
    public async Task WaitForStateAsync(string name, bool running, int timeoutMs = 20000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until)
        {
            await RefreshDistrosAsync();
            var d = FindDistro(name);
            if (d is not null && d.IsRunning == running) return;
            await Task.Delay(700);
        }
    }

    public Distro? FindDistro(string name) =>
        Distros.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
