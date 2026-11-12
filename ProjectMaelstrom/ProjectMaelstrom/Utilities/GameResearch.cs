using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record GameResearch(string Id, string Game, string SourceUrl, string Hypothesis, string CreatedUtc, string[]? SuggestedTags = null)
{
    public static GameResearch Create(string game, string sourceUrl, string hypothesis)
    {
        game = game.Trim(); sourceUrl = sourceUrl.Trim(); hypothesis = hypothesis.Trim();
        if (game.Length is < 1 or > 120 || hypothesis.Length is < 1 or > 2000 ||
            !Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidDataException("Enter a game, an HTTP(S) source URL, and a hypothesis.");
        return new(Guid.NewGuid().ToString("N"), game, sourceUrl, hypothesis, DateTime.UtcNow.ToString("O"));
    }

    public string Save(string dataRoot)
    {
        // A new immutable note is created every time; existing research is never replaced.
        if (!Guid.TryParseExact(Id, "N", out _)) throw new InvalidDataException("Invalid research ID.");
        if (SuggestedTags is { Length: > 10 } || SuggestedTags?.Any(t => t is not ("controls" or "guidance")) == true) throw new InvalidDataException("Invalid research tags.");
        _ = Create(Game, SourceUrl, Hypothesis);
        var directory = Path.Combine(dataRoot, "research");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Id + ".json");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, this, new JsonSerializerOptions { WriteIndented = true });
        return path;
    }

    public static GameResearch Load(string path)
    {
        if (new FileInfo(path).Length > 16384) throw new InvalidDataException("Research note is too large.");
        var note = JsonSerializer.Deserialize<GameResearch>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid research note.");
        if (!Guid.TryParseExact(note.Id, "N", out _)) throw new InvalidDataException("Invalid research ID.");
        if (note.SuggestedTags is { Length: > 10 } || note.SuggestedTags?.Any(t => t is not ("controls" or "guidance")) == true) throw new InvalidDataException("Invalid research tags.");
        _ = Create(note.Game, note.SourceUrl, note.Hypothesis);
        return note;
    }
}
