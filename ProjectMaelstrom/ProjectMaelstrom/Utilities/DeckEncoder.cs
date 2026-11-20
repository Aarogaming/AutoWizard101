using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Native C# implementation of the Deimos deck encoder and decoder.
/// Ported from Deimos-Wizard101-main (src/deck_encoder.py).
/// Compact delimited format compressed via deflate/zlib and encoded in Base64.
/// </summary>
public static class DeckEncoder
{
    private const string Delimiter1 = "\\+";
    private const string Delimiter2 = "\\,";
    private const string Delimiter3 = "\\;";

    public static string Encode(
        Dictionary<string, int> normal,
        Dictionary<string, int> tc,
        Dictionary<string, int> item)
    {
        string sectionNormal = SerializeSection(normal);
        string sectionTc = SerializeSection(tc);
        string sectionItem = SerializeSection(item);

        string combined = $"{sectionNormal}{Delimiter3}{sectionTc}{Delimiter3}{sectionItem}";
        byte[] rawBytes = Encoding.UTF8.GetBytes(combined);

        using var memoryStream = new MemoryStream();
        using (var zlibStream = new ZLibStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlibStream.Write(rawBytes, 0, rawBytes.Length);
        }

        return Convert.ToBase64String(memoryStream.ToArray());
    }

    public static (Dictionary<string, int> Normal, Dictionary<string, int> Tc, Dictionary<string, int> Item) Decode(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        byte[] compressed = Convert.FromBase64String(token);
        using var memoryStream = new MemoryStream(compressed);
        using var zlibStream = new ZLibStream(memoryStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        zlibStream.CopyTo(outputStream);

        string text = Encoding.UTF8.GetString(outputStream.ToArray());
        string[] sections = text.Split(Delimiter3);

        if (sections.Length < 3)
        {
            throw new FormatException("Invalid deck token format; expected 3 sections.");
        }

        return (
            DeserializeSection(sections[0]),
            DeserializeSection(sections[1]),
            DeserializeSection(sections[2])
        );
    }

    private static string SerializeSection(Dictionary<string, int>? section)
    {
        if (section == null || section.Count == 0)
        {
            return "N";
        }

        var sb = new StringBuilder();
        bool first = true;
        foreach (var (k, v) in section)
        {
            if (!first)
            {
                sb.Append(Delimiter2);
            }
            sb.Append(k).Append(Delimiter1).Append(v);
            first = false;
        }
        return sb.ToString();
    }

    private static Dictionary<string, int> DeserializeSection(string section)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(section) || section == "N")
        {
            return dict;
        }

        string[] items = section.Split(Delimiter2, StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in items)
        {
            string[] parts = item.Split(Delimiter1, StringSplitOptions.None);
            if (parts.Length == 2 && int.TryParse(parts[1], out int count))
            {
                dict[parts[0]] = count;
            }
        }
        return dict;
    }
}
