using System.IO;
using System.Text.Json;

namespace wslUI.Services;

public sealed class AppSettings
{
    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "wslUI");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public bool CloseToTray { get; set; } = true;
    public bool LiveStats { get; set; } = true;
    public int StatsIntervalSeconds { get; set; } = 5;
    public bool StartMinimized { get; set; }
    public bool TrayHintShown { get; set; }
    public double LogHeight { get; set; } = 170;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* corrupt file: fall back to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
