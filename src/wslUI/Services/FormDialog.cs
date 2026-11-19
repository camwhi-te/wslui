using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace wslUI.Services;

public enum FieldKind { Text, Password, Combo, EditableCombo, Check, OpenFile, SaveFile, Folder }

public sealed record FormField(string Key, string Label)
{
    public FieldKind Kind { get; init; } = FieldKind.Text;
    public string Value { get; init; } = "";
    public string[] Options { get; init; } = [];
    public string Filter { get; init; } = "All files|*.*";
    public string? Hint { get; init; }
    public bool Required { get; init; }

    /// <summary>Placed in a collapsed "Advanced options" section (progressive disclosure).</summary>
    public bool Advanced { get; init; }
}

/// <summary>Small code-built modal form used for the "ask a few values, then run wsl" actions.</summary>
public static class FormDialog
{
    public static Dictionary<string, string>? Show(string title, string? message, params FormField[] fields) =>
        ShowWithIcon("", title, message, null, fields);

    public static Dictionary<string, string>? ShowWithIcon(string glyph, string title, string? message, string? okText, params FormField[] fields)
    {
        var res = Application.Current.Resources;
        var win = new Window
        {
            Title = title,
            Width = 580,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Background = (Brush)res["CardBrush"],
        };
        if (Application.Current?.MainWindow is { IsVisible: true } owner) win.Owner = owner;
        else win.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        // Header: icon badge + title + description
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        var badge = new Border
        {
            Width = 40, Height = 40, CornerRadius = new CornerRadius(10),
            Background = (Brush)res["AccentSoftBrush"], VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = glyph, FontFamily = (FontFamily)res["IconFont"], FontSize = 18,
                Foreground = (Brush)res["AccentBrush"],
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        };
        DockPanel.SetDock(badge, Dock.Left);
        header.Children.Add(badge);
        var titles = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
        titles.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, FontFamily = (FontFamily)res["DisplayFont"] });
        if (!string.IsNullOrWhiteSpace(message))
            titles.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Foreground = (Brush)res["TextMutedBrush"] });
        header.Children.Add(titles);
        root.Children.Add(header);

        var getters = new Dictionary<string, Func<string>>();
        UIElement? first = null;
        var main = BuildFields(fields.Where(f => !f.Advanced), getters, res, ref first);
        root.Children.Add(main);

        var advanced = fields.Where(f => f.Advanced).ToList();
        if (advanced.Count > 0)
        {
            UIElement? ignored = null;
            var adv = BuildFields(advanced, getters, res, ref ignored);
            adv.Margin = new Thickness(0, 12, 0, 0);
            root.Children.Add(new Expander
            {
                Header = $"Advanced options ({advanced.Count})",
                Content = adv,
                Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            });
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var ok = new Button { Content = okText ?? "OK", MinWidth = 100, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.SetResourceReference(FrameworkElement.StyleProperty, "AccentButtonStyle");
        var cancel = new Button { Content = "Cancel", MinWidth = 100, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        Dictionary<string, string>? result = null;
        ok.Click += (_, _) =>
        {
            var values = getters.ToDictionary(kv => kv.Key, kv => kv.Value());
            var missing = fields.FirstOrDefault(f => f.Required && string.IsNullOrWhiteSpace(values[f.Key]));
            if (missing is not null)
            {
                Dialogs.Error($"\"{missing.Label}\" is required.");
                return;
            }
            result = values;
            win.DialogResult = true;
        };

        win.Content = root;
        win.Loaded += (_, _) => (first as IInputElement)?.Focus();
        win.ShowDialog();
        return result;
    }

    /// <summary>Stacked label-above-field layout (easier to scan than side-by-side labels).</summary>
    private static StackPanel BuildFields(IEnumerable<FormField> fields, Dictionary<string, Func<string>> getters, ResourceDictionary res, ref UIElement? first)
    {
        var panel = new StackPanel();
        foreach (var f in fields)
        {
            var block = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            if (f.Kind != FieldKind.Check)
                block.Children.Add(new TextBlock { Text = f.Label + (f.Required ? "" : "  (optional)"), Margin = new Thickness(0, 0, 0, 6), Foreground = (Brush)res["TextMutedBrush"] });

            FrameworkElement editor;
            switch (f.Kind)
            {
                case FieldKind.Password:
                {
                    var pb = new PasswordBox { Password = f.Value };
                    getters[f.Key] = () => pb.Password;
                    editor = pb;
                    break;
                }
                case FieldKind.Combo:
                case FieldKind.EditableCombo:
                {
                    var cb = new ComboBox { ItemsSource = f.Options, IsEditable = f.Kind == FieldKind.EditableCombo, HorizontalAlignment = HorizontalAlignment.Stretch };
                    if (cb.IsEditable) cb.Text = f.Value;
                    else cb.SelectedItem = f.Options.Contains(f.Value) ? f.Value : f.Options.FirstOrDefault();
                    getters[f.Key] = () => cb.IsEditable ? cb.Text : cb.SelectedItem as string ?? "";
                    editor = cb;
                    break;
                }
                case FieldKind.Check:
                {
                    var chk = new CheckBox { Content = f.Label, IsChecked = f.Value == "true" };
                    chk.SetResourceReference(FrameworkElement.StyleProperty, "ToggleSwitch");
                    getters[f.Key] = () => chk.IsChecked == true ? "true" : "false";
                    editor = chk;
                    break;
                }
                default:
                {
                    var tb = new TextBox { Text = f.Value };
                    getters[f.Key] = () => tb.Text;
                    editor = tb;
                    if (f.Kind is FieldKind.OpenFile or FieldKind.SaveFile or FieldKind.Folder)
                    {
                        var row = new DockPanel();
                        var browse = new Button { Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 6, 12, 6) };
                        browse.Content = new TextBlock { Text = f.Kind == FieldKind.Folder ? "" : "", FontFamily = (FontFamily)res["IconFont"], FontSize = 15 };
                        browse.ToolTip = "Browse…";
                        System.Windows.Automation.AutomationProperties.SetName(browse, "Browse");
                        browse.Click += (_, _) =>
                        {
                            var picked = f.Kind switch
                            {
                                FieldKind.OpenFile => Dialogs.OpenFile(f.Filter),
                                FieldKind.SaveFile => Dialogs.SaveFile(f.Filter, tb.Text),
                                _ => Dialogs.PickFolder(),
                            };
                            if (picked is not null) tb.Text = picked;
                        };
                        DockPanel.SetDock(browse, Dock.Right);
                        row.Children.Add(browse);
                        row.Children.Add(tb);
                        block.Children.Add(row);
                        first ??= tb;
                        AddHint(block, f, res);
                        panel.Children.Add(block);
                        continue;
                    }
                    break;
                }
            }
            block.Children.Add(editor);
            first ??= editor;
            AddHint(block, f, res);
            panel.Children.Add(block);
        }
        return panel;
    }

    private static void AddHint(StackPanel block, FormField f, ResourceDictionary res)
    {
        if (f.Hint is null) return;
        block.Children.Add(new TextBlock
        {
            Text = f.Hint, FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0), Foreground = (Brush)res["TextSubtleBrush"],
        });
    }
}
