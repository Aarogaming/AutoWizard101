using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ProjectMaelstrom.Utilities;

/// <summary>A bounded import of Automatus' historical waypoint text format.
/// This is route-training evidence only; it is not an executable route.</summary>
internal sealed record AutomatusWaypointEntry(NavigationPoint Point, bool IsLegacyControlMarker,
    int? OpaqueMarkerCode);

internal sealed record AutomatusWaypointMap(string Name, IReadOnlyList<AutomatusWaypointEntry> Entries,
    string SourceSha256, string SourceFormat, string Qualification);

/// <summary>Imports Automatus v2 point maps without importing its controller or
/// wizwalker timed-goto behavior. Legacy (0,0,z) controls are preserved as opaque markers.</summary>
internal static class AutomatusWaypointMapImporter
{
    internal const string SourceFormat = "automatus-waypoint-map-v1";
    internal const string Qualification = "candidate-only";
    internal const int MaximumBytes = 1_048_576;
    internal const int MaximumEntries = 4096;
    private const int MaximumCoordinateMagnitude = 10_000_000;

    internal static AutomatusWaypointMap Parse(string content, string expectedName)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(expectedName) || expectedName.Length > 128 || expectedName.Any(char.IsControl))
            throw new InvalidDataException("Automatus map name is invalid.");
        if (Encoding.UTF8.GetByteCount(content) > MaximumBytes)
            throw new InvalidDataException("Automatus map exceeds the import size limit.");

        var normalized = content.TrimStart('\uFEFF');
        var lines = normalized.Split('\n');
        if (lines.Length < 2 || !string.Equals(lines[0].TrimEnd('\r').Trim(), expectedName, StringComparison.Ordinal))
            throw new InvalidDataException("Automatus map header does not match the selected map name.");

        var entries = new List<AutomatusWaypointEntry>();
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].TrimEnd('\r').Trim();
            if (line.Length == 0) continue;
            if (entries.Count >= MaximumEntries)
                throw new InvalidDataException("Automatus map exceeds the waypoint count limit.");

            var fields = line.Split(',');
            if (fields.Length != 3 || !TryCoordinate(fields[0], out var x) ||
                !TryCoordinate(fields[1], out var y) || !TryCoordinate(fields[2], out var z))
                throw new InvalidDataException($"Automatus map has an invalid XYZ row at line {lineIndex + 1}.");

            // Automatus follow_map interprets (0,0,z) as special key commands.
            // The forward/reverse comments disagree, so retain the code but never dispatch it here.
            var marker = x == 0 && y == 0 && z is >= -1 and <= 1;
            entries.Add(new(new(x, y, z), marker, marker ? checked((int)z) : null));
        }

        if (entries.Count < 2)
            throw new InvalidDataException("Automatus map needs at least two route entries.");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        return new(expectedName, entries.AsReadOnly(), hash, SourceFormat, Qualification);
    }

    private static bool TryCoordinate(string value, out double coordinate)
    {
        coordinate = 0;
        if (!int.TryParse(value.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) ||
            parsed is < -MaximumCoordinateMagnitude or > MaximumCoordinateMagnitude)
            return false;
        coordinate = parsed;
        return true;
    }
}
