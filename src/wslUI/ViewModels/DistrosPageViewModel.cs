using System.IO;
using System.Windows.Input;
using wslUI.Infrastructure;
using wslUI.Models;
using wslUI.Services;

namespace wslUI.ViewModels;

public sealed class DistrosPageViewModel : PageViewModel
{
    public DistrosPageViewModel(MainViewModel main) : base(main, "Distributions", "\uE7F4")
    {
        bool Has() => Selected is not null;
        bool Running() => Selected is { IsRunning: true };

        RefreshCommand = new AsyncCommand(Main.RefreshDistrosAsync);
        StartCommand = new AsyncCommand(StartAsync, () => Has() && !Running());
        StopCommand = new AsyncCommand(StopAsync, Running);
        RestartCommand = new AsyncCommand(RestartAsync, Has);
        ShutdownAllCommand = new AsyncCommand(ShutdownAllAsync);
        TerminalCommand = new RelayCommand(() => Wsl.OpenTerminal("-d", Selected!.Name, "--cd", "~"), Has);
        RootTerminalCommand = new RelayCommand(() => Wsl.OpenTerminal("-d", Selected!.Name, "-u", "root", "--cd", "~"), Has);
        ExplorerCommand = new RelayCommand(() => WslService.OpenExplorer($@"\\wsl.localhost\{Selected!.Name}"), Has);
        InstallFolderCommand = new RelayCommand(() => WslService.OpenExplorer(Selected!.BasePath), () => Selected is { BasePath.Length: > 0 });
        ConsoleCommand = new RelayCommand(() => { Main.ConsolePage.SelectedDistro = Selected; Main.Navigate(Main.ConsolePage); }, Has);
        EditConfCommand = new RelayCommand(() => { Main.DistroConfigPage.SelectedDistro = Selected; Main.Navigate(Main.DistroConfigPage); }, Has);
        SetDefaultCommand = new AsyncCommand(SetDefaultAsync, () => Selected is { IsDefault: false });
        SetVersionCommand = new AsyncCommand(SetVersionAsync, Has);
        SetDefaultUserCommand = new AsyncCommand(SetDefaultUserAsync, Has);
        CreateUserCommand = new AsyncCommand(CreateUserAsync, Has);
        ResizeCommand = new AsyncCommand(ResizeAsync, Has);
        SparseCommand = new AsyncCommand(SparseAsync, Has);
        MoveCommand = new AsyncCommand(MoveAsync, Has);
        ExportCommand = new AsyncCommand(ExportAsync, Has);
        CloneCommand = new AsyncCommand(CloneAsync, Has);
        UnregisterCommand = new AsyncCommand(UnregisterAsync, Has);
        LoadDetailsCommand = new AsyncCommand(LoadDetailsAsync, Running);
    }

    public ICommand RefreshCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand ShutdownAllCommand { get; }
    public ICommand TerminalCommand { get; }
    public ICommand RootTerminalCommand { get; }
    public ICommand ExplorerCommand { get; }
    public ICommand InstallFolderCommand { get; }
    public ICommand ConsoleCommand { get; }
    public ICommand EditConfCommand { get; }
    public ICommand SetDefaultCommand { get; }
    public ICommand SetVersionCommand { get; }
    public ICommand SetDefaultUserCommand { get; }
    public ICommand CreateUserCommand { get; }
    public ICommand ResizeCommand { get; }
    public ICommand SparseCommand { get; }
    public ICommand MoveCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand CloneCommand { get; }
    public ICommand UnregisterCommand { get; }
    public ICommand LoadDetailsCommand { get; }

    private Distro? _selected;
    public Distro? Selected
    {
        get => _selected;
        set
        {
            if (!Set(ref _selected, value)) return;
            OnPropertyChanged(nameof(HasSelection));
            CommandManager.InvalidateRequerySuggested();
            if (value is { IsRunning: true } && value.OsName.Length == 0) _ = LoadDetailsAsync();
        }
    }

    public bool HasSelection => _selected is not null;

    public override void OnNavigatedTo()
    {
        if (Selected is null && Main.Distros.Count > 0)
            Selected = Main.Distros.FirstOrDefault(d => d.IsDefault) ?? Main.Distros[0];
    }

    private async Task LoadDetailsAsync()
    {
        var d = Selected;
        if (d is not { IsRunning: true }) return;
        var details = await Wsl.GetDetailsAsync(d.Name);
        if (details is null) return;
        d.OsName = details.OsName;
        d.Kernel = details.Kernel;
        d.DefaultUser = details.DefaultUser;
    }

