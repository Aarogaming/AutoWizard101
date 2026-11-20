using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectMaelstrom.Utilities;

internal sealed record OperationJournalAdmission(int SchemaVersion, OperationRequest Request);
internal sealed record OperationJournalEntry(int SchemaVersion, int Sequence, string PreviousSha256, OperationReceipt Receipt);
internal sealed record OperationJournalState(OperationRequest Request, OperationReceipt? Latest, int Sequence, string LastSha256);

/// <summary>Small immutable lifecycle journal for new scheduler jobs. Its committed
/// records are authoritative; receipt.json remains a replaceable compatibility view.
/// Hash links detect inconsistent evidence, not malicious rewriting of the whole store.</summary>
internal static class OperationJournal
{
    internal const int MaximumBytes = 1048576;
    internal const int MaximumRequestBytes = 524288;
    internal const int MaximumSnapshots = 4;
    internal const int MaximumMessageCharacters = 16384;
    private const int MaximumDirectoryEntries = 13;
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static string DirectoryPath(string jobDirectory) => Path.Combine(jobDirectory, "journal");
    internal static string SnapshotPath(string jobDirectory, int sequence) =>
        Path.Combine(DirectoryPath(jobDirectory), sequence.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".json");
    internal static bool IsTerminal(OperationState state) => Enum.IsDefined(state) && state is not (OperationState.Queued or OperationState.Running);

    internal static OperationJournalState Create(string jobDirectory, OperationRequest request)
    {
        request = OperationRequestSnapshot.Freeze(request);
        _ = Encode(request, MaximumRequestBytes);
        CheckIdentity(jobDirectory, request);
        CheckDirectory(jobDirectory);
        var directory = DirectoryPath(jobDirectory);
        if (Directory.Exists(directory)) throw new InvalidDataException("This operation already has a journal; submit a new run ID.");
        ValidateReceipt(new(request, OperationState.Queued, DateTime.UtcNow, "Queued; no inputs sent.", []));
        var bytes = Encode(new OperationJournalAdmission(1, request));
        Directory.CreateDirectory(directory); CheckDirectory(directory);
        Publish(Path.Combine(directory, "admission.json"), bytes);
        return new(request, null, 0, Digest(bytes));
    }

    internal static OperationJournalState? Read(string jobDirectory)
    {
        CheckDirectory(jobDirectory);
        var directory = DirectoryPath(jobDirectory);
        if (File.Exists(directory)) throw new InvalidDataException("Operation journal must be a directory.");
        if (!Directory.Exists(directory)) return null;
        CheckDirectory(directory);
        var paths = Directory.EnumerateFileSystemEntries(directory).Take(MaximumDirectoryEntries + 1).ToArray();
        if (paths.Length > MaximumDirectoryEntries) throw new InvalidDataException("Operation journal exceeds its entry limit.");
        var committed = new SortedDictionary<int, string>();
        string? admission = null;
        foreach (var path in paths)
        {
            CheckFile(path);
            var name = Path.GetFileName(path);
            if (name.StartsWith(".pending-", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal) &&
                Guid.TryParseExact(name[9..^4], "N", out _)) continue;
            if (name == "admission.json") { admission = path; continue; }
            if (name.Length != 9 || !name.EndsWith(".json", StringComparison.Ordinal) ||
                !int.TryParse(name.AsSpan(0, 4), out var sequence) || sequence is < 1 or > MaximumSnapshots ||
                name != sequence.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".json" || !committed.TryAdd(sequence, path))
                throw new InvalidDataException("Unexpected operation journal entry.");
        }
        if (admission is null)
        {
            if (committed.Count != 0) throw new InvalidDataException("Operation snapshots have no admission.");
            return null; // Only incomplete staging exists; it is not evidence.
        }
        var admissionBytes = ReadBytes(admission);
        var admitted = Decode<OperationJournalAdmission>(admissionBytes);
        if (admitted.SchemaVersion != 1 || admitted.Request is null) throw new InvalidDataException("Unsupported operation admission.");
        var request = OperationRequestSnapshot.Freeze(admitted.Request);
        _ = Encode(request, MaximumRequestBytes);
        CheckIdentity(jobDirectory, request);
        var state = new OperationJournalState(request, null, 0, Digest(admissionBytes));
        foreach (var item in committed)
        {
            var bytes = ReadBytes(item.Value); var entry = Decode<OperationJournalEntry>(bytes);
            if (entry.SchemaVersion != 1 || entry.Sequence != item.Key || entry.Sequence != state.Sequence + 1 ||
                entry.PreviousSha256 != state.LastSha256 || entry.Receipt is null)
                throw new InvalidDataException("Operation journal sequence or hash link is inconsistent.");
            ValidateReceipt(entry.Receipt);
            if (!SameRequest(request, entry.Receipt.Request)) throw new InvalidDataException("Operation journal request changed.");
            CheckTransition(state.Latest?.State, entry.Receipt.State);
            state = new(request, entry.Receipt, entry.Sequence, Digest(bytes));
        }
        return state;
    }

