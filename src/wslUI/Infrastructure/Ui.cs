using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace wslUI.Infrastructure;

/// <summary>Attached helpers for XAML.</summary>
public static class Ui
{
    /// <summary>Applies a uniform margin to every direct child of a Panel (poor man's "Spacing").</summary>
    public static readonly DependencyProperty ChildMarginProperty = DependencyProperty.RegisterAttached(
        "ChildMargin", typeof(Thickness), typeof(Ui), new PropertyMetadata(default(Thickness), OnChildMarginChanged));

    public static Thickness GetChildMargin(DependencyObject o) => (Thickness)o.GetValue(ChildMarginProperty);
    public static void SetChildMargin(DependencyObject o, Thickness v) => o.SetValue(ChildMarginProperty, v);

    private static void OnChildMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel) return;
        void Apply()
        {
            foreach (var child in panel.Children.OfType<FrameworkElement>())
                child.Margin = (Thickness)e.NewValue;
        }
        if (panel.IsLoaded) Apply();
        else panel.Loaded += (_, _) => Apply();
    }
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is not true;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is not true;
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var b = value switch
        {
            bool x => x,
            string s => s.Length > 0,
            int i => i > 0,
            null => false,
            _ => true,
        };
        return b ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
