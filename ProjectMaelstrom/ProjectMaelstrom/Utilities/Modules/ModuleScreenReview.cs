using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record ModuleScreenManifest(int SchemaVersion, string SessionId, string ClockOrigin, string EvidenceMode,
    string BundleFile, string BundleSha256, string EventsFile, RoutineBinding RoutineBinding, Size Geometry);
internal sealed record ModuleReviewImage(string File, string Sha256, double CapturedMs);
internal sealed record ModuleReviewEvent(string EventId, int Sequence, double TimeMs, string Kind,
    ModuleReviewImage? Image, RoutineReceipt? Receipt);
internal sealed record ModuleScreenReviewResult(bool Completed, bool Stopped, string Reason,
    int Proposals, int Receipts, int Frames, string Report);

/// <summary>Immutable bounded source snapshot. Scheduler pins detect later edits;
/// execution reads only these frozen bytes, including all image dependencies.</summary>
internal sealed class ModuleScreenReviewSnapshot
{
    private readonly byte[] _bundle;
    private readonly Dictionary<string, byte[]> _images;
    internal ModuleScreenManifest Manifest { get; }
    internal IReadOnlyList<ModuleReviewEvent> Events { get; }
    public string SessionId => Manifest.SessionId;
    public IReadOnlyDictionary<string, string> Pins { get; }
    internal ModuleScreenReviewSnapshot(ModuleScreenManifest manifest, byte[] bundle, ModuleReviewEvent[] events,
        Dictionary<string, byte[]> images, Dictionary<string, string> pins)
    {
        Manifest = manifest; _bundle = bundle.ToArray(); Events = Array.AsReadOnly(events.ToArray());
        _images = images.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.Ordinal);
        Pins = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(pins, StringComparer.OrdinalIgnoreCase));
    }
    internal OperationModuleBundle Bundle() => ModuleCatalog.ReadBundle(Encoding.UTF8.GetString(_bundle));
    internal byte[] Image(string filename) => _images[filename].ToArray();
    public static ModuleScreenReviewSnapshot Load(string manifestPath) => ModuleScreenReview.Load(manifestPath);
}

