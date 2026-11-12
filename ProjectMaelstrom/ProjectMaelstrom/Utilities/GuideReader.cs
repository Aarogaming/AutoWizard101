using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectMaelstrom.Utilities;

/// <summary>Fetches a user-selected HTTPS guide without running page content.</summary>
internal static class GuideReader
{
    private const int MaxBytes = 512 * 1024;
    public static async Task<string> FetchAsync(string address, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidDataException("Enter an HTTPS guide URL without credentials.");
        var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(IsPrivate)) throw new InvalidDataException("Choose a public guide website.");
        using var handler = new System.Net.Http.SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false,
            ConnectCallback = async (context, token) =>
            {
                var socket = new System.Net.Sockets.Socket(addresses[0].AddressFamily, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                try { await socket.ConnectAsync(addresses[0], context.DnsEndPoint.Port, token); return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true); }
                catch { socket.Dispose(); throw; }
            } };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MaelstromResearch/0.2 (Windows desktop game research)");
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var type = response.Content.Headers.ContentType?.MediaType;
        if (type is not ("text/html" or "text/plain" or "application/json")) throw new InvalidDataException("This source is not an HTML, JSON or plain-text page.");
        if (response.Content.Headers.ContentLength > MaxBytes) throw new InvalidDataException("Guide exceeds 512 KiB.");
        using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + count > MaxBytes) throw new InvalidDataException("Guide exceeds 512 KiB.");
            buffer.Write(chunk, 0, count);
        }
        return ExtractText(Encoding.UTF8.GetString(buffer.ToArray()), type == "text/html");
    }

    internal static string ExtractText(string source, bool html)
    {
        if (!html) return source[..Math.Min(source.Length, 100000)];
        var options = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant;
        var timeout = TimeSpan.FromSeconds(1);
        var text = Regex.Replace(source, @"<(script|style|noscript)\b[^>]*>.*?</\1\s*>", " ", options, timeout);
        text = Regex.Replace(text, @"</?(p|li|div|h[1-6]|br)\b[^>]*>", "\n", options, timeout);
        text = Regex.Replace(text, @"<[^>]*>", " ", options, timeout);
        text = WebUtility.HtmlDecode(text);
        return text[..Math.Min(text.Length, 100000)];
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;
        var b = ip.GetAddressBytes();
        if (b.Length == 16) return (b[0] & 0xfe) == 0xfc;
        return b[0] is 0 or 10 or 127 or >= 224 || (b[0] == 169 && b[1] == 254) || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] is >= 64 and <= 127);
    }
}
