using System.Windows;
using wslUI.Services;
using wslUI.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace wslUI.Views;

/// <summary>Notification-area icon with quick start/stop/terminal actions per distro.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly MainViewModel _vm;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu = new();

    public TrayIcon(MainViewModel vm)
    {
        _vm = vm;
        _menu.Items.Add("Loading…");
        _menu.Opening += (_, _) => Rebuild();
        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "wslUI",
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) App.Current.ShowMainWindow(); };
    }

    private static Drawing.Icon LoadIcon()
    {
        var res = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        using var s = res!.Stream;
        return new Drawing.Icon(s, Forms.SystemInformation.SmallIconSize);
    }

    public void ShowBalloon(string title, string text) => _icon.ShowBalloonTip(4000, title, text, Forms.ToolTipIcon.Info);

    private void Rebuild()
    {
        _menu.Items.Clear();
        var open = new Forms.ToolStripMenuItem("Open wslUI", null, (_, _) => App.Current.ShowMainWindow())
        {
            Font = new Drawing.Font(_menu.Font, Drawing.FontStyle.Bold),
        };
        _menu.Items.Add(open);
        _menu.Items.Add(new Forms.ToolStripSeparator());

        if (_vm.Distros.Count == 0)
            _menu.Items.Add(new Forms.ToolStripMenuItem("No distributions installed") { Enabled = false });

        foreach (var d in _vm.Distros.ToList())
        {
            var name = d.Name;
            var label = $"{name}{(d.IsDefault ? " (default)" : "")} — {d.State}";
            var item = new Forms.ToolStripMenuItem(label) { Checked = d.IsRunning };
            item.DropDownItems.Add("Open terminal", null, (_, _) => _vm.Wsl.OpenTerminal("-d", name, "--cd", "~"));
            item.DropDownItems.Add("Open files", null, (_, _) => WslService.OpenExplorer($@"\\wsl.localhost\{name}"));
            item.DropDownItems.Add(new Forms.ToolStripSeparator());
            if (d.IsRunning)
            {
                item.DropDownItems.Add("Stop", null, async (_, _) =>
                {
                    var r = await _vm.Wsl.TerminateAsync(name);
                    Dialogs.ShowResult(r, "Terminate");
                    await _vm.RefreshDistrosAsync();
                });
            }
            else
            {
                item.DropDownItems.Add("Start", null, async (_, _) =>
                {
                    _vm.Wsl.Start(name);
                    await _vm.WaitForStateAsync(name, true);
                });
            }
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Shut down WSL", null, async (_, _) =>
        {
            var r = await _vm.Wsl.ShutdownAsync(false);
            Dialogs.ShowResult(r, "Shutdown");
            await _vm.RefreshDistrosAsync();
        });
        _menu.Items.Add("Exit wslUI", null, (_, _) => App.Current.ExitApp());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
