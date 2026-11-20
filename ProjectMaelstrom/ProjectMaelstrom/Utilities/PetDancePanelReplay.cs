using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record DancePanelPacket(string RawFile, string Sha256, double[] TimesMs, string SourceVideoSha256);
internal static class PetDancePanelReplay
{
    internal static bool ValidateVideoSources(string? calibrationSource, string? observationSource, bool evaluateDifferentVideo)
    {
        static bool Digest(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
        if (!Digest(calibrationSource) || !Digest(observationSource)) throw new InvalidDataException("Missing video provenance.");
        var different = !string.Equals(calibrationSource, observationSource, StringComparison.OrdinalIgnoreCase);
        if (different && !evaluateDifferentVideo)
            throw new InvalidDataException("Use the explicit evaluation command for footage different from the calibration video.");
        return different;
    }

    internal static void ValidatePacket(DancePanelPacket packet)
    {
        static bool IsDigest(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
        if (packet.RawFile != "panels.rgb" || packet.TimesMs is not { Length: >= 1 and <= 3600 } ||
            !IsDigest(packet.Sha256) || !IsDigest(packet.SourceVideoSha256))
            throw new InvalidDataException("Invalid panel packet.");
        _ = VideoTimingSummary.Create(packet.TimesMs);
        for (var i = 1; i < packet.TimesMs.Length; i++)
            if (packet.TimesMs[i] <= packet.TimesMs[i - 1])
                throw new InvalidDataException("Panel timestamps must strictly increase, including skipped review frames.");
    }

    internal static Bitmap FromRgb(byte[] rgb, Size size)
    {
        var image = new Bitmap(size.Width, size.Height, PixelFormat.Format24bppRgb);
        var bits = image.LockBits(new Rectangle(Point.Empty, size), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            for (var i = 0; i < rgb.Length; i += 3) (rgb[i], rgb[i + 2]) = (rgb[i + 2], rgb[i]);
            for (var y = 0; y < size.Height; y++) Marshal.Copy(rgb, y * size.Width * 3, bits.Scan0 + y * bits.Stride, size.Width * 3);
        }
        finally { image.UnlockBits(bits); }
        return image;
    }
    public static async Task RunAsync(string profilePath, string packetPath, string output, CancellationToken token, bool evaluateDifferentVideo = false)
    {
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Choose a new replay folder.");
        var packetBytes = GameExperiments.BoundedBytes(packetPath, 262144);
        var packetDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(packetBytes));
        var packet = JsonSerializer.Deserialize<DancePanelPacket>(packetBytes) ?? throw new InvalidDataException("Missing panel packet.");
        ValidatePacket(packet);
        using var profile = JsonDocument.Parse(GameExperiments.BoundedBytes(profilePath, 65536));
        var calibrationSource = profile.RootElement.GetProperty("SourceVideoSha256").GetString();
        var separateRecording = ValidateVideoSources(calibrationSource, packet.SourceVideoSha256, evaluateDifferentVideo);
        var raw = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(packetPath))!, packet.RawFile);
        if ((File.GetAttributes(raw) & FileAttributes.ReparsePoint) != 0 || !GameExperiments.Digest(raw).Equals(packet.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Panel packet changed.");
        using var vision = new PetDancePanelVision(profilePath); var size = vision.PanelSize;
        if (new FileInfo(raw).Length != (long)size.Width * size.Height * 3 * packet.TimesMs.Length) throw new InvalidDataException("Panel count does not match the clock.");
        Directory.CreateDirectory(output);
        var sequence = new PetDanceSequence(3); var events = new List<object>(); var proposals = new List<object>();
        var outcomes = new List<object>(); var previousOutcome = DancePhase.Unknown;
        var reasons = new Dictionary<string, int>(); var compute = new List<double>(); var timer = Stopwatch.StartNew();
        using var stream = File.OpenRead(raw); var rgb = new byte[size.Width * size.Height * 3];
        for (var i = 0; i < packet.TimesMs.Length; i++)
        {
            token.ThrowIfCancellationRequested(); stream.ReadExactly(rgb);
            // Bound review cadence to 30 fps; keep the actual selected decoded timestamp.
            if (i % 2 != 0) continue;
            var watch = Stopwatch.StartNew();
            using var panel = FromRgb(rgb, size); var time = packet.TimesMs[i];
            var observed = vision.Read(panel, time);
            var decision = sequence.Observe(time, vision.CanonicalSize, observed.Phase, observed.Cue, observed.FadeOf, observed.Opacity);
            if (decision.Rejected) reasons[decision.Reason] = reasons.GetValueOrDefault(decision.Reason) + 1;
            if (decision.Proposals.Length > 0) proposals.Add(new { time_ms = time, actions = decision.Proposals, dispatched = false });
            if (observed.Phase == DancePhase.Watch) previousOutcome = DancePhase.Unknown;
            if (observed.Phase is DancePhase.Success or DancePhase.Failure && previousOutcome == DancePhase.Unknown)
            { outcomes.Add(new { time_ms = time, outcome = observed.Phase.ToString() }); previousOutcome = observed.Phase; }
            events.Add(new { time_ms = time, phase = observed.Phase.ToString(), cue = observed.Cue.ToString(), fade_of = observed.FadeOf.ToString(), observed.Opacity, observed.Error, observed.Margin, decision.Cues, decision.Rejected, decision.Reason });
            compute.Add(watch.Elapsed.TotalMilliseconds);
            await Task.Delay(Math.Clamp((int)Math.Ceiling(watch.Elapsed.TotalMilliseconds), 1, 50), token).ConfigureAwait(false);
        }
        vision.VerifyInputsUnchanged();
        if (!packetBytes.SequenceEqual(GameExperiments.BoundedBytes(packetPath, 262144))) throw new IOException("Panel packet changed during review.");
        GameExperiments.WriteAtomic(Path.Combine(output, "replay.json"), new
        {
            schema_version = 1,
            profile_sha256 = vision.Pins[Path.GetFullPath(profilePath)],
            packet_sha256 = packetDigest,
            source_video_sha256 = packet.SourceVideoSha256,
            calibration_source_video_sha256 = calibrationSource,
            evaluation_mode = evaluateDifferentVideo,
            separate_recording = separateRecording,
            evaluation_scope = separateRecording ? "Frozen calibration applied to a different recording; labels and independence require separate review." : "Calibration-video replay; not independent validation.",
            frames = events.Count,
            proposals,
            outcomes,
            rejection_reasons = reasons,
            mean_analysis_ms = compute.Average(),
            maximum_analysis_ms = compute.Max(),
            elapsed_ms = timer.Elapsed.TotalMilliseconds,
            events,
            inputs_sent = 0,
            live_qualified = false,
            timing_scope = "Decoded video clock, rate-converted recording. Original per-frame input alignment remains unverified."
        });
    }
}