    // ---------- Power ----------

    private async Task StartAsync()
    {
        var d = Selected!;
        Wsl.Start(d.Name);
        await Main.WaitForStateAsync(d.Name, running: true);
        if (!d.IsRunning) Dialogs.Error($"{d.Name} didn't report Running yet. Check the command log; first boots can take a while.");
        else await LoadDetailsAsync();
    }

    private async Task StopAsync()
    {
        var d = Selected!;
        var r = await Wsl.TerminateAsync(d.Name);
        Dialogs.ShowResult(r, "Terminate");
        await Main.RefreshDistrosAsync();
    }

    private async Task RestartAsync()
    {
        var d = Selected!;
        if (d.IsRunning)
        {
            var r = await Wsl.TerminateAsync(d.Name);
            if (!r.Ok) { Dialogs.ShowResult(r, "Terminate"); return; }
            await Main.WaitForStateAsync(d.Name, running: false, 8000);
        }
        await StartAsync();
    }

    private async Task ShutdownAllAsync()
    {
        if (!Dialogs.Confirm("Shut down WSL? This stops every running distribution and the WSL 2 VM.")) return;
        var r = await Wsl.ShutdownAsync(force: false);
        Dialogs.ShowResult(r, "Shutdown");
        await Main.RefreshDistrosAsync();
    }

    /// <summary>For operations that need the distro stopped: asks, then terminates it.</summary>
    private async Task<bool> EnsureStoppedAsync(Distro d, string action)
    {
        if (!d.IsRunning) return true;
        if (!Dialogs.Confirm($"{action} requires {d.Name} to be stopped. Terminate it now?")) return false;
        var r = await Wsl.TerminateAsync(d.Name);
        if (!r.Ok) { Dialogs.ShowResult(r, "Terminate"); return false; }
        await Main.WaitForStateAsync(d.Name, running: false, 8000);
        return true;
    }

    // ---------- Configure ----------

    private async Task SetDefaultAsync()
    {
        var r = await Wsl.SetDefaultAsync(Selected!.Name);
        Dialogs.ShowResult(r, "Set default");
        await Main.RefreshDistrosAsync();
    }

    private async Task SetVersionAsync()
    {
        var d = Selected!;
        var target = d.Version == 2 ? "1" : "2";
        var f = FormDialog.Show("Convert WSL version", $"{d.Name} currently uses WSL {d.Version}. Conversion can take several minutes for large distributions.",
            new FormField("v", "Target version") { Kind = FieldKind.Combo, Options = ["2", "1"], Value = target });
        if (f is null || f["v"] == d.Version.ToString()) return;
        var r = await Wsl.SetVersionAsync(d.Name, f["v"]);
        Dialogs.ShowResult(r, "Set version");
        await Main.RefreshDistrosAsync();
    }

    private async Task SetDefaultUserAsync()
    {
        var d = Selected!;
        var users = await Wsl.GetUsersAsync(d.Name);
        var f = FormDialog.Show("Set default user", $"User that {d.Name} logs in as by default.",
            new FormField("u", "User") { Kind = FieldKind.EditableCombo, Options = users.ToArray(), Value = d.DefaultUser, Required = true });
        if (f is null) return;
        var r = await Wsl.SetDefaultUserAsync(d.Name, f["u"].Trim());
        Dialogs.ShowResult(r, "Set default user");
        d.DefaultUser = "";
        await LoadDetailsAsync();
    }

    private async Task CreateUserAsync()
    {
        var d = Selected!;
        var f = FormDialog.Show("Create user", $"Creates a Linux user in {d.Name} with a password and adds it to the sudo/wheel group.",
            new FormField("u", "Username") { Required = true },
            new FormField("p", "Password") { Kind = FieldKind.Password, Required = true },
            new FormField("d", "Make this the default user") { Kind = FieldKind.Check, Value = "true" });
        if (f is null) return;
        var r = await Wsl.CreateUserAsync(d.Name, f["u"].Trim(), f["p"], f["d"] == "true");
        Dialogs.ShowResult(r, "Create user");
        if (r.Ok) Dialogs.Info($"User '{f["u"].Trim()}' created.");
        await LoadDetailsAsync();
    }

    // ---------- Storage ----------

