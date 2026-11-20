using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceReviewImage(string File, string Sha256);
internal sealed record PetDanceReviewReferences(PetDanceReviewImage Unselected, PetDanceReviewImage SelectedAway,
    PetDanceReviewImage PlayDisabled, PetDanceReviewImage PlayEnabled, PetDanceReviewImage? PlayEnabledHover = null);
internal sealed record PetDanceReviewFrame(PetDanceReviewImage Image, double TimeMs,
    bool ControlIdentityAnnotated = false, bool PointerAwayAnnotated = false, string? ExpectedState = null,
    PetDanceMenuText? Text = null, string? TextOrigin = null);
internal sealed record PetDanceEntryReviewFixture(int SchemaVersion, string SessionId, string ClockOrigin,
    string SourceVideoSha256, PetDanceReviewReferences References, PetDanceReviewFrame[] Frames);
internal sealed record PetDanceEntryImageResult(PetDanceEntryScene Entry, PetDanceLevelSelectionResult Selection,
    PetDancePlayButtonResult? Play);

/// <summary>Bounded saved-image qualification using the existing local menu OCR and
/// small visual matchers. Annotation of identity/pointer position is explicit;
/// this command neither detects a pointer nor acquires a game/input capability.</summary>
internal static class PetDanceEntryImageReview
{
    public static PetDanceEntryImageResult Analyze(Bitmap image, string hash, double timeMs, PetDanceMenuText text,
        bool controlIdentityAnnotated, bool pointerAwayAnnotated,
        PetDanceLevelSelectionVision selectionMatcher, PetDancePlayButtonVision playMatcher)
    {
        if (text.ImageSize != image.Size) throw new InvalidDataException("OCR geometry disagrees with decoded image.");
        var initial = PetDanceEntryVision.Classify(text, hash, timeMs);
        var instructions = initial.Scene == PetDanceScene.InstructionsUnselected;
        var play = initial.PlayBounds is { } bounds ? playMatcher.Classify(image, bounds) : null;
        var control = new PetDanceLevelControl(PetDanceArena.WizardCity,
            PetDanceLevelSelectionVision.QualifiedControlBounds, controlIdentityAnnotated);
        var selection = selectionMatcher.Classify(image, hash, timeMs,
            new(hash, timeMs, image.Size, control, instructions, pointerAwayAnnotated, play?.State == PetDancePlayState.Enabled));
        var evidence = new PetDanceEntryEvidence(hash, timeMs, image.Size,
            instructions && image.Size == PetDanceLevelSelectionVision.QualifiedGeometry ? [control] : [],
            selection.SelectionVerified ? selection.Arena : null,
            SelectionHighlightVerified: selection.SelectionVerified, PlayEnabledVerified: play?.State == PetDancePlayState.Enabled);
        return new(PetDanceEntryVision.Classify(text, hash, timeMs, evidence), selection, play);
    }

    public static async Task<bool> RunAsync(string inputPath, string outputPath)
    {
        // One bounded reader for the whole review; no startup services or window handle.
        PetDanceMenuOcr? ocr = null;
        return await RunAsync(inputPath, outputPath,
            (image, token) => (ocr ??= new PetDanceMenuOcr()).ReadAsync(image, token), "local-windows-ocr");
    }

