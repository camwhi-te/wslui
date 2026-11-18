using System.IO;
using Microsoft.Win32;

namespace WslManager.Services;

public sealed record LxssDistro(string Guid, string Name, string BasePath, string VhdPath, int? DefaultUid, int? Flags);

/// <summary>Reads WSL's per-user registration data (HKCU\...\Lxss) for paths and metadata wsl.exe doesn't print.</summary>
public static class LxssRegistry
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Lxss";

    public static Dictionary<string, LxssDistro> Read()
    {
        var result = new Dictionary<string, LxssDistro>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (root is null) return result;
            foreach (var sub in root.GetSubKeyNames())
            {
                using var k = root.OpenSubKey(sub);
                if (k?.GetValue("DistributionName") is not string name) continue;
                var basePath = k.GetValue("BasePath") as string ?? "";
                if (basePath.StartsWith(@"\\?\")) basePath = basePath[4..];
                var vhdName = k.GetValue("VhdFileName") as string ?? "ext4.vhdx";
                result[name] = new LxssDistro(
                    sub, name, basePath,
                    basePath.Length > 0 ? Path.Combine(basePath, vhdName) : "",
                    k.GetValue("DefaultUid") as int?,
                    k.GetValue("Flags") as int?);
            }
        }
        catch { /* registry unavailable: details just stay blank */ }
        return result;
    }

    public static int? DefaultVersion()
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(KeyPath);
            return root?.GetValue("DefaultVersion") as int?;
        }
        catch { return null; }
    }
}