/// <summary>Actual saved PNG -> existing local OCR -> module session -> supplied
/// rehearsal receipts. No desktop transport exists in this reader. The scheduler
/// calls the same entry point and preserves its evidence in ordinary run history.</summary>
internal static class ModuleScreenReview
{
    internal static ModuleScreenReviewSnapshot Load(string manifestPath)
    {
        var path = Path.GetFullPath(manifestPath); var directory = Path.GetDirectoryName(path)!;
        var pins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        byte[] Read(string file, int limit)
        {
            var bytes = RoutineReplay.ReadBounded(file, limit); pins[file] = RoutineReplay.Digest(bytes); return bytes;
        }
        var manifestBytes = Read(path, 65536);
        using var document = RoutineReplay.Parse(manifestBytes);
        var manifest = document.RootElement.Deserialize<ModuleScreenManifest>(OperationModules.Json)
            ?? throw new InvalidDataException("Missing saved screen session.");
        OperationModules.CheckId(manifest.SessionId);
        if (manifest.SchemaVersion != 1 || manifest.EvidenceMode != "saved-images-synthetic-rehearsal" ||
            string.IsNullOrWhiteSpace(manifest.ClockOrigin) || manifest.ClockOrigin.Length > 256 ||
            !ModuleRecognition.ValidGeometry(manifest.Geometry) || (long)manifest.Geometry.Width * manifest.Geometry.Height > 4194304)
            throw new InvalidDataException("Choose a bounded saved-image rehearsal with explicit clock provenance.");
        var bundleBytes = Read(LocalPath(directory, manifest.BundleFile), ModuleCatalog.MaximumBundleCharacters);
        if (!RoutineReplay.Digest(bundleBytes).Equals(manifest.BundleSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Module bundle hash mismatch.");
        var bundle = ModuleCatalog.ReadBundle(Encoding.UTF8.GetString(bundleBytes));
        _ = new ModuleScreenSession(bundle, manifest.RoutineBinding, manifest.Geometry, W101ModulePrograms.Policy(), manifest.SessionId);
        var eventBytes = Read(LocalPath(directory, manifest.EventsFile), 2 * 1024 * 1024);
        var events = new List<ModuleReviewEvent>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long totalBytes = 0; double lastTime = -1; var lastSequence = -1; var frames = 0;
        using var lines = new StringReader(Encoding.UTF8.GetString(eventBytes));
        while (lines.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Length > 65536 || events.Count >= 128) throw new InvalidDataException("Limit the review to 128 bounded events.");
            using var itemDocument = RoutineReplay.Parse(Encoding.UTF8.GetBytes(line)); var item = itemDocument.RootElement;
            var id = item.GetProperty("event_id").GetString()!; OperationModules.CheckId(id);
            var sequence = item.GetProperty("sequence").GetInt32(); var time = item.GetProperty("time_ms").GetDouble();
            var kind = item.GetProperty("kind").GetString()!; var payload = item.GetProperty("payload");
            if (!ids.Add(id) || sequence <= lastSequence || !double.IsFinite(time) || time < 0 || time < lastTime ||
                kind is not ("observation" or "outcome" or "action_receipt" or "marker" or "action_proposal") || payload.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Invalid or repeated screen-session event.");
            lastSequence = sequence; lastTime = time;
            ModuleReviewImage? image = null; RoutineReceipt? receipt = null;
            if (kind is "observation" or "outcome")
            {
                image = payload.GetProperty("module_image").Deserialize<ModuleReviewImage>(OperationModules.Json)
                    ?? throw new InvalidDataException("Screen observations require a pinned image.");
                if (++frames > 64 || !double.IsFinite(image.CapturedMs) || image.CapturedMs < 0 ||
                    !PetDanceEntryVision.IsImageHash(image.Sha256)) throw new InvalidDataException("Invalid bounded image observation.");
                if (!images.TryGetValue(image.File, out var bytes))
                {
                    if (images.Count >= 16) throw new InvalidDataException("Review at most sixteen unique screenshots.");
                    bytes = Read(LocalPath(directory, image.File), 8 * 1024 * 1024); totalBytes += bytes.Length;
                    if (totalBytes > 64 * 1024 * 1024) throw new InvalidDataException("Screen bytes exceed 64 MiB.");
                    using var decoded = PetDanceEntryImageReview.Decode(bytes);
                    if (decoded.Size != manifest.Geometry) throw new InvalidDataException("Image geometry changed within the bound review.");
                    images[image.File] = bytes;
                }
                if (!RoutineReplay.Digest(bytes).Equals(image.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Screen image hash mismatch.");
            }
            else if (kind == "action_receipt")
                receipt = payload.GetProperty("routine_receipt").Deserialize<RoutineReceipt>(OperationModules.Json)
                    ?? throw new InvalidDataException("Missing supplied rehearsal receipt.");
            events.Add(new(id, sequence, time, kind, image, receipt));
        }
        if (events.Count == 0 || frames == 0 || pins.Count > 32) throw new InvalidDataException("A review needs bounded image evidence and source pins.");
        return new(manifest, bundleBytes, events.ToArray(), images, pins);
    }

    public static Task<ModuleScreenReviewResult> RunAsync(ModuleScreenReviewSnapshot snapshot, string reportPath, CancellationToken token) =>
        RunAsync(snapshot, reportPath, token, W101MenuRecognition.ReadPngAsync);

    internal static async Task<ModuleScreenReviewResult> RunAsync(ModuleScreenReviewSnapshot snapshot, string reportPath, CancellationToken token,
        Func<byte[], ModuleVisualFrame, CancellationToken, Task<ModuleVisualRecognition>> recognize)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentNullException.ThrowIfNull(recognize);
        if (File.Exists(reportPath)) throw new IOException("Choose a new module screen report.");
        var manifest = snapshot.Manifest;
        var session = new ModuleScreenSession(snapshot.Bundle(), manifest.RoutineBinding, manifest.Geometry, W101ModulePrograms.Policy(), manifest.SessionId);
        var trace = new List<object>(); var frames = 0; var receipts = 0; var cancelled = false; string? error = null;
        try
        {
            foreach (var item in snapshot.Events)
            {
                token.ThrowIfCancellationRequested();
                if (item.Image is { } source)
                {
                    var visualFrame = new ModuleVisualFrame(source.Sha256, source.CapturedMs, manifest.Geometry, manifest.RoutineBinding, "local-windows-ocr");
                    var recognition = await recognize(snapshot.Image(source.File), visualFrame, token).ConfigureAwait(false);
                    var decision = session.Observe(recognition, item.TimeMs,
                        new(true, true, true, VerifiedFacts: W101ModulePrograms.Policy().ContextFacts)); frames++;
                    trace.Add(new { event_id = item.EventId, time_ms = item.TimeMs, source_image = source.File,
                        source_sha256 = source.Sha256, recognition, decision, completed_modules = session.CompletedModules });
                }
                else if (item.Receipt is { } supplied)
                {
                    var receipt = supplied with { TimeMs = item.TimeMs };
                    session.RecordReceipt(receipt); receipts++;
                    trace.Add(new { event_id = item.EventId, time_ms = item.TimeMs, receipt,
                        receipt_origin = "supplied-synthetic-rehearsal", decision = session.Status });
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { cancelled = true; }
        catch (Exception ex) { error = ex.Message; }
        var status = session.Status;
        var completed = status.Completed && !cancelled && error is null;
        var reason = cancelled ? "screen-review-cancelled" : error is not null ? "screen-review-error: " + error : status.Reason;
        var result = new ModuleScreenReviewResult(completed, status.Stopped || cancelled || error is not null, reason,
            session.Proposals, receipts, frames, Path.GetFullPath(reportPath));
        var report = new
        {
            schema_version = 1, session_id = snapshot.SessionId, evidence_mode = manifest.EvidenceMode, manifest.ClockOrigin,
            completed, stopped = result.Stopped, reason, completed_modules = session.CompletedModules,
            proposal_count = session.Proposals, receipt_count = receipts, frame_count = frames,
            desktop_dispatch_count = 0, live_qualified = false, input_lease_acquired = false,
            receipt_origin = "supplied-synthetic-rehearsal", actual_input_alignment_verified = false,
            context_facts_origin = "explicit-rehearsal-assumptions-not-gameplay-or-ownership-proof",
            timing_qualification = "synthetic-review-clock-not-original-capture-latency", source_pins = snapshot.Pins, trace
        };
        using (var output = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            JsonSerializer.Serialize(output, report, OperationModules.Json);
        if (cancelled) throw new OperationCanceledException(token);
        return result;
    }

    private static string LocalPath(string directory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':') || relative.Contains('\\') ||
            relative.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new InvalidDataException("Use contained, normalized screen-review files.");
        var path = directory;
        foreach (var segment in relative.Split('/'))
        {
            path = Path.Combine(path, segment);
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Screen-review links are unsupported.");
        }
        return path;
    }
}
