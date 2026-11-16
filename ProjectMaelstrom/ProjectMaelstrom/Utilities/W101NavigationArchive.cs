using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace ProjectMaelstrom.Utilities;

internal sealed record NavigationArchiveData(string MemberSha256, int ArchiveVersion, int ArchiveEntries,
    int DecodedBytes, NavigationMap Map);
internal sealed record NavigationVertexCandidate(int Vertex, NavigationPoint Point, double DistanceXy,
    double DeltaZ, double Distance3D, int Incoming, int Outgoing, double? WorldBearing,
    double? ClientHeadingCandidate, double? HeadingError);
internal sealed record NavigationBindingReport(string EvidenceGrade, bool ConnectorVerified,
    IReadOnlyList<NavigationVertexCandidate> StartCandidates, IReadOnlyList<NavigationVertexCandidate> GoalCandidates,
    NavigationPath DirectedPath, bool UndirectedReachable, double? MaximumEdgeDeltaZ,
    double? SharpestTurnRadians, double? ReverseEdgeFraction);

/// <summary>Bounded read-only ingestion of the zone.nav member using the KIWAD journal
/// layout already used by WadArchiveReader. It does not extract files, read a process,
/// interpret collision geometry or grant movement authority.</summary>
internal static class W101NavigationArchive
{
    internal const int MaximumEntries = 100000;
    internal const int MaximumJournalBytes = 16 * 1024 * 1024;
    internal const int MaximumNameBytes = 1024;
    private sealed record Entry(int Offset, int Size, int StoredSize, bool Compressed);

    internal static NavigationArchiveData Read(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Read(input);
    }

