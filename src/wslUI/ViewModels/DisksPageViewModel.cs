using System.Collections.ObjectModel;
using System.Windows.Input;
using wslUI.Infrastructure;
using wslUI.Services;

namespace wslUI.ViewModels;

public sealed class DisksPageViewModel : PageViewModel
{
    private static readonly string[] FsTypes = ["ext4", "vfat", "btrfs", "xfs", "ntfs3", "exfat"];

    public DisksPageViewModel(MainViewModel main) : base(main, "Disks", "", "MANAGE")
    {
        RefreshDisksCommand = new AsyncCommand(RefreshDisksAsync);
        MountDiskCommand = new AsyncCommand(p => p is PhysicalDisk d ? MountAsync(d.DeviceId, false, $"{d.Model} ({d.SizeText})") : Task.CompletedTask);
        MountVhdCommand = new AsyncCommand(async () =>
        {
            if (Dialogs.OpenFile("Virtual disk|*.vhdx;*.vhd|All files|*.*", "Choose a virtual disk") is { } file)
                await MountAsync(file, true, System.IO.Path.GetFileName(file));
        });
        UnmountCommand = new AsyncCommand(p => UnmountAsync(p as string), p => p is string);
        UnmountAllCommand = new AsyncCommand(() => UnmountAsync(null));
        LsblkCommand = new AsyncCommand(LsblkAsync, () => Main.HasDistros);
    }

    public ObservableCollection<PhysicalDisk> PhysicalDisks { get; } = [];
    public ObservableCollection<string> Mounted { get; } = [];

    public ICommand RefreshDisksCommand { get; }
    public ICommand MountDiskCommand { get; }
    public ICommand MountVhdCommand { get; }
    public ICommand UnmountCommand { get; }
    public ICommand UnmountAllCommand { get; }
    public ICommand LsblkCommand { get; }

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

    private async Task MountAsync(string disk, bool isVhd, string label)
    {
        if (!isVhd && disk.Contains("PHYSICALDRIVE0", StringComparison.OrdinalIgnoreCase) &&
            !Dialogs.Confirm("PHYSICALDRIVE0 is usually your Windows system disk. Mounting it in WSL detaches it from Windows and can fail or cause problems. Continue?", warning: true))
            return;

        var f = FormDialog.ShowWithIcon(isVhd ? "" : "", $"Mount {label}",
            "The disk is attached to the WSL 2 VM and appears in every distro under /mnt/wsl.", "Mount",
            new FormField("name", "Mount name") { Hint = "Folder name under /mnt/wsl. Leave empty to let WSL choose." },
            new FormField("type", "Filesystem") { Kind = FieldKind.EditableCombo, Options = FsTypes, Value = "ext4", Advanced = true },
            new FormField("part", "Partition number") { Advanced = true, Hint = "Leave empty to mount the whole disk." },
            new FormField("opts", "Mount options") { Advanced = true, Hint = "e.g. ro,noatime" },
            new FormField("bare", "Attach only — don't mount (--bare)") { Kind = FieldKind.Check, Advanced = true });
        if (f is null) return;

        var r = await Wsl.MountAsync(new MountOptions
        {
            Disk = disk,
            Vhd = isVhd,
            Bare = f["bare"] == "true",
            Name = f["name"],
            Type = f["type"] == "ext4" ? null : f["type"],
            Options = f["opts"],
            Partition = f["part"],
        });
        Dialogs.ShowResult(r, "Mount");
        if (!r.Ok) return;
        if (!Mounted.Contains(disk)) Mounted.Add(disk);
        if (!string.IsNullOrWhiteSpace(r.Text)) Dialogs.Info(r.Text, "Mounted");
    }

    private async Task UnmountAsync(string? disk)
    {
        if (disk is null && !Dialogs.Confirm("Unmount and detach ALL disks from WSL?")) return;
        var r = await Wsl.UnmountAsync(disk);
        Dialogs.ShowResult(r, "Unmount");
        if (!r.Ok) return;
        if (disk is null) Mounted.Clear();
        else Mounted.Remove(disk);
    }

    private async Task LsblkAsync()
    {
        var distro = Main.Distros.FirstOrDefault(d => d.IsDefault) ?? Main.Distros[0];
        var r = await Wsl.ExecAsync(distro.Name, "root",
            "lsblk -o NAME,SIZE,TYPE,FSTYPE,MOUNTPOINT 2>/dev/null || cat /proc/partitions");
        Lsblk = $"# from {distro.Name}\n" + r.Text;
    }
}
