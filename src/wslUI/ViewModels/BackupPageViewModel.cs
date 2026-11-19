using System.IO;
using System.Windows.Input;
using wslUI.Infrastructure;
using wslUI.Services;

namespace wslUI.ViewModels;

/// <summary>Export / import / register-in-place, each as a single guided dialog.</summary>
public sealed class BackupPageViewModel : PageViewModel
{
    private static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wsl");
    private static string Documents => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public BackupPageViewModel(MainViewModel main) : base(main, "Backup & import", "", "MANAGE")
    {
        ExportCommand = new AsyncCommand(ExportAsync, () => Main.HasDistros);
        ImportCommand = new AsyncCommand(ImportAsync);
        ImportInPlaceCommand = new AsyncCommand(ImportInPlaceAsync);
    }

    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ImportInPlaceCommand { get; }

    private string _lastResult = "";
    public string LastResult { get => _lastResult; set => Set(ref _lastResult, value); }

    private async Task ExportAsync()
    {
        var names = Main.Distros.Select(d => d.Name).ToArray();
        var preselect = Main.DistrosPage.Selected?.Name ?? names.FirstOrDefault() ?? "";
        var f = FormDialog.ShowWithIcon("", "Export a distribution",
            "Saves a complete copy of a distribution to a single file you can import later or on another PC.", "Export",
            new FormField("d", "Distribution") { Kind = FieldKind.Combo, Options = names, Value = preselect, Required = true },
            new FormField("file", "Save to") { Kind = FieldKind.SaveFile, Required = true,
                Value = Path.Combine(Documents, $"{preselect}-{DateTime.Now:yyyyMMdd}.tar"),
                Filter = "Tar archive|*.tar;*.tar.gz;*.tar.xz|Virtual disk|*.vhdx|All files|*.*" },
            new FormField("fmt", "Format") { Kind = FieldKind.Combo, Options = DistrosPageViewModel.ExportFormats, Value = "tar", Advanced = true,
                Hint = "tar is the most portable. vhd exports the raw disk (fastest for WSL 2)." });
        if (f is null) return;
        var r = await Wsl.ExportAsync(f["d"], f["file"].Trim(), f["fmt"]);
        Dialogs.ShowResult(r, "Export");
        if (r.Ok) LastResult = $"Exported {f["d"]} → {f["file"].Trim()}";
    }

    private async Task ImportAsync()
    {
        var f = FormDialog.ShowWithIcon("", "Import a distribution",
            "Creates a new distribution from a .tar archive or a .vhdx disk (the disk is copied).", "Import",
            new FormField("file", "Source file") { Kind = FieldKind.OpenFile, Required = true,
                Filter = "Distribution archive or disk|*.tar;*.tar.gz;*.tar.xz;*.wsl;*.vhdx;*.vhd|All files|*.*" },
            new FormField("name", "Name for the new distribution") { Required = true },
            new FormField("loc", "Install folder") { Kind = FieldKind.Folder, Advanced = true, Hint = $"Default: {DefaultRoot}\\<name>" },
            new FormField("ver", "WSL version") { Kind = FieldKind.Combo, Options = ["2", "1"], Value = "2", Advanced = true });
        if (f is null) return;
        var file = f["file"].Trim();
        var name = f["name"].Trim();
        var loc = string.IsNullOrWhiteSpace(f["loc"]) ? Path.Combine(DefaultRoot, name) : f["loc"].Trim();
        var isVhd = file.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase);
        Directory.CreateDirectory(loc);
        var r = await Wsl.ImportAsync(name, loc, file, f["ver"], isVhd);
        Dialogs.ShowResult(r, "Import");
        await Main.RefreshDistrosAsync();
        if (r.Ok) LastResult = $"Imported {name} into {loc}. If it logs in as root, set a default user on the Overview page.";
    }

    private async Task ImportInPlaceAsync()
    {
        var f = FormDialog.ShowWithIcon("", "Register an existing disk",
            "Registers an ext4 .vhdx as a distribution without copying it — the file stays where it is.", "Register",
            new FormField("file", "VHDX file") { Kind = FieldKind.OpenFile, Required = true, Filter = "Virtual disk|*.vhdx;*.vhd" },
            new FormField("name", "Name") { Required = true });
        if (f is null) return;
        var r = await Wsl.ImportInPlaceAsync(f["name"].Trim(), f["file"].Trim());
        Dialogs.ShowResult(r, "Register");
        await Main.RefreshDistrosAsync();
        if (r.Ok) LastResult = $"Registered {f["name"].Trim()} from {f["file"].Trim()}";
    }
}
