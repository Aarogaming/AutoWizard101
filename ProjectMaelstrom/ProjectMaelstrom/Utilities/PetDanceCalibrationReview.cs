using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record DanceCalibrationRequest(int CueIndex, int NeutralIndex, int[] Region, string KnownCue);

/// <summary>Reproducible offline calibration using the same matcher as the teacher UI.
/// Creates a separate draft; never edits the source recording or sends game inputs.</summary>
internal static class PetDanceCalibrationReview
{
    internal static DanceCue Validate(DanceCalibrationRequest request, IReadOnlyList<TeachingFrame> frames)
    {
        if (request.CueIndex < 0 || request.CueIndex >= frames.Count || request.NeutralIndex < 0 ||
            request.NeutralIndex >= frames.Count || request.CueIndex == request.NeutralIndex ||
            !Enum.GetNames<DanceCue>().Contains(request.KnownCue, StringComparer.OrdinalIgnoreCase) ||
            !Enum.TryParse<DanceCue>(request.KnownCue, true, out var known) || known is not (DanceCue.Up or DanceCue.Down or DanceCue.Left or DanceCue.Right))
            throw new InvalidDataException("Choose distinct existing arrow and neutral frames and a named direction.");
        var frame = frames[request.CueIndex];
        PetDanceVision.Validate(new(1, frame.Width, frame.Height, request.Region, 1, "", "", ""));
        return known;
    }

    public static async Task RunAsync(string archive, string manifest, string requestPath, string output, CancellationToken token)
    {
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Choose a new calibration output folder.");
        var source = GameExperiments.BoundedBytes(manifest, 1048576);
        var requestBytes = GameExperiments.BoundedBytes(requestPath, 16384);
        var request = JsonSerializer.Deserialize<DanceCalibrationRequest>(requestBytes) ?? throw new InvalidDataException("Missing calibration request.");
        var recording = TeachingRecording.Open(manifest);
        var known = Validate(request, recording.Frames);
        var pack = PetDanceAssets.Import(archive, output, token);
        var region = new Rectangle(request.Region[0], request.Region[1], request.Region[2], request.Region[3]);
        var calibration = PetDanceVision.Calibrate(pack, recording, request.CueIndex, request.NeutralIndex, region, known, token);
        using var vision = new PetDanceVision(pack);
        var observations = new List<object>();
        var frames = recording.Frames;
        for (var index = 0; index < frames.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            using var frame = recording.ReadFrame(index);
            var match = vision.Read(frame);
            observations.Add(new { index, frames[index].TimeMs, frames[index].Sha256, cue = match.Cue.ToString(), match.Score, match.Margin });
            await Task.Delay(20, token).ConfigureAwait(false);
        }
        if (!source.SequenceEqual(GameExperiments.BoundedBytes(manifest, 1048576)) ||
            !requestBytes.SequenceEqual(GameExperiments.BoundedBytes(requestPath, 16384)))
            throw new IOException("Recording or request changed during calibration.");
        GameExperiments.WriteAtomic(Path.Combine(output, "calibration-review.json"), new
        {
            schema_version = 1,
            pack,
            calibration,
            observations,
            source_recording_sha256 = Convert.ToHexString(SHA256.HashData(source)),
            request_sha256 = Convert.ToHexString(SHA256.HashData(requestBytes)),
            labels_modified = false,
            live_qualified = false,
            inputs_sent = 0,
            opencv_threads = 1,
            note = "Offline image calibration only; source video scale and timing still apply. No phase, sequence, action alignment or gameplay success qualification."
        });
    }
}
