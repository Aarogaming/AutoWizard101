using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Review evidence only. Entries grant no input or executable capability.</summary>
internal sealed record W101LegacyEntry(string Id, string Archive, string Sha256, string[] Capabilities,
    string Destination, string Qualification, string Integration, bool LiveQualified, string Limitations, string[] Evidence);

internal sealed record W101LegacyCatalog(int SchemaVersion, DateTimeOffset ReviewedUtc, string Scope, W101LegacyEntry[] Entries)
{
    public static W101LegacyCatalog Load()
    {
        using var stream = typeof(W101LegacyCatalog).Assembly.GetManifestResourceStream("Maelstrom.LegacyW101Catalog.json")
            ?? throw new InvalidDataException("The legacy review catalogue is missing from this build.");
        return Read(stream);
    }

    internal static W101LegacyCatalog Read(Stream stream)
    {
        var catalog = JsonSerializer.Deserialize<W101LegacyCatalog>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The legacy review catalogue is empty.");
        if (catalog.SchemaVersion != 1 || catalog.Entries is not { Length: > 0 and <= 100 }
            || string.IsNullOrWhiteSpace(catalog.Scope))
            throw new InvalidDataException("Unsupported legacy review catalogue.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var archives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in catalog.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id)
                || string.IsNullOrWhiteSpace(entry.Archive) || !archives.Add(entry.Archive)
                || entry.Sha256 is not { Length: 64 } || !entry.Sha256.All(Uri.IsHexDigit)
                || entry.Capabilities is not { Length: > 0 } || entry.Capabilities.Any(string.IsNullOrWhiteSpace)
                || entry.Evidence is not { Length: > 0 } || entry.Evidence.Any(string.IsNullOrWhiteSpace)
                || string.IsNullOrWhiteSpace(entry.Destination) || string.IsNullOrWhiteSpace(entry.Limitations)
                || string.IsNullOrWhiteSpace(entry.Qualification)
                || entry.LiveQualified || entry.Integration != "Catalogue only")
                throw new InvalidDataException("Legacy entries must retain provenance and cannot authorize execution.");
        }
        return catalog;
    }

    public W101LegacyEntry[] ForCapability(string? capability) => Entries
        .Where(entry => capability == null || entry.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
        .OrderBy(entry => entry.Archive, StringComparer.OrdinalIgnoreCase).ToArray();
}
