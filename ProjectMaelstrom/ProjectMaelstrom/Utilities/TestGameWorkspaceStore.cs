using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Stores the user's test-game workspace outside the versioned app directory.</summary>
internal static class TestGameWorkspaceStore
{
    private const string Filename = "test-games-workspace.json";
    public static string? Load(string dataRoot)
    {
        var path = Path.Combine(dataRoot, Filename);
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 16384) throw new InvalidDataException("Test-game workspace setting is too large.");
        using var json = JsonDocument.Parse(File.ReadAllBytes(path));
        if (json.RootElement.GetProperty("version").GetInt32() != 1) throw new InvalidDataException("Unsupported test-game workspace setting.");
        var value = json.RootElement.GetProperty("path").GetString();
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value)) return null;
        return File.Exists(LocalTestGames.DodgePath(value)) && File.Exists(LocalTestGames.GodotPath(value)) ? value : null;
    }

    public static void Save(string dataRoot, string workspace)
    {
        workspace = Path.GetFullPath(workspace);
        if (!File.Exists(LocalTestGames.DodgePath(workspace)) || !File.Exists(LocalTestGames.GodotPath(workspace)))
            throw new InvalidOperationException("Choose the AutoWizard101 folder containing the local Dodge game and Godot runtime.");
        Directory.CreateDirectory(dataRoot);
        var path = Path.Combine(dataRoot, Filename);
        var temporary = Path.Combine(dataRoot, ".test-games-workspace-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { version = 1, path = workspace }, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
