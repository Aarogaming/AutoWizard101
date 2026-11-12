using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Creates a read-only replay session from verified, saved Dodge frames.</summary>
internal static class DodgeSessionExport
{
    public static async Task<string> RunAsync(TeachingRecording recording, string dataRoot, CancellationToken token)
    {
        if (!recording.Complete) throw new InvalidOperationException("Stop the recording before analyzing it.");
        var frames = recording.Frames;
        if (frames.Count == 0) throw new InvalidOperationException("The recording has no frames.");
        var sourcePath = Path.Combine(recording.DirectoryPath, "recording.json");
        if (new FileInfo(sourcePath).Length > 2 * 1024 * 1024) throw new InvalidDataException("Recording manifest is too large.");
        var sourceHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(sourcePath, token).ConfigureAwait(false)));
        var rows = new StringBuilder();
        for (var index = 0; index < frames.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            using var image = recording.ReadFrame(index);
            var observation = DodgeSpecialist.Analyze(image);
            var state = observation.Player == null ? "unknown" : "dodge-advice-" + observation.Advice.ToLowerInvariant();
            var frame = frames[index];
            rows.AppendLine(JsonSerializer.Serialize(new
            {
                event_id = $"frame-{index:D4}",
                kind = "observation",
                sequence = index + 1,
                time_ms = frame.TimeMs,
                payload = new { state, detector = "dodge-color-baseline-v1", frame_sha256 = frame.Sha256, threats = observation.Threats.Count }
            }));
            // Bound sustained analysis load; there is only one frame in memory at a time.
            if (index + 1 < frames.Count) await Task.Delay(100, token).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
        if (new FileInfo(sourcePath).Length > 2 * 1024 * 1024 || sourceHash != Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(sourcePath, token).ConfigureAwait(false))))
            throw new InvalidDataException("The source recording changed during analysis. Try again.");
        var sessionId = "dodge-" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(dataRoot, "sessions", sessionId);
        if (Directory.Exists(directory)) throw new IOException("Session destination already exists.");
        Directory.CreateDirectory(directory);
        var eventsPath = Path.Combine(directory, "events.jsonl");
        await File.WriteAllTextAsync(eventsPath, rows.ToString(), new UTF8Encoding(false), token).ConfigureAwait(false);
        var manifest = new { schema_version = 1, session_id = sessionId, clock_origin = "source-recording-relative-ms", events_file = "events.jsonl", source_recording = sourcePath, source_recording_sha256 = sourceHash, detector = "dodge-color-baseline-v1", note = "Predictions are unverified; no game inputs were sent." };
        var manifestPath = Path.Combine(directory, "session.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false), token).ConfigureAwait(false);
        return manifestPath;
    }
}
