using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class OperationJournalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-journal-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTime Time = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private static OperationRequest Request() => new(Guid.NewGuid().ToString("N"), "test.journal", "v1", 60,
        false, "offline", new Dictionary<string, string>());
    private string Folder(OperationRequest request)
    {
        var path = Path.Combine(_root, request.Id); Directory.CreateDirectory(path); return path;
    }
    private static OperationReceipt Receipt(OperationRequest request, OperationState state, int seconds = 0) =>
        new(request, state, Time.AddSeconds(seconds), "saved outcome", ["relative-report.json"]);
    private static string Admission(string folder) => Path.Combine(OperationJournal.DirectoryPath(folder), "admission.json");
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Edit(string path, Action<JsonObject> change)
    {
        var value = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject(); change(value);
        File.WriteAllText(path, value.ToJsonString());
    }
    private (string Folder, OperationRequest Request, OperationReceipt Running) Running()
    {
        var request = Request(); var folder = Folder(request); OperationJournal.Create(folder, request);
        OperationJournal.Append(folder, Receipt(request, OperationState.Queued));
        var running = Receipt(request, OperationState.Running, 1); OperationJournal.Append(folder, running);
        return (folder, request, running);
    }

    [Fact]
    public void AdmissionIsFrozenAndCannotBeRecreated()
    {
        var input = Path.Combine(_root, "source.json");
        var pins = new Dictionary<string, string> { [input] = new('a', 64) };
        var request = Request() with { Pins = pins }; var folder = Folder(request);
        var created = OperationJournal.Create(folder, request); var bytes = File.ReadAllBytes(Admission(folder));
        pins.Clear();
        Assert.Equal(new string('a', 64), created.Request.Pins[input]);
        var read = OperationJournal.Read(folder)!;
        Assert.Equal(new string('a', 64), read.Request.Pins[input]);
        Assert.Equal(0, read.Sequence); Assert.Null(read.Latest); Assert.Equal(Hash(bytes), read.LastSha256);
        Assert.Throws<InvalidDataException>(() => OperationJournal.Create(folder, created.Request));
        Assert.Equal(bytes, File.ReadAllBytes(Admission(folder)));
    }

    [Fact]
    public void LifecycleAppendsWithoutChangingEarlierEvidence()
    {
        var run = Running(); var admission = File.ReadAllBytes(Admission(run.Folder));
        var first = File.ReadAllBytes(OperationJournal.SnapshotPath(run.Folder, 1));
        var second = File.ReadAllBytes(OperationJournal.SnapshotPath(run.Folder, 2));
        var completed = Receipt(run.Request, OperationState.Completed, 2);
        OperationJournal.Append(run.Folder, completed);
        var state = OperationJournal.Read(run.Folder)!;
        Assert.Equal(3, state.Sequence); Assert.Equal(OperationState.Completed, state.Latest!.State);
        Assert.Equal(completed.UpdatedUtc, state.Latest.UpdatedUtc);
        Assert.Equal(new[] { "relative-report.json" }, state.Latest.Artifacts);
        Assert.Equal(admission, File.ReadAllBytes(Admission(run.Folder)));
        Assert.Equal(first, File.ReadAllBytes(OperationJournal.SnapshotPath(run.Folder, 1)));
        Assert.Equal(second, File.ReadAllBytes(OperationJournal.SnapshotPath(run.Folder, 2)));
        Assert.Equal(Hash(File.ReadAllBytes(OperationJournal.SnapshotPath(run.Folder, 3))), state.LastSha256);
    }

    [Fact]
    public void IdenticalLatestAppendIsIdempotentButConflictingTerminalCannotReplaceIt()
    {
        var run = Running(); var terminal = Receipt(run.Request, OperationState.Completed, 2);
        OperationJournal.Append(run.Folder, terminal);
        var bytes = File.ReadAllBytes(OperationJournal.SnapshotPath(run.Folder, 3));
        OperationJournal.Append(run.Folder, terminal);
        Assert.Equal(3, OperationJournal.Read(run.Folder)!.Sequence);
        Assert.False(File.Exists(OperationJournal.SnapshotPath(run.Folder, 4)));
        Assert.Throws<InvalidDataException>(() => OperationJournal.Append(run.Folder, terminal with { Message = "different outcome" }));
        Assert.Throws<InvalidDataException>(() => OperationJournal.Append(run.Folder, terminal with { State = OperationState.Failed }));
        Assert.Equal(bytes, File.ReadAllBytes(OperationJournal.SnapshotPath(run.Folder, 3)));
    }

    [Fact]
    public void AppendCannotChangeTheAdmittedRequest()
    {
        var run = Running();
        var changed = Receipt(run.Request with { TimeoutSeconds = 61 }, OperationState.Completed, 2);
        Assert.Throws<InvalidDataException>(() => OperationJournal.Append(run.Folder, changed));
        Assert.Equal(2, OperationJournal.Read(run.Folder)!.Sequence);
        Assert.False(File.Exists(OperationJournal.SnapshotPath(run.Folder, 3)));
    }

    [Fact]
    public void SequenceRemainsAuthoritativeWhenTheWallClockMovesBackward()
    {
        var run = Running();
        var terminal = Receipt(run.Request, OperationState.Completed, -300);
        OperationJournal.Append(run.Folder, terminal);
        var state = OperationJournal.Read(run.Folder)!;
        Assert.Equal(3, state.Sequence); Assert.Equal(terminal.UpdatedUtc, state.Latest!.UpdatedUtc);
        Assert.Equal(OperationState.Completed, state.Latest.State);
    }

    [Fact]
    public void QueuedWorkCannotClaimCompletionWithoutRunning()
    {
        var request = Request(); var folder = Folder(request); OperationJournal.Create(folder, request);
        OperationJournal.Append(folder, Receipt(request, OperationState.Queued));
        Assert.Throws<InvalidDataException>(() => OperationJournal.Append(folder, Receipt(request, OperationState.Completed, 1)));
        Assert.Equal(OperationState.Queued, OperationJournal.Read(folder)!.Latest!.State);
        Assert.False(File.Exists(OperationJournal.SnapshotPath(folder, 2)));
    }

    [Fact]
    public void CompatibilityProjectionNeverReplacesImmutableHistory()
    {
        var run = Running(); var terminal = Receipt(run.Request, OperationState.Completed, 2);
        OperationJournal.Append(run.Folder, terminal);
        var bytes = File.ReadAllBytes(OperationJournal.SnapshotPath(run.Folder, 3));
        var projection = Path.Combine(run.Folder, "receipt.json");
        File.WriteAllText(projection, "broken projection");
        Assert.Equal(OperationState.Completed, OperationJournal.Read(run.Folder)!.Latest!.State);
        OperationJournal.Project(run.Folder, terminal);
        Assert.Equal(OperationState.Completed, JsonSerializer.Deserialize<OperationReceipt>(File.ReadAllBytes(projection))!.State);
        Assert.Equal(bytes, File.ReadAllBytes(OperationJournal.SnapshotPath(run.Folder, 3)));
    }

    [Fact]
    public void IncompleteStagingIsIgnoredAndPreserved()
    {
        var request = Request(); var folder = Folder(request); var journal = OperationJournal.DirectoryPath(folder);
        Directory.CreateDirectory(journal);
        var pending = Path.Combine(journal, ".pending-" + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(pending, "incomplete bytes");
        Assert.Null(OperationJournal.Read(folder));
        File.WriteAllText(Admission(folder), JsonSerializer.Serialize(new OperationJournalAdmission(1, request)));
        Assert.Equal(0, OperationJournal.Read(folder)!.Sequence);
        Assert.Equal("incomplete bytes", File.ReadAllText(pending));
    }

    [Fact]
    public void FileAtJournalPathCannotBeTreatedAsMissingLegacyHistory()
    {
        var request = Request(); var folder = Folder(request); var path = OperationJournal.DirectoryPath(folder);
        File.WriteAllText(path, "not a directory");
        Assert.Throws<InvalidDataException>(() => OperationJournal.Read(folder));
        Assert.Equal("not a directory", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("malformed-admission")]
    [InlineData("unknown-admission-field")]
    [InlineData("duplicate-admission-field")]
    [InlineData("missing-admission")]
    [InlineData("changed-request")]
    [InlineData("wrong-sequence")]
    [InlineData("missing-first-snapshot")]
    [InlineData("wrong-hash")]
    public void InvalidCommittedHistoryIsRejectedWithoutRepair(string problem)
    {
        var run = Running(); var admission = Admission(run.Folder);
        var first = OperationJournal.SnapshotPath(run.Folder, 1); var second = OperationJournal.SnapshotPath(run.Folder, 2);
        switch (problem)
        {
            case "malformed-admission": File.WriteAllText(admission, "{broken"); break;
            case "unknown-admission-field": Edit(admission, value => value["Unsupported"] = true); break;
            case "duplicate-admission-field": File.WriteAllText(admission, File.ReadAllText(admission).Insert(1, "\"SchemaVersion\":1,")); break;
            case "missing-admission": File.Delete(admission); break;
            case "changed-request": Edit(second, value => value["Receipt"]!["Request"]!["TimeoutSeconds"] = 61); break;
            case "wrong-sequence": Edit(second, value => value["Sequence"] = 3); break;
            case "missing-first-snapshot": File.Delete(first); break;
            case "wrong-hash": Edit(second, value => value["PreviousSha256"] = new string('f', 64)); break;
            default: throw new InvalidOperationException();
        }
        var files = Directory.GetFiles(OperationJournal.DirectoryPath(run.Folder));
        var saved = files.ToDictionary(path => path, File.ReadAllBytes);
        Assert.Throws<InvalidDataException>(() => OperationJournal.Read(run.Folder));
        Assert.Equal(files.Length, Directory.GetFiles(OperationJournal.DirectoryPath(run.Folder)).Length);
        foreach (var item in saved) Assert.Equal(item.Value, File.ReadAllBytes(item.Key));
    }

    [Theory]
    [InlineData("message")]
    [InlineData("artifact-count")]
    [InlineData("artifact-path")]
    [InlineData("empty-artifact")]
    public void OversizeOrInvalidOutcomesCannotPublishASnapshot(string problem)
    {
        var run = Running(); var receipt = Receipt(run.Request, OperationState.Completed, 2);
        receipt = problem switch
        {
            "message" => receipt with { Message = new('x', OperationJournal.MaximumMessageCharacters + 1) },
            "artifact-count" => receipt with { Artifacts = Enumerable.Repeat("report.json", 65).ToArray() },
            "artifact-path" => receipt with { Artifacts = [new('x', 32769)] },
            "empty-artifact" => receipt with { Artifacts = [" "] },
            _ => throw new InvalidOperationException()
        };
        Assert.Throws<InvalidDataException>(() => OperationJournal.Append(run.Folder, receipt));
        Assert.Equal(2, OperationJournal.Read(run.Folder)!.Sequence);
        Assert.False(File.Exists(OperationJournal.SnapshotPath(run.Folder, 3)));
    }

    [Theory]
    [InlineData("snapshot-count")]
    [InlineData("directory-count")]
    [InlineData("encoded-bytes")]
    public void JournalReadBoundsAreEnforcedBeforeAcceptance(string problem)
    {
        var request = Request(); var folder = Folder(request); OperationJournal.Create(folder, request);
        switch (problem)
        {
            case "snapshot-count": File.WriteAllText(OperationJournal.SnapshotPath(folder, 5), "{}"); break;
            case "directory-count":
                for (var i = 0; i < 13; i++)
                    File.WriteAllText(Path.Combine(OperationJournal.DirectoryPath(folder), ".pending-" + Guid.NewGuid().ToString("N") + ".tmp"), "pending");
                break;
            case "encoded-bytes": File.WriteAllBytes(Admission(folder), new byte[OperationJournal.MaximumBytes + 1]); break;
            default: throw new InvalidOperationException();
        }
        Assert.Throws<InvalidDataException>(() => OperationJournal.Read(folder));
    }

    [Fact]
    public void SerializationReservesRoomForTheSnapshotEnvelope()
    {
        var run = Running();
        var receipt = Receipt(run.Request, OperationState.Completed, 2) with
        {
            Artifacts = Enumerable.Repeat(new string('x', 16320), 64).ToArray()
        };
        var raw = JsonSerializer.SerializeToUtf8Bytes(receipt, new JsonSerializerOptions { WriteIndented = true });
        Assert.InRange(raw.Length, OperationJournal.MaximumBytes - 4095, OperationJournal.MaximumBytes);
        Assert.Throws<InvalidDataException>(() => OperationJournal.Append(run.Folder, receipt));
        Assert.False(File.Exists(OperationJournal.SnapshotPath(run.Folder, 3)));
        Assert.Equal(2, OperationJournal.Read(run.Folder)!.Sequence);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
