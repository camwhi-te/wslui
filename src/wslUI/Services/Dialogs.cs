using System.Windows;
using Microsoft.Win32;

namespace wslUI.Services;

public static class Dialogs
{
    private static Window? Owner => Application.Current?.MainWindow is { IsVisible: true } w ? w : null;

    public static void Error(string message, string title = "wslUI")
    {
        if (Owner is { } o) MessageBox.Show(o, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        else MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static void Info(string message, string title = "wslUI")
    {
        if (Owner is { } o) MessageBox.Show(o, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        else MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public static bool Confirm(string message, string title = "Confirm", bool warning = false)
    {
        var icon = warning ? MessageBoxImage.Warning : MessageBoxImage.Question;
        var r = Owner is { } o
            ? MessageBox.Show(o, message, title, MessageBoxButton.YesNo, icon, MessageBoxResult.No)
            : MessageBox.Show(message, title, MessageBoxButton.YesNo, icon, MessageBoxResult.No);
        return r == MessageBoxResult.Yes;
    }

    /// <summary>Asks the user to type <paramref name="expected"/> to confirm a destructive action.</summary>
    public static bool ConfirmByTyping(string title, string message, string expected)
    {
        var r = FormDialog.Show(title, message + $"\n\nType \"{expected}\" to confirm.",
            new FormField("confirm", "Confirm") { Hint = expected });
        return r is not null && r["confirm"].Trim() == expected;
    }

    public static string? OpenFile(string filter, string? title = null)
    {
        var d = new OpenFileDialog { Filter = filter, Title = title ?? "Open" };
        return d.ShowDialog(Owner) == true ? d.FileName : null;
    }

    public static string? SaveFile(string filter, string? fileName = null, string? title = null)
    {
        var d = new SaveFileDialog { Filter = filter, FileName = fileName ?? "", Title = title ?? "Save" };
        return d.ShowDialog(Owner) == true ? d.FileName : null;
    }

    public static string? PickFolder(string? title = null)
    {
        var d = new OpenFolderDialog { Title = title ?? "Select folder" };
        return d.ShowDialog(Owner) == true ? d.FolderName : null;
    }

    /// <summary>Shows the full output of a command that failed (or anything else worth showing).</summary>
    public static void ShowResult(CliResult r, string action)
    {
        if (r.Ok) return;
        var text = string.IsNullOrWhiteSpace(r.Text) ? $"wsl.exe exited with code {r.ExitCode}." : r.Text;
        Error(text, $"{action} failed");
    }
}
