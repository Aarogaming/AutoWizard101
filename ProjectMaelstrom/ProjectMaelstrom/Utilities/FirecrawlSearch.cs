using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Explicit Firecrawl search; page content is untrusted and is never executed.</summary>
internal static class FirecrawlSearch
{
    public static async Task<IReadOnlyList<PublicGameResult>> SearchAsync(string game, string apiKey, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Set FIRECRAWL_API_KEY for Maelstrom before using Firecrawl search.");
        if (string.IsNullOrWhiteSpace(game) || game.Length > 120) throw new InvalidDataException("Enter a game title of at most 120 characters.");
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.firecrawl.dev/v2/search");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new { query = game.Trim() + " game controls tutorial guide", limit = 8 });
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Firecrawl search failed (HTTP {(int)response.StatusCode}). Check your account and try again.");
        if (response.Content.Headers.ContentLength > 1_048_576) throw new InvalidDataException("Search response is too large.");
        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + read > 1_048_576) throw new InvalidDataException("Search response is too large.");
            output.Write(buffer, 0, read);
        }
        return Parse(output.ToArray());
    }

    internal static IReadOnlyList<PublicGameResult> Parse(byte[] bytes)
    {
        using var doc = JsonDocument.Parse(bytes);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("web", out var web) || web.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Firecrawl returned an unexpected response.");
        var results = new List<PublicGameResult>();
        foreach (var entry in web.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("url", out var rawUrl) || rawUrl.ValueKind != JsonValueKind.String ||
                !Uri.TryCreate(rawUrl.GetString(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)) continue;
            var title = entry.TryGetProperty("title", out var rawTitle) && rawTitle.ValueKind == JsonValueKind.String ? rawTitle.GetString() : uri.Host;
            if (string.IsNullOrWhiteSpace(title) || title.Length > 200 || uri.AbsoluteUri.Length > 2048) continue;
            results.Add(new(title, uri.AbsoluteUri));
            if (results.Count == 8) break;
        }
        return results;
    }
}
