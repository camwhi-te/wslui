using System.IO;
using System.Text;
using System.Windows.Input;
using wslUI.Infrastructure;
using wslUI.Models;
using wslUI.Services;

namespace wslUI.ViewModels;

/// <summary>Global WSL 2 settings: %UserProfile%\.wslconfig</summary>
public sealed class WslConfigPageViewModel : ConfigEditorViewModel
{
    public static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wslconfig");

    public WslConfigPageViewModel(MainViewModel main) : base(main, "Global config", "", ConfigSchemas.WslConfig())
    {
        ApplyCommand = new AsyncCommand(async () =>
        {
            if (!Dialogs.Confirm("Settings in .wslconfig apply after WSL restarts. Shut down WSL now? (All running distributions stop.)")) return;
            var r = await Wsl.ShutdownAsync(force: false);
            Dialogs.ShowResult(r, "Shutdown");
            await Main.RefreshDistrosAsync();
        });
        OpenFolderCommand = new RelayCommand(() => WslService.OpenExplorer(Path.GetDirectoryName(FilePath)!));
    }

    public override string Subtitle => $"Global settings for the WSL 2 VM and all distributions, stored in {FilePath}. Unset values use WSL's defaults.";
    public override ICommand ApplyCommand { get; }
    public override string ApplyText => "Apply (restart WSL)";
    public ICommand OpenFolderCommand { get; }

    protected override string Newline => "\r\n";
    protected override string EscapePath(string path) => path.Replace(@"\", @"\\");

    private bool _loaded;
    public override void OnNavigatedTo()
    {
        if (_loaded) return;
        _loaded = true;
        _ = LoadAsync();
    }

    protected override Task<string?> ReadAsync() =>
        Task.FromResult<string?>(File.Exists(FilePath) ? File.ReadAllText(FilePath) : "");

    protected override Task WriteAsync(string text)
    {
        if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", overwrite: true);
        File.WriteAllText(FilePath, text, new UTF8Encoding(false));
        Main.Log($"[{DateTime.Now:HH:mm:ss}] wrote {FilePath} (previous version saved as .wslconfig.bak)");
        return Task.CompletedTask;
    }

    protected override Task AfterSaveAsync()
    {
        Status += " — run \"Apply (restart WSL)\" for changes to take effect.";
        return Task.CompletedTask;
    }
}

/// <summary>Per-distribution settings: /etc/wsl.conf inside the distro.</summary>
public sealed class DistroConfigPageViewModel : ConfigEditorViewModel
{
    public DistroConfigPageViewModel(MainViewModel main) : base(main, "Distro config", "", ConfigSchemas.WslConf())
    {
        ApplyCommand = new AsyncCommand(async () =>
        {
            var r = await Wsl.TerminateAsync(SelectedDistro!.Name);
            Dialogs.ShowResult(r, "Terminate");
            await Main.RefreshDistrosAsync();
            Status = $"{SelectedDistro.Name} stopped — changes apply on next start.";
        }, () => SelectedDistro is not null);
    }

    public override string Subtitle => "Per-distribution settings stored in /etc/wsl.conf inside the selected distro. Reading or saving starts the distro.";
    public override bool IsDistroScoped => true;
    public override ICommand ApplyCommand { get; }
    public override string ApplyText => "Stop distro to apply";

    private Models.Distro? _selectedDistro;
    public Models.Distro? SelectedDistro
    {
        get => _selectedDistro;
        set
        {
            if (_selectedDistro == value) return;
            if (IsDirty && !Dialogs.Confirm("Discard unsaved changes to the current wsl.conf?"))
            {
                OnPropertyChanged(); // revert the ComboBox
                return;
            }
            IsDirty = false;
            Set(ref _selectedDistro, value);
            if (value is not null) _ = LoadAsync();
        }
    }

    protected override bool CanLoad() => SelectedDistro is not null;

    public override void OnNavigatedTo()
    {
        SelectedDistro ??= Main.DistrosPage.Selected ?? Main.Distros.FirstOrDefault(d => d.IsDefault);
    }

    protected override async Task<string?> ReadAsync()
    {
        if (SelectedDistro is null) return null;
        return await Wsl.ReadFileAsync(SelectedDistro.Name, "/etc/wsl.conf") ?? "";
    }

    protected override Task WriteAsync(string text) => Wsl.WriteFileAsync(SelectedDistro!.Name, "/etc/wsl.conf", text);

    protected override Task AfterSaveAsync()
    {
        Status += $" — restart {SelectedDistro!.Name} for changes to take effect.";
        return Task.CompletedTask;
    }
}
