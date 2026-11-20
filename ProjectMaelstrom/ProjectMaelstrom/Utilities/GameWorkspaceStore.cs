using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

// Saved preferences deliberately cannot restore a live HWND or process identity.
internal sealed record GameWorkspaceProfile(string Name, string ProcessName, int Version = 1);

internal static class GameWorkspaceStore
{
    public static GameWorkspaceProfile? Load(string dataRoot)
    {
        var path = Path.Combine(dataRoot, "workspaces", "last-workspace.json");
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 8192) throw new InvalidDataException("Saved workspace is too large.");
        var profile = JsonSerializer.Deserialize<GameWorkspaceProfile>(File.ReadAllText(path));
        if (profile == null || profile.Version != 1 || string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 120 || string.IsNullOrWhiteSpace(profile.ProcessName) || profile.ProcessName.Length > 260)
            throw new InvalidDataException("Saved workspace is not supported.");
        return profile;
    }

    public static void Save(string dataRoot, GameWorkspaceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 120 || string.IsNullOrWhiteSpace(profile.ProcessName) || profile.ProcessName.Length > 260 || profile.Version != 1)
            throw new InvalidDataException("Enter a workspace name of 1–120 characters.");
        var directory = Path.Combine(dataRoot, "workspaces");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, Path.Combine(directory, "last-workspace.json"), true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
