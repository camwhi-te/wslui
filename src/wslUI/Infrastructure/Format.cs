namespace wslUI.Infrastructure;

public static class Format
{
    public static string Bytes(long? bytes)
    {
        if (bytes is null) return "";
        double v = bytes.Value;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{v:0} {units[i]}" : $"{v:0.0} {units[i]}";
    }

    public static string Duration(TimeSpan? t)
    {
        if (t is null) return "";
        var v = t.Value;
        if (v.TotalDays >= 1) return $"{(int)v.TotalDays}d {v.Hours}h";
        if (v.TotalHours >= 1) return $"{(int)v.TotalHours}h {v.Minutes}m";
        return $"{v.Minutes}m {v.Seconds}s";
    }

    /// <summary>Quote an argument for display / Windows command lines.</summary>
    public static string Quote(string a) =>
        a.Length > 0 && a.IndexOfAny([' ', '\t', '"']) < 0 ? a : "\"" + a.Replace("\"", "\\\"") + "\"";
}