    /// <summary>Leaves the caller's readable, seekable stream open. Only zone.nav is
    /// decoded; other members are checked for journal/range consistency.</summary>
    internal static NavigationArchiveData Read(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead || !input.CanSeek || input.Length < 13 || input.Length > int.MaxValue)
            throw new InvalidDataException("Unsupported archive stream or length.");
        input.Position = 0;
        using var reader = new BinaryReader(input, Encoding.ASCII, leaveOpen: true);
        try
        {
            byte[] Bytes(int count)
            {
                var value = reader.ReadBytes(count);
                if (value.Length != count) throw new InvalidDataException("Truncated archive.");
                return value;
            }
            if (!Bytes(5).AsSpan().SequenceEqual("KIWAD"u8)) throw new InvalidDataException("Invalid archive magic.");
            int version = reader.ReadInt32(), count = reader.ReadInt32();
            if (version is not (1 or 2) || count is < 1 or > MaximumEntries)
                throw new InvalidDataException("Unsupported archive version or entry count.");
            if (version == 2) reader.ReadByte();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<Entry>(count);
            Entry? selected = null;
            for (int i = 0; i < count; i++)
            {
                if (input.Position > MaximumJournalBytes - 21) throw new InvalidDataException("Archive journal budget exceeded.");
                int offset = reader.ReadInt32(), size = reader.ReadInt32(), compressedSize = reader.ReadInt32();
                byte compressed = reader.ReadByte();
                reader.ReadInt32(); // Preserved journal layout; checksum semantics are not assumed.
                int nameLength = reader.ReadInt32();
                if (nameLength is < 1 or > MaximumNameBytes || input.Position + nameLength > MaximumJournalBytes)
                    throw new InvalidDataException("Invalid archive member name length.");
                var nameBytes = Bytes(nameLength);
                if (nameBytes[^1] != 0 || nameBytes.AsSpan(0, nameBytes.Length - 1).Contains((byte)0) ||
                    nameBytes.AsSpan(0, nameBytes.Length - 1).IndexOfAnyInRange((byte)128, byte.MaxValue) >= 0)
                    throw new InvalidDataException("Invalid archive member name encoding.");
                string name = Encoding.ASCII.GetString(nameBytes, 0, nameBytes.Length - 1).Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(name) || name.StartsWith('/') || name.Contains(':') ||
                    name.Split('/').Any(part => part is "" or "." or "..") || name.Any(char.IsControl) || !names.Add(name))
                    throw new InvalidDataException("Invalid or duplicate archive member name.");
                if (offset < 0 || size < 0 || compressedSize < 0 || compressed > 1)
                    throw new InvalidDataException("Invalid archive member metadata.");
                int storedSize = compressed == 1 ? compressedSize : size;
                if ((long)offset + storedSize > input.Length) throw new InvalidDataException("Archive member range exceeds file.");
                var entry = new Entry(offset, size, storedSize, compressed == 1);
                entries.Add(entry);
                if (name.Equals("zone.nav", StringComparison.OrdinalIgnoreCase)) selected = entry;
            }
            long end = input.Position;
            foreach (var entry in entries.OrderBy(entry => entry.Offset))
            {
                if (entry.Offset < end) throw new InvalidDataException("Archive member overlaps journal or another member.");
                end = (long)entry.Offset + entry.StoredSize;
            }
            if (selected is null) throw new InvalidDataException("Archive has no zone.nav member.");
            if (selected.Size < 10 || selected.Size > NavigationMap.MaximumBytes ||
                selected.StoredSize < 1 || selected.StoredSize > NavigationMap.MaximumBytes + 65536)
                throw new InvalidDataException("Navigation member budget exceeded.");
            input.Position = selected.Offset;
            byte[] stored = Bytes(selected.StoredSize);
            byte[] decoded;
            if (!selected.Compressed) decoded = stored;
            else
            {
                if (stored.Length < 6) throw new InvalidDataException("Truncated zlib member.");
                using var compressedInput = new MemoryStream(stored, writable: false);
                using var decoder = new ZLibStream(compressedInput, CompressionMode.Decompress);
                decoded = new byte[selected.Size];
                decoder.ReadExactly(decoded);
                if (decoder.ReadByte() != -1) throw new InvalidDataException("Decoded member exceeds declared size.");
                // Check the zlib trailer independently: some truncated stream paths can return EOF.
                uint a = 1, b = 0;
                foreach (byte value in decoded) { a = (a + value) % 65521; b = (b + a) % 65521; }
                uint trailer = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(stored.AsSpan(stored.Length - 4));
                if (trailer != (b << 16 | a)) throw new InvalidDataException("Invalid zlib checksum/trailer.");
            }
            return new(Convert.ToHexString(SHA256.HashData(decoded)).ToLowerInvariant(), version, count,
                decoded.Length, NavigationMap.Parse(decoded));
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Truncated archive or navigation member.", exception);
        }
    }

    /// <summary>Ranks raw proximity candidates and checks structural reachability. No
    /// connector, clearance, floor, portal or destination prompt is thereby verified.
    /// Reverse-edge fraction describes stored symmetry, not physical directionality.</summary>
    internal static NavigationBindingReport Analyze(NavigationMap map, NavigationPoint start, NavigationPoint goal,
        double? startHeading = null, double? goalHeading = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!new[] { start.X, start.Y, start.Z, goal.X, goal.Y, goal.Z }.All(double.IsFinite) ||
            startHeading is { } sh && !double.IsFinite(sh) || goalHeading is { } gh && !double.IsFinite(gh))
            throw new InvalidDataException("Invalid binding pose or heading.");
        var incoming = new int[map.Points.Count]; var outgoing = new int[map.Points.Count];
        var undirected = Enumerable.Range(0, map.Points.Count).Select(_ => new List<int>()).ToArray();
        foreach (var edge in map.Edges)
        {
            outgoing[edge.From]++; incoming[edge.To]++;
            undirected[edge.From].Add(edge.To); undirected[edge.To].Add(edge.From);
        }
        IReadOnlyList<NavigationVertexCandidate> Rank(NavigationPoint pose, double? heading) =>
            Array.AsReadOnly(map.Points.Select((point, vertex) =>
            {
                double dx = point.X - pose.X, dy = point.Y - pose.Y, dz = point.Z - pose.Z;
                double xy = Math.Sqrt(dx * dx + dy * dy);
                double? bearing = xy == 0 ? null : Math.Atan2(dy, dx);
                double? clientHeading = xy == 0 ? null : Math.Atan2(-dx, -dy);
                double? error = heading is { } h && clientHeading is { } c ? Math.Atan2(Math.Sin(c - h), Math.Cos(c - h)) : null;
                return new NavigationVertexCandidate(vertex, point, xy, dz, Math.Sqrt(xy * xy + dz * dz),
                    incoming[vertex], outgoing[vertex], bearing, clientHeading, error);
            }).OrderBy(value => value.DistanceXy).ThenBy(value => value.Vertex).Take(5).ToArray());
        var starts = Rank(start, startHeading); var goals = Rank(goal, goalHeading);
        var path = map.FindPath(starts[0].Vertex, goals[0].Vertex, maximumVisited: NavigationMap.MaximumVertices);
        var seen = new HashSet<int> { starts[0].Vertex }; var queue = new Queue<int>(); queue.Enqueue(starts[0].Vertex);
        while (queue.TryDequeue(out int current))
            foreach (int next in undirected[current]) if (seen.Add(next)) queue.Enqueue(next);
        double? maxDz = path.Found ? 0 : null, maxTurn = path.Found ? 0 : null;
        for (int i = 1; i < path.Vertices.Count; i++)
        {
            var a = map.Points[path.Vertices[i - 1]]; var b = map.Points[path.Vertices[i]];
            maxDz = Math.Max(maxDz!.Value, Math.Abs(b.Z - a.Z));
            if (i + 1 >= path.Vertices.Count) continue;
            var c = map.Points[path.Vertices[i + 1]];
            double ux = b.X - a.X, uy = b.Y - a.Y, vx = c.X - b.X, vy = c.Y - b.Y;
            double norm = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
            if (norm > 0) maxTurn = Math.Max(maxTurn!.Value, Math.Acos(Math.Clamp((ux * vx + uy * vy) / norm, -1, 1)));
        }
        var unique = map.Edges.ToHashSet();
        double? symmetry = map.Edges.Count == 0 ? null : (double)map.Edges.Count(edge => unique.Contains(new(edge.To, edge.From))) / map.Edges.Count;
        return new("candidate", false, starts, goals, path, seen.Contains(goals[0].Vertex), maxDz, maxTurn, symmetry);
    }
}
