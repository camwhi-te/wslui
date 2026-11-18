using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using WslManager.Infrastructure;

namespace WslManager.Services;

public sealed record DistroListItem(string Name, string State, int Version, bool IsDefault);

public sealed record OnlineDistro(string Name, string FriendlyName);

public sealed record PhysicalDisk(string DeviceId, string Model, long? Size, int? Partitions)
{
    public string SizeText => Format.Bytes(Size);
    public override string ToString() => $"{DeviceId}  {Model}  {SizeText}";
}

public sealed record DistroStats(
    double Load1, long MemTotalKb, long MemAvailKb, long SwapTotalKb, long SwapFreeKb,
    long DiskTotalKb, long DiskUsedKb, int Processes, TimeSpan Uptime);

public sealed record DistroDetails(string OsName, string Kernel, string DefaultUser);

public sealed record InstallOptions
{
    public string? Distro { get; init; }
    public string? Name { get; init; }
    public string? Location { get; init; }
    public string? Version { get; init; }
    public bool FixedVhd { get; init; }
    public string? VhdSize { get; init; }
    public bool WebDownload { get; init; }
    public bool Legacy { get; init; }
    public string? FromFile { get; init; }
    public bool EnableWsl1 { get; init; }
    public bool NoDistribution { get; init; }
}

public sealed record MountOptions
{
    public required string Disk { get; init; }
    public bool Vhd { get; init; }
    public bool Bare { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? Options { get; init; }
    public string? Partition { get; init; }
}

/// <summary>Typed wrapper around every wsl.exe operation.</summary>
public sealed class WslService
{
    public WslCli Cli { get; } = new();

    private readonly Dictionary<string, Process> _keepAlive = new(StringComparer.OrdinalIgnoreCase);

    public Task<CliResult> Run(params string[] args) => Cli.RunAsync(args);

    private static void Add(List<string> a, string flag, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        a.Add(flag);
        a.Add(value.Trim());
    }

    // ---------- Listing ----------

    public async Task<List<DistroListItem>> ListAsync()
    {
        var r = await Cli.RunAsync(["--list", "--verbose"], new RunOptions { Quiet = true });
        var list = new List<DistroListItem>();
        if (!r.Ok) return list; // "no installed distributions" returns non-zero
        foreach (var raw in r.StdOut.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var isDefault = line.StartsWith('*');
            if (isDefault) line = line[1..].Trim();
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || parts[0] == "NAME") continue;
            if (!int.TryParse(parts[^1], out var ver)) continue;
            list.Add(new DistroListItem(parts[0], parts[^2], ver, isDefault));
        }
        return list;
    }

