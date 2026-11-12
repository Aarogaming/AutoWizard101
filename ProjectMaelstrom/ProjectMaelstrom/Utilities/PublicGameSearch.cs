using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record PublicGameResult(string Title, string Url)
{
    public override string ToString() => Title;
}

/// <summary>Public encyclopedia discovery; results are source links, not verified game facts.</summary>
internal static class PublicGameSearch
{
    public static async Task<IReadOnlyList<PublicGameResult>> SearchAsync(string game, CancellationToken cancellationToken = default)
    {
        game = game.Trim();
        if (game.Length is < 1 or > 120) throw new InvalidDataException("Enter a game title.");
        var query = Uri.EscapeDataString(game + " video game");
        var address = "https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch=" + query + "&srlimit=10&format=json";
        return Parse(await GuideReader.FetchAsync(address, cancellationToken));
    }

    internal static IReadOnlyList<PublicGameResult> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var results = new List<PublicGameResult>();
        foreach (var item in document.RootElement.GetProperty("query").GetProperty("search").EnumerateArray())
        {
            var title = item.GetProperty("title").GetString();
            if (string.IsNullOrWhiteSpace(title) || title.Length > 200) continue;
            results.Add(new(title, "https://en.wikipedia.org/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_'))));
            if (results.Count == 10) break;
        }
        return results;
    }
}
