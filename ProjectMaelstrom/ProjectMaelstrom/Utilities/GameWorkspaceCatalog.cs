using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Game-specific routing hints only. A title match is not authentication
/// or permission to capture or control a window.</summary>
internal static class GameWorkspaceCatalog
{
    internal const string DodgeGameId = "godot-dodge-the-creeps";

    internal static bool IsDodgeWindow(GameWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var title = window.Title.Trim();
        var knownTitle = title.Equals("Dodge the Creeps", StringComparison.OrdinalIgnoreCase) ||
            title.Equals("Dodge the Creeps (DEBUG)", StringComparison.OrdinalIgnoreCase);
        var knownProcess = window.ProcessName.StartsWith("Godot", StringComparison.OrdinalIgnoreCase) ||
            window.ProcessName.Equals("dodge_the_creeps", StringComparison.OrdinalIgnoreCase);
        return knownTitle && knownProcess;
    }

    /// <summary>Checks routing metadata, not gameplay compatibility. The Workshop
    /// and each test adapter retain their own full validation.</summary>
    internal static string RequireDodgePack(string path)
    {
        var fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 1024 * 1024) throw new InvalidDataException("Choose a Dodge game pack smaller than 1 MiB.");
        using var json = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 32 });
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.EnumerateObject().Count(property => property.NameEquals("game_id")) != 1 ||
            !root.TryGetProperty("game_id", out var gameId) || gameId.ValueKind != JsonValueKind.String ||
            gameId.GetString() != DodgeGameId)
            throw new InvalidDataException("This workspace opens Dodge the Creeps packs. Choose a pack with the Dodge game identifier.");
        return fullPath;
    }
}
