using wslUI.Infrastructure;
using wslUI.Services;

namespace wslUI.Models;

public sealed class Distro : ObservableObject
{
    public Distro(string name) => Name = name;

    public string Name { get; }

    private string _state = "";
    public string State
    {
        get => _state;
        set
        {
            if (!Set(ref _state, value)) return;
            OnPropertyChanged(nameof(IsRunning));
            if (!IsRunning) ClearStats();
        }
    }

    public bool IsRunning => _state == "Running";

    private int _version;
    public int Version { get => _version; set => Set(ref _version, value); }

    private bool _isDefault;
    public bool IsDefault { get => _isDefault; set => Set(ref _isDefault, value); }

    private string _guid = "";
    public string Guid { get => _guid; set => Set(ref _guid, value); }

    private string _basePath = "";
    public string BasePath { get => _basePath; set => Set(ref _basePath, value); }

    private string _vhdPath = "";
    public string VhdPath { get => _vhdPath; set => Set(ref _vhdPath, value); }

    private long? _vhdSize;
    public long? VhdSize
    {
        get => _vhdSize;
        set { if (Set(ref _vhdSize, value)) OnPropertyChanged(nameof(VhdSizeText)); }
    }
    public string VhdSizeText => Format.Bytes(_vhdSize);

    private int? _defaultUid;
    public int? DefaultUid { get => _defaultUid; set => Set(ref _defaultUid, value); }

    // ----- live stats (only while running) -----

    private DistroStats? _stats;
    public DistroStats? Stats
    {
        get => _stats;
        set
        {
            if (!Set(ref _stats, value)) return;
            OnPropertyChanged(nameof(DiskText));
            OnPropertyChanged(nameof(ProcessText));
            OnPropertyChanged(nameof(UptimeText));
            OnPropertyChanged(nameof(DiskPercent));
        }
    }

    public string DiskText => _stats is { DiskTotalKb: > 0 } s ? $"{Format.Bytes(s.DiskUsedKb * 1024)} / {Format.Bytes(s.DiskTotalKb * 1024)}" : "";
    public double DiskPercent => _stats is { DiskTotalKb: > 0 } s ? 100.0 * s.DiskUsedKb / s.DiskTotalKb : 0;
    public string ProcessText => _stats is { } s ? s.Processes.ToString() : "";
    public string UptimeText => _stats is { } s ? Format.Duration(s.Uptime) : "";

    // ----- details (fetched on demand while running) -----

    private string _osName = "";
    public string OsName { get => _osName; set => Set(ref _osName, value); }

    private string _kernel = "";
    public string Kernel { get => _kernel; set => Set(ref _kernel, value); }

    private string _defaultUser = "";
    public string DefaultUser { get => _defaultUser; set => Set(ref _defaultUser, value); }

    private void ClearStats() => Stats = null;

    public override string ToString() => Name;
}
