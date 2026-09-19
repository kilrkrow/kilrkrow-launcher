using System.Text.Json;
using System.Text.Json.Serialization;

namespace KilrkrowLauncher.Settings;

public sealed class LauncherSettings
{
    public string Owner { get; set; } = "kilrkrow";

    /// <summary>
    /// Optional GitHub token for a higher rate limit. Public catalog works without it.
    /// Stored only in the local settings file under LocalAppData.
    /// </summary>
    public string? GitHubToken { get; set; }
}

public sealed class LauncherSettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _path;

    public LauncherSettingsStore(string path)
    {
        _path = path;
    }

    public static string DefaultPath(string localAppData)
        => Path.Combine(localAppData, "KilrkrowLauncher", "settings.json");

    public LauncherSettings Load()
    {
        if (!File.Exists(_path))
            return new LauncherSettings();

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<LauncherSettings>(json, Json) ?? new LauncherSettings();
        }
        catch
        {
            return new LauncherSettings();
        }
    }

    public void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, Json));
    }
}