    private async Task ResizeAsync()
    {
        var d = Selected!;
        var f = FormDialog.Show("Resize virtual disk", $"Sets the maximum size of {d.Name}'s virtual disk (current file size: {d.VhdSizeText}).",
            new FormField("s", "New size") { Hint = "e.g. 256GB, 1TB", Required = true });
        if (f is null || !await EnsureStoppedAsync(d, "Resizing")) return;
        var r = await Wsl.ResizeAsync(d.Name, f["s"].Trim());
        Dialogs.ShowResult(r, "Resize");
        await Main.RefreshDistrosAsync();
    }

    private async Task SparseAsync()
    {
        var d = Selected!;
        var f = FormDialog.Show("Sparse VHD", "A sparse VHD automatically gives freed disk space back to Windows.",
            new FormField("s", "Sparse") { Kind = FieldKind.Combo, Options = ["true", "false"], Value = "true" });
        if (f is null || !await EnsureStoppedAsync(d, "Changing sparse mode")) return;
        var r = await Wsl.SetSparseAsync(d.Name, f["s"] == "true");
        Dialogs.ShowResult(r, "Set sparse");
    }

    private async Task MoveAsync()
    {
        var d = Selected!;
        var f = FormDialog.Show("Move distribution", $"Moves {d.Name}'s files from\n{d.BasePath}\nto a new folder.",
            new FormField("p", "New location") { Kind = FieldKind.Folder, Required = true });
        if (f is null || !await EnsureStoppedAsync(d, "Moving")) return;
        var r = await Wsl.MoveAsync(d.Name, f["p"].Trim());
        Dialogs.ShowResult(r, "Move");
        await Main.RefreshDistrosAsync();
    }

    // ---------- Backup ----------

    public static readonly string[] ExportFormats = ["tar", "tar.gz", "tar.xz", "vhd"];

    private async Task ExportAsync()
    {
        var d = Selected!;
        var f = FormDialog.Show("Export distribution", $"Exports {d.Name} to a file you can import later.",
            new FormField("fmt", "Format") { Kind = FieldKind.Combo, Options = ExportFormats, Value = "tar" },
            new FormField("file", "File") { Kind = FieldKind.SaveFile, Filter = "Tar archive|*.tar;*.tar.gz;*.tar.xz|Virtual disk|*.vhdx|All files|*.*", Value = $"{d.Name}-{DateTime.Now:yyyyMMdd}.tar", Required = true });
        if (f is null) return;
        var file = f["file"].Trim();
        if (!Path.IsPathRooted(file)) file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), file);
        var r = await Wsl.ExportAsync(d.Name, file, f["fmt"]);
        Dialogs.ShowResult(r, "Export");
        if (r.Ok) Dialogs.Info($"Exported {d.Name} to\n{file}");
    }

    private async Task CloneAsync()
    {
        var d = Selected!;
        var defaultLoc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wsl");
        var f = FormDialog.Show("Clone distribution", $"Creates an independent copy of {d.Name} (export + import).",
            new FormField("name", "New name") { Value = d.Name + "-clone", Required = true },
            new FormField("loc", "Install folder") { Kind = FieldKind.Folder, Value = defaultLoc, Hint = "A subfolder named after the clone is created here.", Required = true });
        if (f is null) return;
        var name = f["name"].Trim();
        var location = Path.Combine(f["loc"].Trim(), name);
        Directory.CreateDirectory(location);
        var vhd = d.Version == 2;
        var temp = Path.Combine(location, vhd ? "clone-source.vhdx" : "clone-source.tar");
        try
        {
            var r = await Wsl.ExportAsync(d.Name, temp, vhd ? "vhd" : null);
            if (!r.Ok) { Dialogs.ShowResult(r, "Export"); return; }
            r = await Wsl.ImportAsync(name, location, temp, d.Version.ToString(), vhd);
            Dialogs.ShowResult(r, "Import");
            if (r.Ok)
            {
                if (d.DefaultUser.Length > 0) await Wsl.SetDefaultUserAsync(name, d.DefaultUser);
                Dialogs.Info($"Cloned {d.Name} → {name}");
            }
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            await Main.RefreshDistrosAsync();
        }
    }

    private async Task UnregisterAsync()
    {
        var d = Selected!;
        if (!Dialogs.ConfirmByTyping("Unregister distribution",
                $"This permanently deletes {d.Name} and ALL of its files (the virtual disk at {d.VhdPath}). Export it first if you want a backup.",
                d.Name)) return;
        var r = await Wsl.UnregisterAsync(d.Name);
        Dialogs.ShowResult(r, "Unregister");
        Selected = null;
        await Main.RefreshDistrosAsync();
    }
}
