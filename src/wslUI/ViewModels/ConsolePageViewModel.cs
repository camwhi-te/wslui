using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using wslUI.Infrastructure;
using wslUI.Models;
using wslUI.Services;

namespace wslUI.ViewModels;

/// <summary>Runs commands inside a distro (or raw wsl.exe arguments) and streams output; stdin stays open.</summary>
public sealed class ConsolePageViewModel : PageViewModel
{
    private readonly TextBuffer _output;
    private Process? _process;

    public ConsolePageViewModel(MainViewModel main) : base(main, "Console", "")
    {
        _output = new TextBuffer(t => Output = t);
        RunCommand = new AsyncCommand(RunAsync, () => !IsRunning && !string.IsNullOrWhiteSpace(Command));
        StopCommand = new RelayCommand(Stop, () => IsRunning);
        SendCommand = new RelayCommand(Send, () => IsRunning);
        ClearCommand = new RelayCommand(() => _output.Clear());
        TerminalCommand = new RelayCommand(OpenTerminal);
    }

    public ICommand RunCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SendCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand TerminalCommand { get; }

    public ObservableCollection<string> History { get; } = [];

    private Distro? _selectedDistro;
    /// <summary>null = default distribution.</summary>
    public Distro? SelectedDistro { get => _selectedDistro; set => Set(ref _selectedDistro, value); }

    private string _user = "";
    public string User { get => _user; set => Set(ref _user, value); }

    private bool _rawMode;
    /// <summary>When true, <see cref="Command"/> is passed to wsl.exe verbatim (any CLI option).</summary>
    public bool RawMode { get => _rawMode; set { if (Set(ref _rawMode, value)) OnPropertyChanged(nameof(Prompt)); } }

    public string Prompt => RawMode ? "wsl.exe" : "$";

    private string _command = "";
    public string Command { get => _command; set => Set(ref _command, value); }

    private string _input = "";
    public string Input { get => _input; set => Set(ref _input, value); }

    private string _outputText = "";
    public string Output { get => _outputText; private set => Set(ref _outputText, value); }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set { if (Set(ref _isRunning, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public void ClearDistro() => SelectedDistro = null;

    private string BuildArgs()
    {
        if (RawMode) return Command.Trim();
        var parts = new List<string>();
        if (SelectedDistro is not null) parts.Add("-d " + Format.Quote(SelectedDistro.Name));
        if (!string.IsNullOrWhiteSpace(User)) parts.Add("-u " + Format.Quote(User.Trim()));
        parts.Add("-- " + Command.Trim());
        return string.Join(' ', parts);
    }

    private async Task RunAsync()
    {
        var cmd = Command.Trim();
        History.Remove(cmd);
        History.Insert(0, cmd);
        while (History.Count > 50) History.RemoveAt(History.Count - 1);

        var args = BuildArgs();
        _output.AppendLine($"> wsl {args}");
        Main.Log($"[{DateTime.Now:HH:mm:ss}] > wsl {args}   (console)");
        IsRunning = true;
        try
        {
            _process = Wsl.Cli.StartInteractive(args, _output.AppendLine);
            await _process.WaitForExitAsync();
            _output.AppendLine($"[exit code {_process.ExitCode}]");
            _output.AppendLine("");
        }
        catch (Exception ex)
        {
            _output.AppendLine("[error] " + ex.Message);
        }
        finally
        {
            _process?.Dispose();
            _process = null;
            IsRunning = false;
            Command = "";
        }
    }

    private void Stop()
    {
        try { _process?.Kill(entireProcessTree: true); } catch { }
    }

    private void Send()
    {
        if (_process is null) return;
        try
        {
            _process.StandardInput.Write(Input + "\n");
            _output.AppendLine(Input);
        }
        catch (Exception ex) { _output.AppendLine("[stdin error] " + ex.Message); }
        Input = "";
    }

    private void OpenTerminal()
    {
        var a = new List<string>();
        if (SelectedDistro is not null) a.AddRange(["-d", SelectedDistro.Name]);
        if (!string.IsNullOrWhiteSpace(User)) a.AddRange(["-u", User.Trim()]);
        a.AddRange(["--cd", "~"]);
        Wsl.OpenTerminal([.. a]);
    }
}
