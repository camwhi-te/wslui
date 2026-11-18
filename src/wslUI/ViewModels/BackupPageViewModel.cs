using System.IO;
using System.Windows.Input;
using wslUI.Infrastructure;
using wslUI.Models;
using wslUI.Services;

namespace wslUI.ViewModels;

public sealed class BackupPageViewModel : PageViewModel
{
    public BackupPageViewModel(MainViewModel main) : base(main, "Backup & Import", "")
    {
        ExportCommand = new AsyncCommand(ExportAsync, () => ExportDistro is not null && !string.IsNullOrWhiteSpace(ExportFile));
        BrowseExportCommand = new RelayCommand(() =>
        {
            var ext = ExportFormat == "vhd" ? "vhdx" : ExportFormat;
            if (Dialogs.SaveFile($"{ExportFormat}|*.{ext}|All files|*.*", $"{ExportDistro?.Name ?? "distro"}-{DateTime.Now:yyyyMMdd}.{ext}") is { } p)
                ExportFile = p;
        });

        ImportCommand = new AsyncCommand(ImportAsync, () =>
            !string.IsNullOrWhiteSpace(ImportName) && !string.IsNullOrWhiteSpace(ImportLocation) && !string.IsNullOrWhiteSpace(ImportFile));
        BrowseImportFileCommand = new RelayCommand(() =>
        {
            if (Dialogs.OpenFile("Distribution archive or disk|*.tar;*.tar.gz;*.tar.xz;*.wsl;*.vhdx;*.vhd|All files|*.*") is { } p)
            {
                ImportFile = p;
                ImportIsVhd = p.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrWhiteSpace(ImportName)) ImportName = Path.GetFileNameWithoutExtension(p).Split('.')[0];
            }
        });
        BrowseImportLocationCommand = new RelayCommand(() => { if (Dialogs.PickFolder() is { } p) ImportLocation = p; });

        ImportInPlaceCommand = new AsyncCommand(ImportInPlaceAsync, () => !string.IsNullOrWhiteSpace(InPlaceName) && !string.IsNullOrWhiteSpace(InPlaceFile));
        BrowseInPlaceCommand = new RelayCommand(() =>
        {
            if (Dialogs.OpenFile("Virtual disk|*.vhdx;*.vhd") is { } p)
            {
                InPlaceFile = p;
                if (string.IsNullOrWhiteSpace(InPlaceName)) InPlaceName = Path.GetFileNameWithoutExtension(p);
            }
        });
    }

    public string[] Formats => DistrosPageViewModel.ExportFormats;

    public ICommand ExportCommand { get; }
    public ICommand BrowseExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand BrowseImportFileCommand { get; }
    public ICommand BrowseImportLocationCommand { get; }
    public ICommand ImportInPlaceCommand { get; }
    public ICommand BrowseInPlaceCommand { get; }

    private Distro? _exportDistro;
    public Distro? ExportDistro { get => _exportDistro; set => Set(ref _exportDistro, value); }

    private string _exportFile = "";
    public string ExportFile { get => _exportFile; set => Set(ref _exportFile, value); }

    private string _exportFormat = "tar";
    public string ExportFormat { get => _exportFormat; set => Set(ref _exportFormat, value); }

    private string _importName = "";
    public string ImportName
    {
        get => _importName;
        set
        {
            if (!Set(ref _importName, value)) return;
            // Suggest a location the first time a name is entered.
            if (string.IsNullOrWhiteSpace(ImportLocation) || ImportLocation.StartsWith(DefaultRoot, StringComparison.OrdinalIgnoreCase))
                ImportLocation = string.IsNullOrWhiteSpace(value) ? "" : Path.Combine(DefaultRoot, value.Trim());
        }
    }

    private static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wsl");

    private string _importLocation = "";
    public string ImportLocation { get => _importLocation; set => Set(ref _importLocation, value); }

    private string _importFile = "";
    public string ImportFile { get => _importFile; set => Set(ref _importFile, value); }

    public string[] Versions { get; } = ["2", "1"];
    private string _importVersion = "2";
    public string ImportVersion { get => _importVersion; set => Set(ref _importVersion, value); }

    private bool _importIsVhd;
    public bool ImportIsVhd { get => _importIsVhd; set => Set(ref _importIsVhd, value); }

    private string _inPlaceName = "";
    public string InPlaceName { get => _inPlaceName; set => Set(ref _inPlaceName, value); }

    private string _inPlaceFile = "";
    public string InPlaceFile { get => _inPlaceFile; set => Set(ref _inPlaceFile, value); }

    public override void OnNavigatedTo()
    {
        ExportDistro ??= Main.DistrosPage.Selected ?? Main.Distros.FirstOrDefault();
    }

    private async Task ExportAsync()
    {
        var r = await Wsl.ExportAsync(ExportDistro!.Name, ExportFile.Trim(), ExportFormat);
        Dialogs.ShowResult(r, "Export");
        if (r.Ok) Dialogs.Info($"Exported {ExportDistro.Name} to\n{ExportFile}");
    }

    private async Task ImportAsync()
    {
        Directory.CreateDirectory(ImportLocation.Trim());
        var r = await Wsl.ImportAsync(ImportName.Trim(), ImportLocation.Trim(), ImportFile.Trim(), ImportVersion, ImportIsVhd);
        Dialogs.ShowResult(r, "Import");
        await Main.RefreshDistrosAsync();
        if (r.Ok) Dialogs.Info($"Imported {ImportName}. If it logs in as root, pick a user under Distributions → Set default user.");
    }

    private async Task ImportInPlaceAsync()
    {
        var r = await Wsl.ImportInPlaceAsync(InPlaceName.Trim(), InPlaceFile.Trim());
        Dialogs.ShowResult(r, "Import in place");
        await Main.RefreshDistrosAsync();
        if (r.Ok) Dialogs.Info($"Registered {InPlaceName} using the disk in place.");
    }
}
