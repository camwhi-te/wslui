using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace wslUI.Infrastructure;

/// <summary>Brand colour + initials for distro tiles, so machines are recognisable at a glance.</summary>
public static class DistroBrand
{
    private static readonly (string Key, string Hex)[] Known =
    [
        ("ubuntu", "#E95420"), ("debian", "#D70A53"), ("docker", "#2496ED"), ("kali", "#367BF0"),
        ("arch", "#1793D1"), ("fedora", "#51A2DA"), ("opensuse", "#73BA25"), ("suse", "#30BA78"),
        ("alma", "#0F4266"), ("rocky", "#10B981"), ("rlc", "#10B981"), ("oracle", "#C74634"),
        ("alpine", "#0D597F"), ("azure", "#0078D4"), ("elxr", "#7C3AED"), ("nixos", "#5277C3"),
    ];

    private static readonly string[] Fallback = ["#6366F1", "#0EA5E9", "#14B8A6", "#F59E0B", "#EC4899", "#8B5CF6", "#84CC16"];

    public static Color ColorFor(string? name)
    {
        var n = (name ?? "").ToLowerInvariant();
        foreach (var (key, hex) in Known)
            if (n.Contains(key)) return (Color)ColorConverter.ConvertFromString(hex);
        var hash = n.Aggregate(17, (a, c) => a * 31 + c);
        return (Color)ColorConverter.ConvertFromString(Fallback[Math.Abs(hash % Fallback.Length)]);
    }

    public static string InitialsFor(string? name)
    {
        var parts = (name ?? "?").Split(['-', '_', ' ', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        var letters = parts[0][..1].ToUpperInvariant();
        if (parts.Length > 1 && char.IsLetter(parts[1][0])) letters += char.ToUpperInvariant(parts[1][0]);
        else if (parts[0].Length > 1) letters += char.ToLowerInvariant(parts[0][1]);
        return letters;
    }
}

public sealed class BrandBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var b = new SolidColorBrush(DistroBrand.ColorFor(value as string));
        b.Freeze();
        return b;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class InitialsConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => DistroBrand.InitialsFor(value as string);
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Maps a config section name to a friendly title or icon (ConverterParameter = "glyph").</summary>
public sealed class SectionInfoConverter : IValueConverter
{
    private static readonly Dictionary<string, (string Title, string Glyph, string Hint)> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["wsl2"] = ("WSL 2 virtual machine", "\uE950", "Resources, networking and features of the shared VM"),
        ["experimental"] = ("Experimental", "\uEA86", "Preview features that may change between WSL releases"),
        ["general"] = ("General", "\uE713", "Behaviour that applies to every distribution"),
        ["boot"] = ("Boot", "\uE7E8", "What runs when the distribution starts"),
        ["user"] = ("User", "\uE77B", "Login defaults"),
        ["automount"] = ("Windows drives", "\uEDA2", "How C:\\, D:\\ … appear inside Linux"),
        ["network"] = ("Network", "\uE968", "Hostname, hosts file and DNS"),
        ["interop"] = ("Windows interop", "\uE8AB", "Running Windows programs from Linux"),
        ["gpu"] = ("GPU", "\uE7F4", "Graphics acceleration"),
        ["time"] = ("Time", "\uE823", "Clock and timezone"),
    };

    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var key = value as string ?? "";
        var info = Map.TryGetValue(key, out var v) ? v : (key, "\uE713", "");
        return (p as string) switch
        {
            "glyph" => info.Item2,
            "hint" => info.Item3,
            _ => info.Item1,
        };
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>True when value equals ConverterParameter (string compare) → Visible.</summary>
public sealed class EqualsToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type t, object p, CultureInfo c) =>
        (string.Equals(value?.ToString(), p?.ToString(), StringComparison.OrdinalIgnoreCase) ^ Invert) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
