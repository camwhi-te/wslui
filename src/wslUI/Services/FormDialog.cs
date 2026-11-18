using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
}

/// <summary>Small code-built modal form used for the many "ask a couple of values, then run wsl" actions.</summary>
public static class FormDialog
{
    public static Dictionary<string, string>? Show(string title, string? message, params FormField[] fields)
    {
        var win = new Window
        {
            Title = title,
            Width = 560,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };
        if (Application.Current?.MainWindow is { IsVisible: true } owner) win.Owner = owner;
        else win.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new StackPanel { Margin = new Thickness(20) };
        if (!string.IsNullOrWhiteSpace(message))
            root.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });

        var getters = new Dictionary<string, Func<string>>();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        UIElement? first = null;

        foreach (var f in fields)
        {
            var row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (f.Kind != FieldKind.Check)
            {
                var label = new TextBlock { Text = f.Label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 10) };
                Grid.SetRow(label, row);
                grid.Children.Add(label);
            }

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
                    var cb = new ComboBox { ItemsSource = f.Options, IsEditable = f.Kind == FieldKind.EditableCombo };
                    if (cb.IsEditable) cb.Text = f.Value;
                    else cb.SelectedItem = f.Options.Contains(f.Value) ? f.Value : f.Options.FirstOrDefault();
                    getters[f.Key] = () => cb.IsEditable ? cb.Text : cb.SelectedItem as string ?? "";
                    editor = cb;
                    break;
                }
                case FieldKind.Check:
                {
                    var chk = new CheckBox { Content = f.Label, IsChecked = f.Value == "true" };
                    getters[f.Key] = () => chk.IsChecked == true ? "true" : "false";
                    editor = chk;
                    break;
                }
                default:
                {
                    var tb = new TextBox { Text = f.Value };
                    if (f.Hint is not null) tb.ToolTip = f.Hint;
                    getters[f.Key] = () => tb.Text;
                    editor = tb;
                    if (f.Kind is FieldKind.OpenFile or FieldKind.SaveFile or FieldKind.Folder)
                    {
                        var browse = new Button { Content = "Browse…", Margin = new Thickness(8, 0, 0, 10), VerticalAlignment = VerticalAlignment.Top };
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
                        Grid.SetRow(browse, row);
                        Grid.SetColumn(browse, 2);
                        grid.Children.Add(browse);
                    }
                    break;
                }
            }
            editor.Margin = new Thickness(0, 0, 0, 10);
            Grid.SetRow(editor, row);
            Grid.SetColumn(editor, f.Kind == FieldKind.Check ? 0 : 1);
            if (f.Kind == FieldKind.Check) Grid.SetColumnSpan(editor, 3);
            grid.Children.Add(editor);
            first ??= editor;

            if (f.Hint is not null && f.Kind != FieldKind.Check)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var hint = new TextBlock { Text = f.Hint, Opacity = 0.7, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, -6, 0, 10) };
                Grid.SetRow(hint, row + 1);
                Grid.SetColumn(hint, 1);
                grid.Children.Add(hint);
            }
        }
        root.Children.Add(grid);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = "OK", MinWidth = 90, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.SetResourceReference(FrameworkElement.StyleProperty, "AccentButtonStyle");
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true };
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
}