    internal static async Task<bool> RunAsync(string inputPath, string outputPath,
        Func<Bitmap, CancellationToken, Task<PetDanceMenuText>> readText, string readerOrigin = "supplied-test-reader")
    {
        if (File.Exists(outputPath)) throw new IOException("Choose a new entry review report.");
        var raw = ReadBounded(inputPath, 65536);
        using var document = JsonDocument.Parse(raw, new() { MaxDepth = 16 });
        RoutineGraphs.CheckProperties(document.RootElement);
        var fixture = JsonSerializer.Deserialize<PetDanceEntryReviewFixture>(raw, PetDanceVisitReplay.Json)
            ?? throw new InvalidDataException("Missing entry review manifest.");
        if (fixture.SchemaVersion != 1 || string.IsNullOrWhiteSpace(fixture.SessionId) || fixture.SessionId.Length > 128 ||
            string.IsNullOrWhiteSpace(fixture.ClockOrigin) || fixture.ClockOrigin.Length > 256 ||
            !PetDanceEntryVision.IsImageHash(fixture.SourceVideoSha256) || fixture.References is null ||
            fixture.Frames is null || fixture.Frames.Length is < 1 or > 16)
            throw new InvalidDataException("Bound one source recording, clock provenance and 1–16 frames.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(inputPath))!;
        var snapshots = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        long bytesRead = 0;
        byte[] Snapshot(PetDanceReviewImage reference)
        {
            if (reference is null || !PetDanceEntryVision.IsImageHash(reference.Sha256))
                throw new InvalidDataException("Each image needs an explicit SHA-256.");
            var path = LocalPath(directory, reference.File);
            if (!snapshots.TryGetValue(path, out var bytes))
            {
                if (snapshots.Count >= 21) throw new InvalidDataException("Too many image sources.");
                bytes = ReadBounded(path, 8 * 1024 * 1024);
                bytesRead += bytes.Length;
                if (bytesRead > 64 * 1024 * 1024) throw new InvalidDataException("Image bytes exceed 64 MiB.");
                snapshots[path] = bytes;
            }
            if (!Digest(bytes).Equals(reference.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Image hash mismatch.");
            return bytes;
        }
        using var unselected = Decode(Snapshot(fixture.References.Unselected));
        using var selected = Decode(Snapshot(fixture.References.SelectedAway));
        using var disabled = Decode(Snapshot(fixture.References.PlayDisabled));
        using var enabled = Decode(Snapshot(fixture.References.PlayEnabled));
        using var hover = fixture.References.PlayEnabledHover is { } hoverReference ? Decode(Snapshot(hoverReference)) : null;
        var selectionMatcher = new PetDanceLevelSelectionVision(unselected, selected);
        var playMatcher = new PetDancePlayButtonVision(disabled, enabled, hover);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var reviews = new List<object>(); var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var passed = true; double previous = -1;
        foreach (var frame in fixture.Frames)
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (frame is null || !double.IsFinite(frame.TimeMs) || frame.TimeMs < 0 || frame.TimeMs <= previous ||
                (frame.ExpectedState != null && !Enum.GetNames<PetDanceLevelSelectionState>().Contains(frame.ExpectedState)))
                throw new InvalidDataException("Frames need increasing finite source times and valid expected states.");
            if ((frame.Text is null && frame.TextOrigin is not null) ||
                (frame.Text is not null && frame.TextOrigin is not ("manual-annotation" or "saved-ocr")))
                throw new InvalidDataException("Supplied text needs its explicit annotation or saved-OCR origin.");
            previous = frame.TimeMs;
            var bytes = Snapshot(frame.Image);
            var read = frame.Text is { } suppliedText
                ? (Image: Decode(bytes), Text: suppliedText)
                : await ReadSnapshotAsync(bytes, readText, deadline.Token);
            using var image = read.Image;
            var text = read.Text;
            var result = Analyze(image, Digest(bytes), frame.TimeMs, text,
                frame.ControlIdentityAnnotated, frame.PointerAwayAnnotated, selectionMatcher, playMatcher);
            var expectationMet = frame.ExpectedState is null || result.Selection.State.ToString() == frame.ExpectedState;
            passed &= expectationMet;
            reviews.Add(new { source = frame.Image.File, sha256 = Digest(bytes), time_ms = frame.TimeMs,
                repeated_image = !unique.Add(Digest(bytes)), frame.ControlIdentityAnnotated, frame.PointerAwayAnnotated,
                expected_state = frame.ExpectedState, expectation_met = expectationMet,
                text_origin = frame.TextOrigin ?? readerOrigin, text, result });
        }
        var report = new
        {
            schema_version = 1, session_id = fixture.SessionId, fixture.ClockOrigin, fixture.SourceVideoSha256,
            manifest_sha256 = Digest(raw), references = fixture.References, evidence_mode = "same-recording-calibration-only",
            source_video_digest_verified = false, source_provenance = "caller-declared-recording; encoded-PNG-digests-verified",
            ocr_origin = fixture.Frames.All(frame => frame.Text != null) ? "supplied-text-see-per-frame-origin" :
                fixture.Frames.Any(frame => frame.Text != null) ? "mixed-see-per-frame-origin" : readerOrigin,
            pointer_evidence = "supplied-per-frame-annotation-not-pointer-detection",
            clock_alignment_verified = false, independent_session_holdout = false,
            desktop_dispatch_count = 0, live_qualified = false, runtime_enabled = false,
            expectations_met = passed, unique_image_count = unique.Count, bytes_read = bytesRead, reviews
        };
        var output = JsonSerializer.SerializeToUtf8Bytes(report, PetDanceVisitReplay.Json);
        if (output.Length > 2 * 1024 * 1024) throw new InvalidDataException("Entry report exceeds 2 MiB.");
        using var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(output, deadline.Token);
        return passed;
    }

    internal static async Task<(Bitmap Image, PetDanceMenuText Text)> ReadSnapshotAsync(byte[] bytes,
        Func<Bitmap, CancellationToken, Task<PetDanceMenuText>> readText, CancellationToken token)
    {
        var image = Decode(bytes);
        Task<PetDanceMenuText>? task = null;
        try
        {
            // Also bound synchronous reader initialization (such as a stalled OCR
            // engine). Only one reader task can be outstanding in this review.
            task = Task.Run(() => readText(image, token), token);
            return (image, await task.WaitAsync(token));
        }
        catch
        {
            if (task is { IsCompleted: false })
            {
                // The deadline must return promptly even if a reader ignores
                // cancellation. Retain its single bitmap until it actually exits.
                _ = task.ContinueWith(finished => { _ = finished.Exception; image.Dispose(); },
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            else { _ = task?.Exception; image.Dispose(); }
            throw;
        }
    }

    internal static Bitmap Decode(byte[] bytes)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length is < 33 or > 8388608 || !bytes.AsSpan(0, 8).SequenceEqual(signature) ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) != 13 || !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("Only bounded PNG evidence is accepted.");
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        if (width is 0 or > 8192 || height is 0 or > 8192 || (ulong)width * height > 4194304)
            throw new InvalidDataException("PNG geometry exceeds the review limit.");
        using var stream = new MemoryStream(bytes, writable: false);
        using var decoded = new Bitmap(stream);
        if (decoded.Width != width || decoded.Height != height) throw new InvalidDataException("PNG geometry is inconsistent.");
        return decoded.Clone(new Rectangle(Point.Empty, decoded.Size), decoded.PixelFormat);
    }

    private static string LocalPath(string directory, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name) || name.Contains(':') || name.Contains('\\') ||
            name.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("Use contained normalized image paths.");
        var path = directory;
        foreach (var part in name.Split('/'))
        {
            path = Path.Combine(path, part);
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Image links are unsupported.");
        }
        return path;
    }

    private static byte[] ReadBounded(string path, int limit)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > limit) throw new InvalidDataException("Review input exceeds its byte limit.");
        using var output = new MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Review input exceeds its byte limit.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
