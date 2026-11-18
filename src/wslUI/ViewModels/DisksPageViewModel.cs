using System.Collections.ObjectModel;
using System.Windows.Input;
using wslUI.Infrastructure;
using wslUI.Services;

namespace wslUI.ViewModels;

public sealed class DisksPageViewModel : PageViewModel
{
    public DisksPageViewModel(MainViewModel main) : base(main, "Disks", "")
    {
        RefreshDisksCommand = new AsyncCommand(RefreshDisksAsync);
        BrowseVhdCommand = new RelayCommand(() =>
        {
            if (Dialogs.OpenFile("Virtual disk|*.vhdx;*.vhd|All files|*.*") is { } p) { DiskPath = p; IsVhd = true; }
        });
        MountCommand = new AsyncCommand(MountAsync, () => !string.IsNullOrWhiteSpace(DiskPath));
        UnmountCommand = new AsyncCommand(() => UnmountAsync(SelectedMounted), () => SelectedMounted is not null);
        UnmountPathCommand = new AsyncCommand(() => UnmountAsync(DiskPath), () => !string.IsNullOrWhiteSpace(DiskPath));
        UnmountAllCommand = new AsyncCommand(() => UnmountAsync(null));
        LsblkCommand = new AsyncCommand(LsblkAsync, () => Main.Distros.Count > 0);
    }

    public ObservableCollection<PhysicalDisk> PhysicalDisks { get; } = [];
    public ObservableCollection<string> Mounted { get; } = [];

    public ICommand RefreshDisksCommand { get; }
    public ICommand BrowseVhdCommand { get; }
    public ICommand MountCommand { get; }
    public ICommand UnmountCommand { get; }
    public ICommand UnmountPathCommand { get; }
    public ICommand UnmountAllCommand { get; }
    public ICommand LsblkCommand { get; }

    private PhysicalDisk? _selectedDisk;
    public PhysicalDisk? SelectedDisk
    {
        get => _selectedDisk;
        set
        {
            if (!Set(ref _selectedDisk, value) || value is null) return;
            DiskPath = value.DeviceId;
            IsVhd = false;
        }
    }

    private string _diskPath = "";
    public string DiskPath { get => _diskPath; set => Set(ref _diskPath, value); }

    private bool _isVhd;
    public bool IsVhd { get => _isVhd; set => Set(ref _isVhd, value); }

    private bool _bare;
    public bool Bare { get => _bare; set => Set(ref _bare, value); }

    private string _mountName = "";
    public string MountName { get => _mountName; set => Set(ref _mountName, value); }

    public string[] FsTypes { get; } = ["", "ext4", "vfat", "btrfs", "xfs", "ntfs3", "exfat"];
    private string _fsType = "";
    public string FsType { get => _fsType; set => Set(ref _fsType, value); }

    private string _mountOptions = "";
    public string MountOptions { get => _mountOptions; set => Set(ref _mountOptions, value); }

    private string _partition = "";
    public string Partition { get => _partition; set => Set(ref _partition, value); }

    private string? _selectedMounted;
    public string? SelectedMounted { get => _selectedMounted; set => Set(ref _selectedMounted, value); }

    private string _lsblk = "";
    public string Lsblk { get => _lsblk; set => Set(ref _lsblk, value); }

    public override void OnNavigatedTo()
    {
        if (PhysicalDisks.Count == 0) _ = RefreshDisksAsync();
    }

    private async Task RefreshDisksAsync()
    {
        var disks = await Wsl.GetPhysicalDisksAsync();
        PhysicalDisks.Clear();
        foreach (var d in disks) PhysicalDisks.Add(d);
    }

    private async Task MountAsync()
    {
        if (!IsVhd && DiskPath.Contains("PHYSICALDRIVE0", StringComparison.OrdinalIgnoreCase) &&
            !Dialogs.Confirm("PHYSICALDRIVE0 is usually your Windows system disk. Mounting it in WSL detaches it from Windows and can fail or cause problems. Continue?", warning: true))
            return;
        var r = await Wsl.MountAsync(new MountOptions
        {
            Disk = DiskPath.Trim(),
            Vhd = IsVhd,
            Bare = Bare,
            Name = MountName,
            Type = FsType,
            Options = MountOptions,
            Partition = Partition,
        });
        Dialogs.ShowResult(r, "Mount");
        if (r.Ok)
        {
            if (!Mounted.Contains(DiskPath.Trim())) Mounted.Add(DiskPath.Trim());
            if (!string.IsNullOrWhiteSpace(r.Text)) Dialogs.Info(r.Text, "Mounted");
        }
    }

    private async Task UnmountAsync(string? disk)
    {
        if (disk is null && !Dialogs.Confirm("Unmount and detach ALL disks from WSL?")) return;
        var r = await Wsl.UnmountAsync(disk?.Trim());
        Dialogs.ShowResult(r, "Unmount");
        if (!r.Ok) return;
        if (disk is null) Mounted.Clear();
        else Mounted.Remove(disk.Trim());
    }

    private async Task LsblkAsync()
    {
        var distro = Main.Distros.FirstOrDefault(d => d.IsDefault) ?? Main.Distros[0];
        var r = await Wsl.ExecAsync(distro.Name, "root",
            "lsblk -o NAME,SIZE,TYPE,FSTYPE,MOUNTPOINT 2>/dev/null || cat /proc/partitions");
        Lsblk = $"(from {distro.Name})\n" + r.Text;
    }
}
