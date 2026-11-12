using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Runs the supervised GPU recorder and leaves a failure report if finalization fails.</summary>
internal static class GpuVideoRecording
{
    public static async Task RunAsync(GameWindow target, string directory, TimeSpan duration, CancellationToken stop, Action onFirstFrame,
        Action<double, double>? onCaptureStarted = null, Action<double>? onCaptureStopped = null, bool allowRateConvertedOutput = false,
        bool includeGameAudio = false)
    {
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Choose a new recording folder; existing data is never overwritten.");
        GameAudioRecording? audio = null;
        var verified = false;
        try
        {
            await GpuSurfaceVideoProbe.RunAsync(target, directory, duration, warm: true, direct: true, stop, onFirstFrame,
                (captureMs, callbackMs) => { audio?.SetVideoOrigin(captureMs, callbackMs); onCaptureStarted?.Invoke(captureMs, callbackMs); },
                endMs => { audio?.SetVideoEnd(endMs); onCaptureStopped?.Invoke(endMs); }, allowRateConvertedOutput,
                includeGameAudio ? async () =>
                {
                    audio = await GameAudioRecording.StartAsync(target.ProcessId, target.StartedUtcTicks,
                    directory, () => GameWindows.IsCurrent(target), stop).ConfigureAwait(false);
                }
            : null).ConfigureAwait(false);
            verified = true;
        }
        catch (Exception ex)
        {
            // Only a directory created by this recording may receive a failure report.
            try
            {
                if (Directory.Exists(directory))
                {
                    var path = Path.Combine(directory, "timing.json");
                    using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    JsonSerializer.Serialize(file, new { status = "failed", failure = ex.Message, verified_video_frames = (int?)null }, new JsonSerializerOptions { WriteIndented = true });
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
        finally { if (audio != null) await audio.StopAsync(verified).ConfigureAwait(false); }
    }
}
