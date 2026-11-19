using wslUI.Infrastructure;
using wslUI.Services;

namespace wslUI.Models;

public sealed class Distro : ObservableObject
{
    public const int HistoryLength = 60;

    private readonly List<double> _cpuHistory = [];
    private readonly List<double> _memHistory = [];
    private DistroStats? _prevSample;

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
            OnPropertyChanged(nameof(StateKind));
            if (!IsRunning) ClearStats();
        }
    }

    public bool IsRunning => _state == "Running";

    /// <summary>running / stopped / busy — drives the status colour.</summary>
    public string StateKind => _state switch
    {
        "Running" => "running",
        "Stopped" => "stopped",
        _ => "busy",
    };

    private int _version;
    public int Version { get => _version; set { if (Set(ref _version, value)) OnPropertyChanged(nameof(Subtitle)); } }

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
        set { if (Set(ref _vhdSize, value)) OnPropertyChanged(nameof(DiskSummary)); }
    }

    private int? _defaultUid;
    public int? DefaultUid { get => _defaultUid; set => Set(ref _defaultUid, value); }

    // ----- live metrics (only while running) -----

    private DistroStats? _stats;
    public DistroStats? Stats { get => _stats; private set => Set(ref _stats, value); }

    private double? _cpuPercent;
    /// <summary>Share of the whole VM's CPU capacity used by this distro's processes.</summary>
    public double? CpuPercent { get => _cpuPercent; private set => Set(ref _cpuPercent, value); }

    private long? _memBytes;
    public long? MemBytes { get => _memBytes; private set => Set(ref _memBytes, value); }

    private double? _memPercent;
    public double? MemPercent { get => _memPercent; private set => Set(ref _memPercent, value); }

    private double[] _cpuHistoryView = [];
    public double[] CpuHistory { get => _cpuHistoryView; private set => Set(ref _cpuHistoryView, value); }

    private double[] _memHistoryView = [];
    public double[] MemHistory { get => _memHistoryView; private set => Set(ref _memHistoryView, value); }

    public string CpuText => _cpuPercent is { } c ? $"{c:0.0}%" : "—";
    public string MemText => _memBytes is { } m ? Format.Bytes(m) : "—";
    public string UptimeText => _stats is { } s ? Format.Duration(s.Uptime) : "—";
    public string ProcessText => _stats is { } s ? s.Processes.ToString() : "—";

    /// <summary>Single disk line for the inspector: space used inside Linux + size of the VHD file on Windows.</summary>
    public string DiskSummary
    {
        get
        {
            var file = _vhdSize is { } v ? $"VHD file {Format.Bytes(v)}" : "";
            if (_stats is not { DiskTotalKb: > 0 } s) return file.Length > 0 ? file : "—";
            var used = $"{Format.Bytes(s.DiskUsedKb * 1024)} used of {Format.Bytes(s.DiskTotalKb * 1024)}";
            return file.Length > 0 ? $"{used}  ·  {file}" : used;
        }
    }

    public double DiskPercent => _stats is { DiskTotalKb: > 0 } s ? 100.0 * s.DiskUsedKb / s.DiskTotalKb : 0;

    /// <summary>Feeds a new sample; CPU % is derived from the delta to the previous one.</summary>
    public void AddSample(DistroStats s)
    {
        if (_prevSample is { } p)
        {
            var dTotal = s.CpuTotalJiffies - p.CpuTotalJiffies;
            var dProc = s.ProcJiffies - p.ProcJiffies;
            if (dTotal > 0) CpuPercent = Math.Clamp(100.0 * Math.Max(0, dProc) / dTotal, 0, 100);
        }
        _prevSample = s;
        Stats = s;
        MemBytes = s.RssBytes;
        MemPercent = s.MemTotalKb > 0 ? Math.Clamp(100.0 * s.RssBytes / (s.MemTotalKb * 1024.0), 0, 100) : null;

        if (CpuPercent is { } cpu) Push(_cpuHistory, cpu);
        if (MemPercent is { } mem) Push(_memHistory, mem);
        CpuHistory = [.. _cpuHistory];
        MemHistory = [.. _memHistory];
        RaiseTexts();
    }

    private static void Push(List<double> list, double v)
    {
        list.Add(v);
        if (list.Count > HistoryLength) list.RemoveAt(0);
    }

    private void RaiseTexts()
    {
        OnPropertyChanged(nameof(CpuText));
        OnPropertyChanged(nameof(MemText));
        OnPropertyChanged(nameof(UptimeText));
        OnPropertyChanged(nameof(ProcessText));
        OnPropertyChanged(nameof(DiskSummary));
        OnPropertyChanged(nameof(DiskPercent));
    }

    // ----- details (fetched on demand while running) -----

    private string _osName = "";
    public string OsName { get => _osName; set { if (Set(ref _osName, value)) OnPropertyChanged(nameof(Subtitle)); } }

    private string _kernel = "";
    public string Kernel { get => _kernel; set => Set(ref _kernel, value); }

    private string _defaultUser = "";
    public string DefaultUser { get => _defaultUser; set => Set(ref _defaultUser, value); }

    /// <summary>"Ubuntu 26.04 LTS · WSL 2" (OS name once known).</summary>
    public string Subtitle => (_osName.Length > 0 ? _osName + "  ·  " : "") + $"WSL {_version}";

    private void ClearStats()
    {
        _prevSample = null;
        _cpuHistory.Clear();
        _memHistory.Clear();
        Stats = null;
        CpuPercent = null;
        MemBytes = null;
        MemPercent = null;
        CpuHistory = [];
        MemHistory = [];
        RaiseTexts();
    }

    public override string ToString() => Name;
}