    public async Task<(List<OnlineDistro> Items, string Error)> ListOnlineAsync(bool legacy = false)
    {
        var args = new List<string> { "--list", "--online" };
        if (legacy) args.Add("--legacy");
        var r = await Cli.RunAsync(args);
        var list = new List<OnlineDistro>();
        var started = false;
        foreach (var raw in r.StdOut.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("NAME ", StringComparison.Ordinal)) { started = true; continue; }
            if (!started || line.Length == 0) continue;
            var idx = line.IndexOfAny([' ', '\t']);
            if (idx < 0) list.Add(new OnlineDistro(line, line));
            else list.Add(new OnlineDistro(line[..idx], line[idx..].Trim()));
        }
        return (list, r.Ok ? "" : r.Text);
    }

    // ---------- Lifecycle ----------

    /// <summary>
    /// Boots a distro and keeps it running by holding a hidden, idle wsl.exe session open.
    /// The distro stays up until terminated (or until the session is killed and it idles out).
    /// </summary>
    public void Start(string distro)
    {
        lock (_keepAlive)
        {
            if (_keepAlive.TryGetValue(distro, out var existing) && !existing.HasExited) return;
            var psi = new ProcessStartInfo(WslCli.WslExe) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in new[] { "-d", distro, "--exec", "sh", "-c", "while :; do sleep 3600; done" })
                psi.ArgumentList.Add(a);
            Cli.Log($"[{DateTime.Now:HH:mm:ss}] > wsl -d {Format.Quote(distro)} --exec sh -c <keep-alive>   (holding the distro open)");
            _keepAlive[distro] = Process.Start(psi)!;
        }
    }

    public void ReleaseKeepAlive(string distro)
    {
        lock (_keepAlive)
        {
            if (_keepAlive.Remove(distro, out var p))
            {
                try { if (!p.HasExited) p.Kill(true); } catch { }
                p.Dispose();
            }
        }
    }

    public Task<CliResult> TerminateAsync(string distro)
    {
        ReleaseKeepAlive(distro);
        return Run("--terminate", distro);
    }

    public Task<CliResult> ShutdownAsync(bool force)
    {
        lock (_keepAlive) foreach (var k in _keepAlive.Keys.ToList()) ReleaseKeepAlive(k);
        return force ? Run("--shutdown", "--force") : Run("--shutdown");
    }

    // ---------- Install / remove ----------

    public Task<CliResult> InstallAsync(InstallOptions o, CancellationToken ct = default)
    {
        var a = new List<string> { "--install" };
        if (!string.IsNullOrWhiteSpace(o.FromFile)) Add(a, "--from-file", o.FromFile);
        else if (!string.IsNullOrWhiteSpace(o.Distro)) a.Add(o.Distro.Trim());
        if (o.NoDistribution) a.Add("--no-distribution");
        Add(a, "--name", o.Name);
        Add(a, "--location", o.Location);
        Add(a, "--version", o.Version);
        if (o.FixedVhd) a.Add("--fixed-vhd");
        Add(a, "--vhd-size", o.VhdSize);
        if (o.WebDownload) a.Add("--web-download");
        if (o.Legacy) a.Add("--legacy");
        if (o.EnableWsl1) a.Add("--enable-wsl1");
        // Never let wsl.exe launch an interactive shell inside our hidden process.
        if (!o.NoDistribution) a.Add("--no-launch");
        return Cli.RunAsync(a, new RunOptions { Token = ct });
    }

    public Task<CliResult> UnregisterAsync(string distro)
    {
        ReleaseKeepAlive(distro);
        return Run("--unregister", distro);
    }

    public Task<CliResult> ExportAsync(string distro, string file, string? format)
    {
        var a = new List<string> { "--export", distro, file };
        Add(a, "--format", format);
        return Cli.RunAsync(a);
    }

    public Task<CliResult> ImportAsync(string name, string location, string file, string? version, bool vhd)
    {
        var a = new List<string> { "--import", name, location, file };
        Add(a, "--version", version);
        if (vhd) a.Add("--vhd");
        return Cli.RunAsync(a);
    }

    public Task<CliResult> ImportInPlaceAsync(string name, string vhdFile) => Run("--import-in-place", name, vhdFile);

    // ---------- Disks ----------

    public Task<CliResult> MountAsync(MountOptions o)
    {
        var a = new List<string> { "--mount", o.Disk };
        if (o.Vhd) a.Add("--vhd");
        if (o.Bare) a.Add("--bare");
        Add(a, "--name", o.Name);
        Add(a, "--type", o.Type);
        Add(a, "--options", o.Options);
        Add(a, "--partition", o.Partition);
        return Cli.RunAsync(a);
    }

    public Task<CliResult> UnmountAsync(string? disk) =>
        string.IsNullOrWhiteSpace(disk) ? Run("--unmount") : Run("--unmount", disk);

    public async Task<List<PhysicalDisk>> GetPhysicalDisksAsync()
    {
        var ps = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
        var r = await Cli.RunExeAsync(ps,
            ["-NoProfile", "-NonInteractive", "-Command",
             "Get-CimInstance Win32_DiskDrive | Select-Object DeviceID,Model,Size,Partitions | ConvertTo-Json -Compress"],
            new RunOptions { Quiet = true });
        var list = new List<PhysicalDisk>();
        if (!r.Ok || string.IsNullOrWhiteSpace(r.StdOut)) return list;
        using var doc = JsonDocument.Parse(r.StdOut);
        IEnumerable<JsonElement> items = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray()
            : [doc.RootElement];
        foreach (var e in items)
        {
            list.Add(new PhysicalDisk(
                e.GetProperty("DeviceID").GetString() ?? "",
                e.TryGetProperty("Model", out var m) ? m.GetString() ?? "" : "",
                e.TryGetProperty("Size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : null,
                e.TryGetProperty("Partitions", out var pa) && pa.ValueKind == JsonValueKind.Number ? pa.GetInt32() : null));
        }
        return list;
    }

    // ---------- Distro management ----------

    public Task<CliResult> SetDefaultAsync(string distro) => Run("--set-default", distro);
    public Task<CliResult> SetVersionAsync(string distro, string version) => Run("--set-version", distro, version);
    public Task<CliResult> SetDefaultVersionAsync(string version) => Run("--set-default-version", version);
    public Task<CliResult> MoveAsync(string distro, string location) => Run("--manage", distro, "--move", location);
    public Task<CliResult> SetSparseAsync(string distro, bool sparse) => Run("--manage", distro, "--set-sparse", sparse ? "true" : "false");
    public Task<CliResult> ResizeAsync(string distro, string size) => Run("--manage", distro, "--resize", size);
    public Task<CliResult> SetDefaultUserAsync(string distro, string user) => Run("--manage", distro, "--set-default-user", user);

    public Task<CliResult> UpdateAsync(bool preRelease) => preRelease ? Run("--update", "--pre-release") : Run("--update");
    public Task<CliResult> UninstallWslAsync() => Run("--uninstall");

    // ---------- In-distro helpers ----------

    public Task<CliResult> ExecAsync(string distro, string? user, string script, RunOptions? o = null, params string[] scriptArgs)
    {
        var a = new List<string> { "-d", distro };
        Add(a, "-u", user);
        a.AddRange(["--exec", "sh", "-c", script, "sh"]);
        a.AddRange(scriptArgs);
        return Cli.RunAsync(a, o);
    }

    public async Task<string?> ReadFileAsync(string distro, string path)
    {
        var r = await ExecAsync(distro, "root", "if [ -e \"$1\" ]; then cat \"$1\"; fi", null, path);
        return r.Ok ? r.StdOut : throw new InvalidOperationException(r.Text);
    }

    public async Task WriteFileAsync(string distro, string path, string content)
    {
        content = content.Replace("\r\n", "\n");
        var r = await ExecAsync(distro, "root", "cat > \"$1\"", new RunOptions { Stdin = content }, path);
        if (!r.Ok) throw new InvalidOperationException(r.Text);
        Cli.Log($"  wrote {content.Length} bytes to {distro}:{path}");
    }

    public async Task<List<string>> GetUsersAsync(string distro)
    {
        var r = await ExecAsync(distro, "root",
            "getent passwd | awk -F: '$3==0 || ($3>=1000 && $3<60000) {print $1}'", new RunOptions { Quiet = true });
        return r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>Creates a user with a password and admin group membership, then makes it the default user.</summary>
    public async Task<CliResult> CreateUserAsync(string distro, string user, string password, bool makeDefault)
    {
        const string script = """
            U="$1"
            if ! id "$U" >/dev/null 2>&1; then
              if command -v useradd >/dev/null 2>&1; then
                SH=/bin/sh; [ -x /bin/bash ] && SH=/bin/bash
                useradd -m -s "$SH" "$U" || exit 1
              else
                adduser -D "$U" || exit 1
              fi
            fi
            for g in sudo wheel; do
              if getent group "$g" >/dev/null 2>&1; then
                usermod -aG "$g" "$U" 2>/dev/null || addgroup "$U" "$g" 2>/dev/null
              fi
            done
            chpasswd || exit 1
            echo "User $U is ready."
            """;
        var r = await ExecAsync(distro, "root", script.Replace("\r\n", "\n"),
            new RunOptions { Stdin = $"{user}:{password}\n" }, user);
        if (!r.Ok || !makeDefault) return r;
        return await SetDefaultUserAsync(distro, user);
    }

    public async Task<DistroStats?> GetStatsAsync(string distro)
    {
        const string script =
            "cat /proc/loadavg; echo @@; grep -E '^(MemTotal|MemAvailable|SwapTotal|SwapFree):' /proc/meminfo; echo @@; " +
            "df -kP / 2>/dev/null | tail -n 1; echo @@; ls -d /proc/[0-9]* 2>/dev/null | wc -l; echo @@; cat /proc/uptime";
        var r = await ExecAsync(distro, null, script, new RunOptions { Quiet = true });
        if (!r.Ok) return null;
        try
        {
            var sec = r.StdOut.Split("@@");
            var inv = CultureInfo.InvariantCulture;
            var load = double.Parse(sec[0].Trim().Split(' ')[0], inv);
            var mem = sec[1].Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Split(':'))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => long.Parse(p[1].Trim().Split(' ')[0], inv));
            var df = sec[2].Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            long dfTotal = df.Length > 2 ? long.Parse(df[1], inv) : 0, dfUsed = df.Length > 2 ? long.Parse(df[2], inv) : 0;
            var procs = int.Parse(sec[3].Trim(), inv);
            var up = double.Parse(sec[4].Trim().Split(' ')[0], inv);
            return new DistroStats(load,
                mem.GetValueOrDefault("MemTotal"), mem.GetValueOrDefault("MemAvailable"),
                mem.GetValueOrDefault("SwapTotal"), mem.GetValueOrDefault("SwapFree"),
                dfTotal, dfUsed, procs, TimeSpan.FromSeconds(up));
        }
        catch { return null; }
    }

    public async Task<DistroDetails?> GetDetailsAsync(string distro)
    {
        const string script = "( . /etc/os-release 2>/dev/null; echo \"${PRETTY_NAME:-unknown}\" ); uname -r; id -un";
        var r = await ExecAsync(distro, null, script, new RunOptions { Quiet = true });
        if (!r.Ok) return null;
        var l = r.StdOut.Split('\n');
        return new DistroDetails(l.ElementAtOrDefault(0)?.Trim() ?? "", l.ElementAtOrDefault(1)?.Trim() ?? "", l.ElementAtOrDefault(2)?.Trim() ?? "");
    }

    // ---------- External windows ----------

    /// <summary>Opens wsl.exe with the given arguments in a new console window (user's default terminal).</summary>
    public void OpenTerminal(params string[] args)
    {
        var argString = string.Join(' ', args.Select(Format.Quote));
        Cli.Log($"[{DateTime.Now:HH:mm:ss}] > wsl {argString}   (new terminal window)");
        Process.Start(new ProcessStartInfo(WslCli.WslExe, argString) { UseShellExecute = true });
    }

    public static void OpenExplorer(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", Format.Quote(path)) { UseShellExecute = true });
}
