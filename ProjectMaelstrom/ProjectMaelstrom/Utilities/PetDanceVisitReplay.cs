using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceVisitReplayDispatch(string ProposalId, PetDanceLifecycleAction Action, bool Accepted, bool InputReleased);
internal sealed record PetDanceVisitReplayStep(string Kind, double TimeMs,
    PetDanceVisitObservation? Observation = null, PetDanceVisitReplayDispatch? Dispatch = null,
    PetDanceSpecialistReceipt? Specialist = null, PetDanceMenuText? Text = null,
    PetDanceEntryEvidence? EntryEvidence = null, string? ImageFile = null);
internal sealed record PetDanceVisitReplayFixture(int SchemaVersion, string SessionId, bool Synthetic,
    PetDanceVisitOptions Options, PetDanceVisitReplayStep[] Events, string? ClockOrigin = null);

/// <summary>Rehearses the visit adapter from explicit recorded OCR/annotations or
/// synthetic observations. Contains no capture/input path and never creates a
/// receipt for a proposed action. Recorded OCR is supplied evidence, not a fresh OCR run.</summary>
internal static class PetDanceVisitReplay
{
    internal static readonly JsonSerializerOptions Json = new(RoutineGraphs.Json)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static bool Run(string inputPath, string outputPath)
    {
        var raw = ReadBounded(inputPath, 1024 * 1024);
        using var document = JsonDocument.Parse(raw, new() { MaxDepth = 16 });
        RoutineGraphs.CheckProperties(document.RootElement);
        var fixture = JsonSerializer.Deserialize<PetDanceVisitReplayFixture>(raw, Json)
            ?? throw new InvalidDataException("Missing visit replay fixture.");
        if (fixture.SchemaVersion != 1 || string.IsNullOrWhiteSpace(fixture.SessionId) || fixture.SessionId.Length > 128 ||
            fixture.Options is null || fixture.Events is null || fixture.Events.Length is < 1 or > 2000)
            throw new InvalidDataException("Unsupported or unbounded visit replay fixture.");
        var player = new PetDanceVisit(fixture.Options, fixture.SessionId);
        var trace = new List<object>(); var images = new Dictionary<string, string>(StringComparer.Ordinal);
        var imageSizes = new Dictionary<string, Size>(StringComparer.Ordinal);
        var directory = Path.GetDirectoryName(Path.GetFullPath(inputPath))!;
        long imageBytes = 0; double lastTime = -1; var proposals = 0; var receipts = 0;
        foreach (var step in fixture.Events)
        {
            if (step is null || !double.IsFinite(step.TimeMs) || step.TimeMs < 0 || step.TimeMs < lastTime)
                throw new InvalidDataException("Visit events must have finite nondecreasing timestamps.");
            lastTime = step.TimeMs;
            switch (step.Kind)
            {
                case "observation":
                    if (step.Observation is null || step.Dispatch != null || step.Specialist != null)
                        throw new InvalidDataException("Observation needs exactly one frame payload.");
                    var observation = step.Observation with { TimeMs = step.TimeMs };
                    if (!fixture.Synthetic && step.ImageFile is null)
                        throw new InvalidDataException("Recorded observation needs its source image.");
                    if (step.ImageFile is { } filename)
                    {
                        var path = LocalImage(directory, filename);
                        if (!images.TryGetValue(filename, out var hash))
                        {
                            if (images.Count >= 64) throw new InvalidDataException("Replay supports at most 64 source images.");
                            var bytes = ReadBounded(path, 8 * 1024 * 1024);
                            imageBytes += bytes.Length;
                            if (imageBytes > 64 * 1024 * 1024) throw new InvalidDataException("Image evidence exceeds 64 MiB.");
                            imageSizes[filename] = PngSize(bytes);
                            images[filename] = hash = Digest(bytes);
                        }
                        if (!string.Equals(hash, observation.ImageSha256, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Image hash does not match its observation.");
                        if (imageSizes[filename] != observation.Geometry)
                            throw new InvalidDataException("Source PNG geometry disagrees with its observation.");
                    }
                    if (step.Text is { } text)
                    {
                        if (text.ImageSize != observation.Geometry) throw new InvalidDataException("OCR geometry disagrees with the frame.");
                        var entry = PetDanceEntryVision.Classify(text, observation.ImageSha256, step.TimeMs, step.EntryEvidence);
                        var post = PetDancePostGameClassifier.Classify(text);
                        var isPostgame = post.Scene is PetDanceScene.GameResult or PetDanceScene.SnackOffer or
                            PetDanceScene.OutsideAfterGame or PetDanceScene.MenuObserved or PetDanceScene.NativeIdle;
                        // A positively identified Dance sigil wins over the generic outside prompt.
                        var scene = entry.Scene != PetDanceScene.Unknown ? entry.Scene : isPostgame ? post.Scene : PetDanceScene.Unknown;
                        observation = observation with { Scene = scene, Entry = entry, ControlBounds = isPostgame ? post.ActionBounds : null };
                    }
                    else if (step.EntryEvidence is not null)
                        throw new InvalidDataException("Supplemental visual evidence requires the associated OCR frame.");
                    var plan = player.Observe(observation);
                    if (plan.IsProposal) proposals++;
                    trace.Add(new { kind = step.Kind, time_ms = step.TimeMs, scene = observation.Scene,
                        image_sha256 = observation.ImageSha256, supplied_ocr = step.Text != null, plan });
                    break;
                case "action_receipt":
                    if (step.Dispatch is not { } dispatch || step.Observation != null || step.Specialist != null ||
                        step.Text != null || step.EntryEvidence != null || step.ImageFile != null)
                        throw new InvalidDataException("Dispatch event needs exactly one receipt payload.");
                    player.RecordDispatch(dispatch.ProposalId, dispatch.Action, step.TimeMs, dispatch.Accepted, dispatch.InputReleased);
                    receipts++;
                    trace.Add(new { kind = step.Kind, time_ms = step.TimeMs, receipt = dispatch, status = player.Status });
                    break;
                case "specialist_receipt":
                    if (step.Specialist is not { } specialist || step.Observation != null || step.Dispatch != null ||
                        step.Text != null || step.EntryEvidence != null || step.ImageFile != null)
                        throw new InvalidDataException("Specialist event needs exactly one completion payload.");
                    player.RecordSpecialist(specialist with { CompletedMs = step.TimeMs });
                    receipts++;
                    trace.Add(new { kind = step.Kind, time_ms = step.TimeMs, receipt = specialist with { CompletedMs = step.TimeMs }, status = player.Status });
                    break;
                default:
                    throw new InvalidDataException("Unknown visit replay event.");
            }
        }
        var result = new
        {
            schema_version = 1, session_id = fixture.SessionId,
            clock_origin = fixture.ClockOrigin ?? "supplied-offline-monotonic-ms",
            evidence_mode = fixture.Synthetic ? "synthetic-visit-replay" : "recorded-annotations-and-ocr-replay",
            sha256_fixture = Digest(raw), images, desktop_dispatch_count = 0, live_qualified = false,
            sha256_graph = Digest(JsonSerializer.SerializeToUtf8Bytes(RoutineGraphs.BuiltIn("pet-dance-visit"), RoutineGraphs.Json)),
            ocr_evidence = "supplied-annotations-not-independent-recognition-validation",
            completed = player.Status.Completed, stopped = player.Status.Stopped, reason = player.Status.Reason,
            proposal_count = proposals, supplied_receipt_count = receipts, trace
        };
        var bytesOut = JsonSerializer.SerializeToUtf8Bytes(result, Json);
        if (bytesOut.Length > 8 * 1024 * 1024) throw new InvalidDataException("Replay report exceeds 8 MiB.");
        using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytesOut);
        return player.Status.Completed;
    }

    private static string LocalImage(string directory, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name) || name.Contains(':') ||
            name.Replace('\\', '/').Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("Images must be normalized paths inside the fixture directory.");
        var path = directory;
        foreach (var part in name.Replace('\\', '/').Split('/'))
        {
            path = Path.Combine(path, part);
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Replay image links are not supported.");
        }
        return path;
    }

    private static string Digest(byte[] raw) => Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
    private static Size PngSize(byte[] raw)
    {
        // Inspect only bounded PNG metadata, never decompress untrusted full-size
        // raster data just to bind a saved OCR trace to its source dimensions.
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (raw.Length < 33 || !raw.AsSpan(0, 8).SequenceEqual(signature) ||
            BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(8, 4)) != 13 ||
            !raw.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("Visit image evidence must be a PNG with bounded dimensions.");
        var width = BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(20, 4));
        if (width is 0 or > 8192 || height is 0 or > 8192 || (ulong)width * height > 16777216)
            throw new InvalidDataException("Source PNG geometry exceeds the replay limit.");
        return new((int)width, (int)height);
    }
    private static byte[] ReadBounded(string path, int limit)
    {
        using var input = File.OpenRead(path);
        if (input.Length > limit) throw new InvalidDataException("Replay input exceeds its byte limit.");
        using var output = new MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = input.Read(buffer)) > 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Replay input exceeds its byte limit.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}
