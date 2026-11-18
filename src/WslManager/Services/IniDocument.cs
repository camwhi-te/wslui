namespace WslManager.Services;

/// <summary>
/// Minimal INI document that preserves comments, ordering and unknown keys when values are changed.
/// Used for both %UserProfile%\.wslconfig and /etc/wsl.conf.
/// </summary>
public sealed class IniDocument
{
    private readonly List<string> _lines;

    private IniDocument(List<string> lines) => _lines = lines;

    public static IniDocument Parse(string? text)
    {
        var lines = (text ?? "").Replace("\r\n", "\n").Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return new IniDocument(lines);
    }

    private static bool IsHeader(string line, out string name)
    {
        var t = line.Trim();
        if (t.StartsWith('[') && t.Contains(']'))
        {
            name = t[1..t.IndexOf(']')].Trim();
            return true;
        }
        name = "";
        return false;
    }

    private static bool TryKeyValue(string line, out string key, out string value)
    {
        key = value = "";
        var t = line.Trim();
        if (t.Length == 0 || t[0] is '#' or ';' or '[') return false;
        var eq = t.IndexOf('=');
        if (eq <= 0) return false;
        key = t[..eq].Trim();
        value = t[(eq + 1)..].Trim();
        return true;
    }

    /// <summary>Returns (start, end) line indices of every block for a section; start is the header line.</summary>
    private List<(int Start, int End)> Sections(string section)
    {
        var result = new List<(int, int)>();
        for (var i = 0; i < _lines.Count; i++)
        {
            if (!IsHeader(_lines[i], out var name) || !name.Equals(section, StringComparison.OrdinalIgnoreCase)) continue;
            var end = i + 1;
            while (end < _lines.Count && !IsHeader(_lines[end], out _)) end++;
            result.Add((i, end));
        }
        return result;
    }

    public string? Get(string section, string key)
    {
        string? found = null;
        foreach (var (s, e) in Sections(section))
            for (var i = s + 1; i < e; i++)
                if (TryKeyValue(_lines[i], out var k, out var v) && k.Equals(key, StringComparison.OrdinalIgnoreCase))
                    found = v;
        return found;
    }

    /// <summary>Sets a value, or removes the key when <paramref name="value"/> is null/empty.</summary>
    public void Set(string section, string key, string? value)
    {
        value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (Get(section, key) == value) return;

        var sections = Sections(section);
        // Remove every existing occurrence except the last one, which we update in place.
        int? target = null;
        foreach (var (s, e) in sections)
            for (var i = s + 1; i < e; i++)
                if (TryKeyValue(_lines[i], out var k, out _) && k.Equals(key, StringComparison.OrdinalIgnoreCase))
                    target = i;

        if (target is int t)
        {
            if (value is null) _lines.RemoveAt(t);
            else
            {
                TryKeyValue(_lines[t], out var origKey, out _);
                _lines[t] = $"{origKey}={value}";
            }
            return;
        }
        if (value is null) return;

        if (sections.Count == 0)
        {
            if (_lines.Count > 0 && _lines[^1].Trim().Length > 0) _lines.Add("");
            _lines.Add($"[{section}]");
            _lines.Add($"{key}={value}");
            return;
        }

        // Insert after the last non-blank line of the last block for that section.
        var (start, end) = sections[^1];
        var insertAt = end;
        while (insertAt - 1 > start && _lines[insertAt - 1].Trim().Length == 0) insertAt--;
        _lines.Insert(insertAt, $"{key}={value}");
    }

    public string ToString(string newline) => _lines.Count == 0 ? "" : string.Join(newline, _lines) + newline;

    public override string ToString() => ToString("\n");
}
