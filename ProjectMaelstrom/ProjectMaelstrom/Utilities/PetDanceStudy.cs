using System.Diagnostics;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProjectMaelstrom.Utilities;

internal sealed record DanceStudyResult(string Pack, string Report, int Frames, int Proposals, int RejectedFrames, double AnalysisMs);

/// <summary>Validated, calibrated teacher perception. Unknown pixels never imply a neutral gap.</summary>
internal sealed class PetDanceVision : IDisposable
{
    private readonly PetDanceMatcher _matcher;
    private readonly DanceCalibration _calibration;
    private readonly byte[] _neutral;
    public PetDanceVision(string packPath)
    {
        var path = Path.Combine(Path.GetDirectoryName(packPath)!, "pet-dance-calibration.json");
        _calibration = JsonSerializer.Deserialize<DanceCalibration>(GameExperiments.BoundedBytes(path, 524288)) ?? throw new InvalidDataException("Missing Pet Dance calibration.");
        Validate(_calibration);
        var bytes = Convert.FromBase64String(_calibration.NeutralPng);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(_calibration.NeutralSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Neutral reference changed.");
        using var neutral = PetDanceAssets.Decode(bytes);
        if (neutral.Size != Region(_calibration).Size) throw new InvalidDataException("Neutral reference size differs from calibration.");
        _neutral = VisualStateRecognizer.Features(neutral);
        _matcher = new(PetDanceAssets.Read(packPath), _calibration.Scale);
        if (_matcher.Match(neutral).Cue != DanceCue.Unknown) { Dispose(); throw new InvalidDataException("Neutral reference contains an arrow."); }
    }
    public static Rectangle Region(DanceCalibration calibration) => new(calibration.Region[0], calibration.Region[1], calibration.Region[2], calibration.Region[3]);
    internal static void Validate(DanceCalibration calibration)
    {
        WindowObserver.CheckSize(calibration.Width, calibration.Height);
        var r = calibration.Region;
        if (calibration.Version != 1 || r == null || r.Length != 4 || r[0] < 0 || r[1] < 0 || r[2] is < 20 or > 256 || r[3] is < 20 or > 256 ||
            (long)r[0] + r[2] > calibration.Width || (long)r[1] + r[3] > calibration.Height || !double.IsFinite(calibration.Scale) || calibration.Scale is < .5 or > 1.5)
            throw new InvalidDataException("Invalid Pet Dance calibration. Use a 20–256 pixel cue region inside the captured window.");
    }
    public DanceMatch Read(Bitmap frame)
    {
        if (frame.Width != _calibration.Width || frame.Height != _calibration.Height) return new(DanceCue.Unknown, 0, 0);
        using var crop = frame.Clone(Region(_calibration), PixelFormat.Format24bppRgb);
        var match = _matcher.Match(crop);
        if (match.Cue != DanceCue.Unknown) return match;
        var similarity = VisualStateRecognizer.Similarity(_neutral, VisualStateRecognizer.Features(crop));
        // A weak arrow match alone is never evidence of an empty cue area.
        return similarity >= .985 && match.Score < .94 ? new(DanceCue.Neutral, similarity, 0) : match;
    }
    public void Dispose() => _matcher.Dispose();

    public static DanceCalibration Calibrate(string packPath, TeachingRecording recording, int cueIndex, int neutralIndex,
        Rectangle region, DanceCue known, CancellationToken token)
    {
        using var work = PetDanceWork.Acquire();
        token.ThrowIfCancellationRequested();
        if (GameplaySamplePlan.Load(recording.DirectoryPath)?.Purpose == "check") throw new InvalidOperationException("Keep check recordings separate; calibrate with a teaching session.");
        using var cue = recording.ReadFrame(cueIndex); using var neutral = recording.ReadFrame(neutralIndex);
        var validation = new DanceCalibration(1, cue.Width, cue.Height, [region.X, region.Y, region.Width, region.Height], 1, "", "", "");
        Validate(validation);
        if (cue.Size != neutral.Size || cueIndex == neutralIndex) throw new InvalidDataException("Choose distinct arrow and empty-area frames at the same geometry.");
        using var cueCrop = cue.Clone(region, PixelFormat.Format24bppRgb); using var neutralCrop = neutral.Clone(region, PixelFormat.Format24bppRgb);
        var assets = PetDanceAssets.Read(packPath);
        var scale = PetDanceMatcher.Calibrate(assets, cueCrop, known, token);
        using var matcher = new PetDanceMatcher(assets, scale);
        if (matcher.Match(neutralCrop).Score >= .94 || VisualStateRecognizer.Similarity(VisualStateRecognizer.Features(cueCrop), VisualStateRecognizer.Features(neutralCrop)) >= .985)
            throw new InvalidDataException("The empty-area reference is too close to an arrow. Select a clear gap frame.");
        using var png = new MemoryStream(); neutralCrop.Save(png, ImageFormat.Png); var bytes = png.ToArray();
        var result = validation with { Scale = scale, NeutralPng = Convert.ToBase64String(bytes), NeutralSha256 = Convert.ToHexString(SHA256.HashData(bytes)), SourceFrameSha256 = recording.Frames[neutralIndex].Sha256 };
        token.ThrowIfCancellationRequested();
        GameExperiments.WriteAtomic(Path.Combine(Path.GetDirectoryName(packPath)!, "pet-dance-calibration.json"), result);
        return result;
    }
}

internal static class PetDanceStudy
{
    /// <summary>One sequential worker; exports proposals to a new draft without changing the recording's human labels.</summary>
    public static async Task<DanceStudyResult> RunAsync(string packPath, TeachingRecording recording, VisualStateRecognizer? phases,
        int expected, string dataRoot, IProgress<string>? progress, CancellationToken token)
    {
        using var work = PetDanceWork.Acquire();
        token.ThrowIfCancellationRequested();
        if (!recording.Complete || recording.Frames.Count == 0) throw new InvalidOperationException("Choose a completed recording with frames.");
        if (GameplaySamplePlan.Load(recording.DirectoryPath)?.Purpose == "check") throw new InvalidOperationException("This creates teacher examples. Preserve check recordings for independent evaluation.");
        var manifestPath = Path.Combine(recording.DirectoryPath, "recording.json");
        var sourceBytes = GameExperiments.BoundedBytes(manifestPath, 1048576);
        recording = TeachingRecording.Open(manifestPath);
        var packBytes = GameExperiments.BoundedBytes(packPath, 1048576);
        var calibrationPath = Path.Combine(Path.GetDirectoryName(packPath)!, "pet-dance-calibration.json");
        var calibrationBytes = GameExperiments.BoundedBytes(calibrationPath, 524288);
        using var vision = new PetDanceVision(packPath);
        var sequence = new PetDanceSequence(expected);
        var frames = recording.Frames;
        var events = new List<object>(); int proposals = 0, rejected = 0, recognized = 0, unknownPhases = 0, unknownCues = 0; double analysisMs = 0;
        var latencies = new List<double>(); var reasons = new Dictionary<string, int>();
        for (var index = 0; index < frames.Count; index++)
        {
            token.ThrowIfCancellationRequested(); var timer = Stopwatch.StartNew();
            using var image = recording.ReadFrame(index); var frame = frames[index];
            var prediction = phases?.Predict(image);
            var phase = PetDanceSequence.ParsePhase(prediction?.State ?? (phases == null ? frame.State : null));
            var match = vision.Read(image);
            var decision = sequence.Observe(frame.TimeMs, image.Size, phase, match.Cue);
            if (phase == DancePhase.Unknown) unknownPhases++;
            if (match.Cue == DanceCue.Unknown) unknownCues++;
            if (match.Cue is not (DanceCue.Unknown or DanceCue.Neutral)) recognized++;
            if (decision.Rejected) rejected++;
            if (decision.Rejected) reasons[decision.Reason] = reasons.GetValueOrDefault(decision.Reason) + 1;
            events.Add(new
            {
                event_id = "frame-" + index,
                kind = "observation",
                sequence = events.Count,
                time_ms = frame.TimeMs,
                payload = new { state = "dance-" + phase.ToString().ToLowerInvariant(), cue = match.Cue.ToString().ToLowerInvariant(), match.Score, match.Margin, phase_score = prediction?.Similarity, phase_margin = prediction?.Margin, phase_source = phases == null ? "recording_manual_label" : "visual_phase_prediction", label_source = "legacy_teacher_proposal", frame = frame.File, frame_sha256 = frame.Sha256, decision.Cues, decision.Rejected, decision.Reason }
            });
            if (decision.Proposals.Length > 0)
            {
                proposals++;
                events.Add(new { event_id = "proposal-" + index, kind = "action_proposal", sequence = events.Count, time_ms = frame.TimeMs, payload = new { action_ids = decision.Proposals, label_source = "legacy_teacher_proposal", dispatched = false, reason = decision.Reason } });
            }
            analysisMs += timer.Elapsed.TotalMilliseconds;
            latencies.Add(timer.Elapsed.TotalMilliseconds);
            if (index % 20 == 0) progress?.Report($"Reviewed {index + 1}/{frames.Count} frames · {proposals} sequence proposals · {rejected} rejected frames");
            // Bound sustained duty cycle and memory. Do not queue tasks for individual frames.
            await Task.Delay(Math.Clamp((int)timer.ElapsedMilliseconds, 20, 500), token).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
        foreach (var pair in new[] { (manifestPath, sourceBytes), (packPath, packBytes), (calibrationPath, calibrationBytes) })
            if (!GameExperiments.BoundedBytes(pair.Item1, 1048576).SequenceEqual(pair.Item2)) throw new IOException("Teaching inputs changed during review; rerun with a stable recording and pack.");
        var assets = PetDanceAssets.Read(packPath);
        var id = "pet-dance-" + Guid.NewGuid().ToString("N")[..12];
        var destination = Path.Combine(dataRoot, "game-packs", id); Directory.CreateDirectory(destination);
        for (var index = 0; index < assets.Length; index++)
        {
            var path = Path.Combine(destination, PetDanceAssets.Reviewed[index].File); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, assets[index]);
        }
        File.WriteAllBytes(Path.Combine(destination, "pet-dance-calibration.json"), calibrationBytes);
        var session = Path.Combine(destination, "teacher"); Directory.CreateDirectory(session);
        File.WriteAllLines(Path.Combine(session, "events.jsonl"), events.Select(e => JsonSerializer.Serialize(e)));
        GameExperiments.WriteAtomic(Path.Combine(session, "session.json"), new { schema_version = 1, session_id = id, clock_origin = "source-recording-relative-ms", events_file = "events.jsonl", source_recording = manifestPath, source_recording_sha256 = Hash(sourceBytes), source = "legacy_teacher_proposals", inputs_sent = 0 });
        var reportPath = Path.Combine(destination, "teacher-report.json");
        GameExperiments.WriteAtomic(reportPath, new
        {
            schema_version = 1,
            teacher = "pet-dance-visual-v1",
            frames = frames.Count,
            recognized_arrow_frames = recognized,
            sequence_proposals = proposals,
            rejected_frames = rejected,
            analysis_ms = analysisMs,
            analysis_ms_per_frame = analysisMs / frames.Count,
            unknown_phase_frames = unknownPhases,
            unknown_cue_frames = unknownCues,
            analysis_p95_ms = latencies.Order().ElementAt((int)Math.Ceiling(latencies.Count * .95) - 1),
            analysis_max_ms = latencies.Max(),
            rejection_reasons = reasons,
            source_pack_sha256 = Hash(packBytes),
            calibration_sha256 = Hash(calibrationBytes),
            phase_pack_sha256 = phases?.PackHash,
            expected_first_round_cues = expected,
            source_recording_sha256 = Hash(sourceBytes),
            inputs_sent = 0,
            live_qualified = false,
            note = "Teacher proposals, not executed actions, observed rewards or human labels. Timing gaps invalidate rounds. No win-rate claim. Original recording is required to inspect image evidence."
        });
        var pack = JsonNode.Parse(packBytes)!.AsObject();
        pack["version"] = "0.2.0-teacher-" + id; pack["fixtures"] = new JsonArray("teacher/session.json");
        pack["teacher_study"] = JsonSerializer.SerializeToNode(new { report = "teacher-report.json", calibration = "pet-dance-calibration.json", label_source = "legacy_teacher_proposal", live_qualified = false });
        var resultPack = Path.Combine(destination, "pack.json"); File.WriteAllText(resultPack, pack.ToJsonString(GameExperiments.Json));
        return new(resultPack, reportPath, frames.Count, proposals, rejected, analysisMs);
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
