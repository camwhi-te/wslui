using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WslManager.Infrastructure;

namespace WslManager.Services;

public sealed record CliResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Ok => ExitCode == 0;

    public string Text => string.IsNullOrWhiteSpace(StdErr) ? StdOut.Trim() : (StdOut.TrimEnd() + "\n" + StdErr).Trim();
}

public sealed class RunOptions
{
    /// <summary>Text written to the process's stdin (then stdin is closed).</summary>
    public string? Stdin { get; init; }

    /// <summary>Don't echo the command or its output into the command log.</summary>
    public bool Quiet { get; init; }

    public Action<string>? OnLine { get; init; }

    public CancellationToken Token { get; init; }
}

/// <summary>Low-level process runner for wsl.exe (and a few helper executables).</summary>
public sealed partial class WslCli
{
    public static readonly string WslExe = Path.Combine(Environment.SystemDirectory, "wsl.exe");
    private static readonly UTF8Encoding Utf8 = new(false);

    private CancellationTokenSource _cancelAll = new();
    private int _active;

    /// <summary>Raised (on a background thread) for each line that should go to the command log.</summary>
    public event Action<string>? Logged;

    /// <summary>Raised when the count of running (non-quiet) commands changes.</summary>
    public event Action<int>? ActiveChanged;

    public void Log(string line) => Logged?.Invoke(line);

    /// <summary>Cancels every running non-quiet command.</summary>
    public void CancelAll()
    {
        var old = _cancelAll;
        _cancelAll = new CancellationTokenSource();
        old.Cancel();
    }

    public Task<CliResult> RunAsync(IEnumerable<string> args, RunOptions? o = null)
    {
        var list = args.ToList();
        var psi = new ProcessStartInfo(WslExe);
        foreach (var a in list) psi.ArgumentList.Add(a);
        return CoreAsync(psi, "wsl " + string.Join(' ', list.Select(Format.Quote)), o ?? new RunOptions());
    }

    /// <summary>Runs wsl.exe with a raw, pre-formatted argument string (exactly as typed on a CLI).</summary>
    public Task<CliResult> RunRawAsync(string arguments, RunOptions? o = null) =>
        CoreAsync(new ProcessStartInfo(WslExe, arguments), "wsl " + arguments, o ?? new RunOptions());

    public Task<CliResult> RunExeAsync(string exe, IEnumerable<string> args, RunOptions? o = null)
    {
        var list = args.ToList();
        var psi = new ProcessStartInfo(exe);
        foreach (var a in list) psi.ArgumentList.Add(a);
        return CoreAsync(psi, Path.GetFileName(exe) + " " + string.Join(' ', list.Select(Format.Quote)), o ?? new RunOptions());
    }

    /// <summary>Starts wsl.exe with stdin left open for interactive use (console page).</summary>
    public Process StartInteractive(string arguments, Action<string> onLine)
    {
        var psi = new ProcessStartInfo(WslExe, arguments);
        Prepare(psi);
        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) onLine(Clean(e.Data)); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLine(Clean(e.Data)); };
        p.Start();
        p.StandardInput.AutoFlush = true;
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p;
    }

    private static void Prepare(ProcessStartInfo psi)
    {
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.StandardOutputEncoding = Utf8;
        psi.StandardErrorEncoding = Utf8;
        psi.StandardInputEncoding = Utf8;
        // Makes wsl.exe emit UTF-8 instead of UTF-16 for its own messages.
        psi.Environment["WSL_UTF8"] = "1";
    }

    private async Task<CliResult> CoreAsync(ProcessStartInfo psi, string display, RunOptions o)
    {
        Prepare(psi);
        using var linked = o.Quiet
            ? CancellationTokenSource.CreateLinkedTokenSource(o.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(o.Token, _cancelAll.Token);
        var token = linked.Token;

        if (!o.Quiet)
        {
            Log($"[{DateTime.Now:HH:mm:ss}] > {display}");
            ActiveChanged?.Invoke(Interlocked.Increment(ref _active));
        }

        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        var outDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var errDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => OnData(e.Data, sbOut, outDone);
            p.ErrorDataReceived += (_, e) => OnData(e.Data, sbErr, errDone);

            void OnData(string? data, StringBuilder sb, TaskCompletionSource done)
            {
                if (data is null) { done.TrySetResult(); return; }
                var line = Clean(data);
                lock (sb) sb.Append(line).Append('\n');
                if (!o.Quiet && line.Length > 0) Log("  " + line);
                o.OnLine?.Invoke(line);
            }

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            if (o.Stdin is not null) await p.StandardInput.WriteAsync(o.Stdin);
            p.StandardInput.Close();

            try
            {
                await p.WaitForExitAsync(token);
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
                if (!o.Quiet) Log("  (cancelled)");
                throw;
            }

            try { await Task.WhenAll(outDone.Task, errDone.Task).WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException) { }

            string so, se;
            lock (sbOut) so = sbOut.ToString();
            lock (sbErr) se = sbErr.ToString();
            var result = new CliResult(p.ExitCode, so, se);
            if (!o.Quiet && !result.Ok) Log($"  (exit code {result.ExitCode})");
            return result;
        }
        finally
        {
            if (!o.Quiet) ActiveChanged?.Invoke(Interlocked.Decrement(ref _active));
        }
    }

    [GeneratedRegex(@"\x1B\[[0-9;?]*[A-Za-z]")]
    private static partial Regex AnsiRegex();

    /// <summary>Strips NULs/ANSI codes and collapses carriage-return progress updates to their final state.</summary>
    public static string Clean(string s)
    {
        s = AnsiRegex().Replace(s.Replace("\0", ""), "").TrimEnd('\r');
        var i = s.LastIndexOf('\r');
        return i >= 0 ? s[(i + 1)..] : s;
    }
}
