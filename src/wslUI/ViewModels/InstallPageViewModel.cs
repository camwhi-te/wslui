using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using wslUI.Infrastructure;
using wslUI.Services;

namespace wslUI.ViewModels;

public sealed class InstallPageViewModel : PageViewModel
{
    public InstallPageViewModel(MainViewModel main) : base(main, "Install", "\uE896", "MANAGE")
    {
        OnlineView = CollectionViewSource.GetDefaultView(Online);
        OnlineView.Filter = o => string.IsNullOrWhiteSpace(Filter) || o is CatalogItem d &&
            (d.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase) || d.FriendlyName.Contains(Filter, StringComparison.OrdinalIgnoreCase));

        LoadOnlineCommand = new AsyncCommand(LoadOnlineAsync);
        InstallCommand = new AsyncCommand(() => InstallAsync(fromFile: false), () => Selected is not null);
        InstallFromFileCommand = new AsyncCommand(async () =>
        {
            if (Dialogs.OpenFile("WSL distribution|*.wsl;*.tar;*.tar.gz;*.tar.xz|All files|*.*", "Install from file") is not { } file) return;
            FromFile = file;
            await InstallAsync(fromFile: true);
        });
        BrowseLocationCommand = new RelayCommand(() => { if (Dialogs.PickFolder() is { } p) Location = p; });
        BrowseFileCommand = new RelayCommand(() =>
        {
            if (Dialogs.OpenFile("WSL distribution|*.wsl;*.tar;*.tar.gz;*.tar.xz|All files|*.*") is { } p) FromFile = p;
        });
    }

    public ObservableCollection<CatalogItem> Online { get; } = [];
    public ICollectionView OnlineView { get; }

    public ICommand LoadOnlineCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand InstallFromFileCommand { get; }
    public ICommand BrowseLocationCommand { get; }
    public ICommand BrowseFileCommand { get; }

    private string _filter = "";
    public string Filter { get => _filter; set { if (Set(ref _filter, value)) OnlineView.Refresh(); } }

    private CatalogItem? _selected;
    public CatalogItem? Selected { get => _selected; set => Set(ref _selected, value); }

    private bool _loading;
    public bool IsLoading { get => _loading; set => Set(ref _loading, value); }

    private string _onlineError = "";
    public string OnlineError { get => _onlineError; set => Set(ref _onlineError, value); }

    // ----- options -----
    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _location = "";
    public string Location { get => _location; set => Set(ref _location, value); }

    public string[] Versions { get; } = ["2", "1"];
    private string _version = "2";
    public string Version { get => _version; set => Set(ref _version, value); }

    private bool _fixedVhd;
    public bool FixedVhd { get => _fixedVhd; set => Set(ref _fixedVhd, value); }

    private string _vhdSize = "";
    public string VhdSize { get => _vhdSize; set => Set(ref _vhdSize, value); }

    private bool _webDownload;
    public bool WebDownload { get => _webDownload; set => Set(ref _webDownload, value); }

    private bool _legacy;
    public bool Legacy { get => _legacy; set { if (Set(ref _legacy, value)) _ = LoadOnlineAsync(); } }

    private bool _createUser = true;
    public bool CreateUser { get => _createUser; set => Set(ref _createUser, value); }

    private string _userName = "";
    public string UserName { get => _userName; set => Set(ref _userName, value); }

    /// <summary>Set from the view's PasswordBox (not bindable).</summary>
    public string Password { get; set; } = "";

    private bool _openTerminal;
    public bool OpenTerminalAfter { get => _openTerminal; set => Set(ref _openTerminal, value); }

    private string _fromFile = "";
    public string FromFile { get => _fromFile; set => Set(ref _fromFile, value); }

    private string _status = "";
    public string Status { get => _status; set => Set(ref _status, value); }

    public override void OnNavigatedTo()
    {
        if (Online.Count == 0 && !IsLoading) _ = LoadOnlineAsync();
    }

    private async Task LoadOnlineAsync()
    {
        IsLoading = true;
        try
        {
            var (items, error) = await Wsl.ListOnlineAsync(Legacy);
            Online.Clear();
            foreach (var i in items) Online.Add(new CatalogItem(i.Name, i.FriendlyName, Main.FindDistro(i.Name) is not null));
            OnlineError = error;
        }
        finally { IsLoading = false; }
    }

    private async Task InstallAsync(bool fromFile)
    {
        if (CreateUser && (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrEmpty(Password)))
        {
            Dialogs.Error("Enter a username and password, or untick \"Create a default user\".");
            return;
        }

        var before = (await Wsl.ListAsync()).Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var opts = new InstallOptions
        {
            Distro = fromFile ? null : Selected?.Name,
            FromFile = fromFile ? FromFile : null,
            Name = Name,
            Location = Location,
            Version = Version == "2" ? null : Version,
            FixedVhd = FixedVhd,
            VhdSize = VhdSize,
            WebDownload = WebDownload,
            Legacy = Legacy && !fromFile,
        };
        Status = "Installing… (progress is in the command log below)";
        var r = await Wsl.InstallAsync(opts);
        if (!r.Ok)
        {
            Status = "Install failed.";
            Dialogs.ShowResult(r, "Install");
            return;
        }

        // Work out the installed name: explicit --name, else whatever new distro appeared.
        var after = await Wsl.ListAsync();
        var installed = !string.IsNullOrWhiteSpace(Name) ? Name.Trim()
            : after.Select(d => d.Name).FirstOrDefault(n => !before.Contains(n)) ?? Selected?.Name;
        await Main.RefreshDistrosAsync();

        if (installed is not null && CreateUser)
        {
            Status = $"Creating user {UserName}…";
            var u = await Wsl.CreateUserAsync(installed, UserName.Trim(), Password, makeDefault: true);
            if (!u.Ok) Dialogs.ShowResult(u, "Create user");
        }
        Status = $"Installed {installed}.";
        if (installed is not null && OpenTerminalAfter) Wsl.OpenTerminal("-d", installed);
        Dialogs.Info($"{installed ?? "Distribution"} installed." +
                     (CreateUser ? "" : "\n\nOpen it in a terminal to finish first-run setup (create your user)."));
    }
}

/// <summary>Catalog entry plus whether a distro with that name is already registered.</summary>
public sealed record CatalogItem(string Name, string FriendlyName, bool IsInstalled);
