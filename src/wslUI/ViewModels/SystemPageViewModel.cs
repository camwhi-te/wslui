using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using wslUI.Infrastructure;
using wslUI.Services;

namespace wslUI.ViewModels;

public sealed class SystemPageViewModel : PageViewModel
{
    private static readonly string WslSettingsExe =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"WSL\wslsettings\wslsettings.exe");

    public SystemPageViewModel(MainViewModel main) : base(main, "System", "\uE770", "TOOLS")
    {
        RefreshCommand = new AsyncCommand(RefreshAsync);
        UpdateCommand = new AsyncCommand(() => UpdateAsync(false));
        UpdatePreCommand = new AsyncCommand(() => UpdateAsync(true));
        ShutdownCommand = new AsyncCommand(() => ShutdownAsync(false));
        ForceShutdownCommand = new AsyncCommand(() => ShutdownAsync(true));
        SetDefaultVersionCommand = new AsyncCommand(async p =>
        {
            var r = await Wsl.SetDefaultVersionAsync((string)p!);
            Dialogs.ShowResult(r, "Set default version");
            await RefreshAsync();
        });
        InstallComponentsCommand = new AsyncCommand(async () =>
        {
            var r = await Wsl.InstallAsync(new InstallOptions { NoDistribution = true, EnableWsl1 = EnableWsl1 });
            Dialogs.ShowResult(r, "Install components");
            if (r.Ok) Dialogs.Info(r.Text.Length > 0 ? r.Text : "Done. A reboot may be required.");
        });
        UninstallCommand = new AsyncCommand(async () =>
        {
            if (!Dialogs.ConfirmByTyping("Uninstall WSL",
                    "This uninstalls the Windows Subsystem for Linux package from this machine. Your distributions stay registered but can't run until WSL is reinstalled.",
                    "uninstall")) return;
            var r = await Wsl.UninstallWslAsync();
            Dialogs.ShowResult(r, "Uninstall");
        });
        DebugShellCommand = new RelayCommand(() => Wsl.OpenTerminal("--debug-shell"));
        SystemShellCommand = new RelayCommand(() => Wsl.OpenTerminal("--system"));
        WslSettingsCommand = new RelayCommand(
            () => Process.Start(new ProcessStartInfo(WslSettingsExe) { UseShellExecute = true }),
            () => File.Exists(WslSettingsExe));
        OpenSettingsFolderCommand = new RelayCommand(() =>
            WslService.OpenExplorer(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "wslUI")));
    }

    public ICommand RefreshCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand UpdatePreCommand { get; }
    public ICommand ShutdownCommand { get; }
    public ICommand ForceShutdownCommand { get; }
    public ICommand SetDefaultVersionCommand { get; }
    public ICommand InstallComponentsCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand DebugShellCommand { get; }
    public ICommand SystemShellCommand { get; }
    public ICommand WslSettingsCommand { get; }
    public ICommand OpenSettingsFolderCommand { get; }

    /// <summary>"Key: value" lines from wsl --version and --status, shown as a tidy list.</summary>
    private IReadOnlyList<KeyValuePair<string, string>> _facts = [];
    public IReadOnlyList<KeyValuePair<string, string>> Facts { get => _facts; set => Set(ref _facts, value); }

    private static IReadOnlyList<KeyValuePair<string, string>> ParseFacts(string text) =>
        text.Split((char)10, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Contains(':'))
            .Select(l => new KeyValuePair<string, string>(l[..l.IndexOf(':')].Trim(), l[(l.IndexOf(':') + 1)..].Trim()))
            .Where(kv => kv.Value.Length > 0)
            .DistinctBy(kv => kv.Key)
            .ToList();

    private string _versionText = "";
    public string VersionText { get => _versionText; set => Set(ref _versionText, value); }

    private string _statusText = "";
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    private string _defaultVersion = "";
    public string DefaultVersion { get => _defaultVersion; set => Set(ref _defaultVersion, value); }

    private bool _enableWsl1;
    public bool EnableWsl1 { get => _enableWsl1; set => Set(ref _enableWsl1, value); }

    // ----- app settings -----
    public bool CloseToTray
    {
        get => Main.Settings.CloseToTray;
        set { Main.Settings.CloseToTray = value; Main.Settings.Save(); OnPropertyChanged(); }
    }

    public bool StartMinimized
    {
        get => Main.Settings.StartMinimized;
        set { Main.Settings.StartMinimized = value; Main.Settings.Save(); OnPropertyChanged(); }
    }

    public bool LiveStats
    {
        get => Main.Settings.LiveStats;
        set { Main.Settings.LiveStats = value; Main.Settings.Save(); OnPropertyChanged(); }
    }

    public int StatsInterval
    {
        get => Main.Settings.StatsIntervalSeconds;
        set
        {
            Main.Settings.StatsIntervalSeconds = Math.Clamp(value, 2, 300);
            Main.Settings.Save();
            Main.ApplyStatsInterval();
            OnPropertyChanged();
        }
    }

    private bool _loaded;
    public override void OnNavigatedTo()
    {
        if (_loaded) return;
        _loaded = true;
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var v = await Wsl.Cli.RunAsync(["--version"], new RunOptions { Quiet = true });
        VersionText = v.Text;
        var s = await Wsl.Cli.RunAsync(["--status"], new RunOptions { Quiet = true });
        StatusText = s.Text;
        Facts = ParseFacts(v.Text + Environment.NewLine + s.Text);
        DefaultVersion = LxssRegistry.DefaultVersion()?.ToString() ?? "2";
    }

    private async Task UpdateAsync(bool pre)
    {
        var r = await Wsl.UpdateAsync(pre);
        Dialogs.ShowResult(r, "Update");
        if (r.Ok) Dialogs.Info(r.Text.Length > 0 ? r.Text : "WSL is up to date.");
        await RefreshAsync();
    }

    private async Task ShutdownAsync(bool force)
    {
        var msg = force
            ? "Force-terminate the WSL 2 VM even if an operation is in progress? This can cause data loss."
            : "Shut down WSL? All running distributions and the WSL 2 VM stop.";
        if (!Dialogs.Confirm(msg, warning: force)) return;
        var r = await Wsl.ShutdownAsync(force);
        Dialogs.ShowResult(r, "Shutdown");
        await Main.RefreshDistrosAsync();
    }
}
