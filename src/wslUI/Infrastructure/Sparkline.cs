using System.Windows;
using System.Windows.Media;

namespace wslUI.Infrastructure;

/// <summary>Lightweight streaming area chart: newest sample on the right, fixed capacity.</summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CapacityProperty = DependencyProperty.Register(
        nameof(Capacity), typeof(int), typeof(Sparkline),
        new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.LimeGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values { get => (IReadOnlyList<double>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public int Capacity { get => (int)GetValue(CapacityProperty); set => SetValue(CapacityProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var values = Values;
        double w = ActualWidth, h = ActualHeight;
        if (values is null || values.Count < 2 || w <= 0 || h <= 0) return;

        // Maximum <= 0 means auto-scale to the visible data (with a small floor so idle noise stays flat).
        var max = Maximum > 0 ? Maximum : Math.Max(5, values.Max() * 1.25);
        var cap = Math.Max(Capacity, values.Count);
        var step = w / (cap - 1);
        var start = w - step * (values.Count - 1);
        double Y(double v) => h - 1 - Math.Clamp(v / max, 0, 1) * (h - 2);

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(new Point(start, Y(values[0])), false, false);
            for (var i = 1; i < values.Count; i++) ctx.LineTo(new Point(start + i * step, Y(values[i])), true, true);
        }
        line.Freeze();

        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(start, h), true, true);
            for (var i = 0; i < values.Count; i++) ctx.LineTo(new Point(start + i * step, Y(values[i])), true, true);
            ctx.LineTo(new Point(w, h), true, true);
        }
        area.Freeze();

        var color = Stroke is SolidColorBrush s ? s.Color : Colors.LimeGreen;
        var fill = new LinearGradientBrush(
            Color.FromArgb(90, color.R, color.G, color.B),
            Color.FromArgb(0, color.R, color.G, color.B), 90);
        fill.Freeze();
        var pen = new Pen(Stroke, 1.6) { LineJoin = PenLineJoin.Round };
        pen.Freeze();

        dc.DrawGeometry(fill, null, area);
        dc.DrawGeometry(null, pen, line);
    }
}
