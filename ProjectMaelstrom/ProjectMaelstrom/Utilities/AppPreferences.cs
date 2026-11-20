using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record AppPreferences(bool ControlSuggestions = true, bool TipsAndHiddenItems = true, int Version = 1,
    bool ActionCountdownSound = true, bool W101KeyboardOnlyTakeover = false, bool W101RunningCaption = false)
{
    public static AppPreferences Load(string dataRoot)
    {
        var path = Path.Combine(dataRoot, "settings.json");
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 16384) throw new InvalidDataException("Settings file is too large.");
        var settings = JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid settings file.");
        if (settings.Version != 1) throw new InvalidDataException("This settings version is not supported.");
        return settings;
    }

    public void Save(string dataRoot)
    {
        if (Version != 1) throw new InvalidDataException("This settings version is not supported.");
        Directory.CreateDirectory(dataRoot);
        var path = Path.Combine(dataRoot, "settings.json");
        var temporary = Path.Combine(dataRoot, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