    internal static OperationReceipt Append(string jobDirectory, OperationReceipt receipt)
    {
        ValidateReceipt(receipt);
        var state = Read(jobDirectory) ?? throw new InvalidDataException("Operation journal admission is missing.");
        if (!SameRequest(state.Request, receipt.Request)) throw new InvalidDataException("Operation journal request changed.");
        if (state.Latest is { } latest && Encode(latest).AsSpan().SequenceEqual(Encode(receipt))) return latest;
        CheckTransition(state.Latest?.State, receipt.State);
        if (state.Sequence >= MaximumSnapshots)
            throw new InvalidDataException("Operation journal exceeds its snapshot limit.");
        var entry = new OperationJournalEntry(1, state.Sequence + 1, state.LastSha256, receipt);
        var bytes = Encode(entry);
        Publish(SnapshotPath(jobDirectory, entry.Sequence), bytes);
        return Decode<OperationJournalEntry>(bytes).Receipt;
    }

    internal static void Project(string jobDirectory, OperationReceipt receipt)
    {
        CheckDirectory(jobDirectory); ValidateReceipt(receipt);
        var path = Path.Combine(jobDirectory, "receipt.json");
        var bytes = Encode(receipt);
        if (File.Exists(path))
        {
            CheckFile(path);
            if (new FileInfo(path).Length <= MaximumBytes && ReadBytes(path).AsSpan().SequenceEqual(bytes)) return;
        }
        GameExperiments.WriteAtomic(path, receipt);
    }

    internal static void ValidateReceipt(OperationReceipt receipt)
    {
        if (receipt is null || receipt.SchemaVersion != 1 || !Enum.IsDefined(receipt.State) ||
            receipt.UpdatedUtc == default || receipt.Message is null || receipt.Message.Length > MaximumMessageCharacters ||
            receipt.Artifacts is null || receipt.Artifacts.Length > 64 ||
            receipt.Artifacts.Any(path => string.IsNullOrWhiteSpace(path) || path.Length > 32768))
            throw new InvalidDataException("Operation outcome or receipt exceeds the supported contract.");
        OperationScheduler.Validate(receipt.Request);
        // Reserve headroom for the sequence/hash envelope and nested indentation.
        _ = Encode(receipt, MaximumBytes - 4096);
    }

    private static void CheckTransition(OperationState? previous, OperationState next)
    {
        if (!Enum.IsDefined(next) || previous is null && next is not (OperationState.Queued or OperationState.Interrupted) ||
            previous == OperationState.Queued && next is not (OperationState.Running or OperationState.Failed or OperationState.Cancelled or OperationState.TimedOut or OperationState.Interrupted) ||
            previous is { } state && (IsTerminal(state) || next == OperationState.Queued || state == OperationState.Running && next == OperationState.Running))
            throw new InvalidDataException("Conflicting or invalid operation lifecycle transition.");
    }

    private static bool SameRequest(OperationRequest left, OperationRequest right)
    {
        static byte[] Canonical(OperationRequest request) => Encode(request with
        {
            Pins = request.Pins.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        });
        return Canonical(left).AsSpan().SequenceEqual(Canonical(right));
    }

    private static void CheckIdentity(string jobDirectory, OperationRequest request)
    {
        if (!string.Equals(Path.GetFileName(Path.GetFullPath(jobDirectory)), request.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Operation admission does not match its job folder.");
    }
    private static void CheckDirectory(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
            throw new InvalidDataException("Operation journal directories cannot be links or files.");
    }
    private static void CheckFile(string path)
    {
        if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new InvalidDataException("Operation journal entries must be regular, non-linked files.");
    }
    private static byte[] ReadBytes(string path)
    {
        CheckFile(path);
        return GameExperiments.BoundedBytes(path, MaximumBytes);
    }
    private static T Decode<T>(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 16 });
            RoutineGraphs.CheckProperties(document.RootElement);
            return document.RootElement.Deserialize<T>(Json) ?? throw new InvalidDataException("Missing operation journal record.");
        }
        catch (JsonException error) { throw new InvalidDataException("Malformed operation journal record.", error); }
    }
    private static byte[] Encode<T>(T value, int limit = MaximumBytes)
    {
        using var output = new BoundedBuffer(limit);
        JsonSerializer.Serialize(output, value, Json);
        return output.ToArray();
    }
    private sealed class BoundedBuffer(int limit) : MemoryStream
    {
        private void Check(int count)
        {
            if (Position + count > limit) throw new InvalidDataException("Operation journal record exceeds its byte limit.");
        }
        public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
    }
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Publish(string path, byte[] bytes)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, ".pending-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes); output.Flush(flushToDisk: true); }
            try { File.Move(temporary, path, overwrite: false); }
            catch (IOException) when (File.Exists(path))
            {
                if (!ReadBytes(path).AsSpan().SequenceEqual(bytes)) throw new InvalidDataException("Immutable operation evidence differs.");
            }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
