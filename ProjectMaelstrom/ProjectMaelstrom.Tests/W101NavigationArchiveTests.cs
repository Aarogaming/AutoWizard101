using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101NavigationArchiveTests
{
    private static byte[] Nav()
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write((short)2); writer.Write((short)3); writer.Write((short)0);
        foreach (var point in new[] { (0f, 0f, 0f), (3f, 4f, 0f), (3f, 8f, 3f) }.Select((value, index) => (value, index)))
        {
            writer.Write(point.value.Item1); writer.Write(point.value.Item2); writer.Write(point.value.Item3); writer.Write((short)point.index);
        }
        writer.Write(3);
        foreach (short index in new short[] { 0, 1, 1, 0, 1, 2 }) writer.Write(index);
        return stream.ToArray();
    }

    private static byte[] Archive(bool compressed = false, int version = 1, string[]? names = null)
    {
        names ??= ["zone.nav"];
        byte[] payload = Nav(); byte[] stored = payload;
        if (compressed)
        {
            using var buffer = new MemoryStream();
            using (var compressor = new ZLibStream(buffer, CompressionLevel.Fastest, leaveOpen: true)) compressor.Write(payload);
            stored = buffer.ToArray();
        }
        int offset = 13 + (version == 2 ? 1 : 0) + names.Sum(name => 21 + Encoding.ASCII.GetByteCount(name) + 1);
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write("KIWAD"u8); writer.Write(version); writer.Write(names.Length);
        if (version == 2) writer.Write((byte)0);
        foreach (string name in names)
        {
            writer.Write(offset); writer.Write(payload.Length); writer.Write(stored.Length);
            writer.Write(compressed); writer.Write(0); writer.Write(name.Length + 1); writer.Write(Encoding.ASCII.GetBytes(name + '\0'));
            offset += stored.Length;
        }
        foreach (string _ in names) writer.Write(stored);
        return stream.ToArray();
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public void ReadsOnlyNavAndLeavesStreamOpen(bool compressed, int version)
    {
        using var stream = new MemoryStream(Archive(compressed, version, ["other.bin", "zone.nav"]));
        var data = W101NavigationArchive.Read(stream);
        Assert.True(stream.CanRead);
        Assert.Equal(version, data.ArchiveVersion);
        Assert.Equal(2, data.ArchiveEntries);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Nav())).ToLowerInvariant(), data.MemberSha256);
        Assert.Equal(3, data.Map.Points.Count);
        Assert.Equal(10, data.Map.FindPath(0, 2).Distance);
    }

    [Fact]
    public void EveryArchiveTruncationIsRejected()
    {
        foreach (bool compressed in new[] { false, true })
        {
            var bytes = Archive(compressed);
            for (int length = 0; length < bytes.Length; length++)
                Assert.Throws<InvalidDataException>(() => W101NavigationArchive.Read(new MemoryStream(bytes[..length])));
        }
    }

    [Theory]
    [InlineData("zone.nav", "ZONE.NAV")]
    [InlineData("zone.nav", "../other")]
    [InlineData("zone.nav", "/other")]
    [InlineData("zone.nav", "a//b")]
    public void RejectsDuplicateAndMalformedNames(string first, string second)
    {
        Assert.Throws<InvalidDataException>(() => W101NavigationArchive.Read(new MemoryStream(Archive(names: [first, second]))));
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(9, 100001)]
    [InlineData(13, 0)]
    [InlineData(13, int.MaxValue)]
    [InlineData(17, -1)]
    [InlineData(17, int.MaxValue)]
    [InlineData(30, 1025)]
    public void RejectsUnsupportedOrOutOfBudgetMetadata(int offset, int value)
    {
        var data = Archive(); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);
        Assert.Throws<InvalidDataException>(() => W101NavigationArchive.Read(new MemoryStream(data)));
    }

    [Fact]
    public void RejectsOverlapMissingNavAndCorruptZlib()
    {
        var data = Archive(names: ["other.bin", "zone.nav"]);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(13 + 21 + 10), BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(13)));
        Assert.Throws<InvalidDataException>(() => W101NavigationArchive.Read(new MemoryStream(data)));
        Assert.Throws<InvalidDataException>(() => W101NavigationArchive.Read(new MemoryStream(Archive(names: ["other.bin"]))));
        data = Archive(true); data[^1] ^= 1;
        Assert.Throws<InvalidDataException>(() => W101NavigationArchive.Read(new MemoryStream(data)));
    }

    [Fact]
    public void RejectsDecodedLengthMismatchAndExpansionBeyondDeclaration()
    {
        foreach (int size in new[] { Nav().Length - 1, Nav().Length + 1 })
        {
            var data = Archive(true); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(17), size);
            Assert.Throws<InvalidDataException>(() => W101NavigationArchive.Read(new MemoryStream(data)));
        }
    }

    [Fact]
    public void BindingReportDistinguishesGeometryCandidatesFromTraversability()
    {
        var map = NavigationMap.Parse(Nav());
        var result = W101NavigationArchive.Analyze(map, new(0, 0, 40), new(3, 8, 3), 0);
        Assert.Equal("candidate", result.EvidenceGrade); Assert.False(result.ConnectorVerified);
        Assert.Equal(0, result.StartCandidates[0].Vertex);
        Assert.Equal(-40, result.StartCandidates[0].DeltaZ);
        Assert.Null(result.StartCandidates[0].WorldBearing);
        Assert.True(result.DirectedPath.Found); Assert.True(result.UndirectedReachable);
        Assert.Equal(3, result.MaximumEdgeDeltaZ);
        Assert.Equal(2.0 / 3.0, result.ReverseEdgeFraction);
        Assert.Equal(Math.Acos(0.8), result.SharpestTurnRadians!.Value, 8);
        var reverse = W101NavigationArchive.Analyze(map, new(3, 8, 3), new(0, 0, 0));
        Assert.False(reverse.DirectedPath.Found); Assert.True(reverse.UndirectedReachable);
        Assert.Null(reverse.MaximumEdgeDeltaZ); Assert.Null(reverse.SharpestTurnRadians);
    }

    [Fact]
    public void CandidateHeadingIsExplicitlyConvertedAndNonfiniteInputsRejected()
    {
        var map = NavigationMap.Parse(Nav());
        var result = W101NavigationArchive.Analyze(map, new(0, 1, 0), new(3, 8, 3), 0);
        Assert.Equal(0, result.StartCandidates[0].ClientHeadingCandidate);
        Assert.Equal(0, result.StartCandidates[0].HeadingError);
        Assert.Throws<InvalidDataException>(() => W101NavigationArchive.Analyze(map, new(double.NaN, 0, 0), new(0, 0, 0)));
    }

    [Fact]
    public void OptInInstalledArchivesProduceCandidateBindingReports()
    {
        string? output = Environment.GetEnvironmentVariable("MAELSTROM_NAV_AUDIT_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return;
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        const string installed = @"C:\ProgramData\KingsIsle Entertainment\Wizard101\Data\GameData";
        var cases = new[]
        {
            (Name: "commons", Archive: "WizardCity-WC_Hub.wad", Bytes: 8658, Vertices: 290, Edges: 1147,
                Start: new NavigationPoint(-371.1885, -518.4771, -9.92746), Goal: new NavigationPoint(6928.848, -447.477, -27.914),
                StartHeading: 3.612695, GoalHeading: 5.50698),
            (Name: "pavilion", Archive: "WizardCity-WC_Streets-Interiors-WC_PET_Park.wad", Bytes: 3408, Vertices: 101, Edges: 496,
                Start: new NavigationPoint(-1720.781, 674.815, -40.346), Goal: new NavigationPoint(-3962.8569, -1234.6804, -5.67993),
                StartHeading: 0.0, GoalHeading: 1.0857346)
        };
        // Explicit recorded-input rebinding; never calls a live reader or treats
        // the receipt as indefinitely current. Output remains candidate-only.
        var startReceipt = Environment.GetEnvironmentVariable("MAELSTROM_NAV_START_RECEIPT");
        string? startReceiptSha256 = null;
        if (!string.IsNullOrWhiteSpace(startReceipt))
        {
            var bytes = GameExperiments.BoundedBytes(startReceipt, 262144);
            using var source = System.Text.Json.JsonDocument.Parse(bytes);
            var pose = source.RootElement.GetProperty("PoseCandidate");
            Assert.Equal("WizardCity/WC_Hub", pose.GetProperty("ZoneCandidate").GetProperty("zoneName").GetString());
            var position = pose.GetProperty("Position");
            cases[0].Start = new(position.GetProperty("X").GetDouble(), position.GetProperty("Y").GetDouble(), position.GetProperty("Z").GetDouble());
            cases[0].StartHeading = pose.GetProperty("YawRadians").GetDouble();
            startReceiptSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        }
        foreach (var item in cases)
        {
            var data = W101NavigationArchive.Read(Path.Combine(installed, item.Archive));
            Assert.Equal(item.Bytes, data.DecodedBytes);
            Assert.Equal(item.Vertices, data.Map.Points.Count);
            Assert.Equal(item.Vertices - 1, data.Map.HeaderCount);
            Assert.Equal(0, data.Map.HeaderUnknown);
            Assert.Equal(item.Edges, data.Map.Edges.Count);
            var analysis = W101NavigationArchive.Analyze(data.Map, item.Start, item.Goal, item.StartHeading, item.GoalHeading);
            var report = new
            {
                schemaVersion = 1,
                evidenceGrade = "candidate",
                liveQualified = false,
                sourceArchive = item.Archive,
                data.MemberSha256,
                data.DecodedBytes,
                data.ArchiveVersion,
                data.ArchiveEntries,
                header = new[] { (int)data.Map.HeaderCount, data.Map.VertexRecords, data.Map.HeaderUnknown },
                vertexCount = data.Map.Points.Count,
                edgeCount = data.Map.Edges.Count,
                item.Start,
                item.Goal,
                item.StartHeading,
                item.GoalHeading,
                startReceipt = item.Name == "commons" ? startReceipt : null,
                startReceiptSha256 = item.Name == "commons" ? startReceiptSha256 : null,
                analysis,
                perEdge = analysis.DirectedPath.Vertices.Zip(analysis.DirectedPath.Vertices.Skip(1), (from, to) => new
                {
                    from,
                    to,
                    start = data.Map.Points[from],
                    end = data.Map.Points[to],
                    deltaZ = data.Map.Points[to].Z - data.Map.Points[from].Z
                }).ToArray(),
                asymmetricEdges = data.Map.Edges.Where(e => !data.Map.Edges.Contains(new NavigationEdge(e.To, e.From))).ToArray(),
                excludeLargestCommonsEdge = item.Name == "commons" ? data.Map.FindPath(analysis.StartCandidates[0].Vertex, 209, new HashSet<NavigationEdge> { new(183, 192), new(199, 209) }) : null,
                excludeAbove40 = data.Map.FindPath(analysis.StartCandidates[0].Vertex, analysis.GoalCandidates[0].Vertex,
                    data.Map.Edges.Where(e => Math.Abs(data.Map.Points[e.To].Z - data.Map.Points[e.From].Z) > 40).ToHashSet()),
                limitations = "Raw nearest-XY bindings are unverified connectors; stored edge symmetry is not proof of physical directionality. No clearance, portal or Dance prompt anchor is qualified."
            };
            string destination = Path.Combine(output, item.Name + "-candidate-binding.json");
            using var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            System.Text.Json.JsonSerializer.Serialize(file, report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
    }
}
