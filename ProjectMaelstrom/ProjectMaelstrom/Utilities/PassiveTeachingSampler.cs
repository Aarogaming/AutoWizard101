using System.Diagnostics;

namespace ProjectMaelstrom.Utilities;

/// <summary>Bounded, selected-window still sampling without a form, input hooks, or focus changes.</summary>
internal static class PassiveTeachingSampler
{
    internal static void Validate(int seconds, int fps)
    {
        if (seconds is < 1 or > 60 || fps is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(seconds), "Use 1–60 seconds and 1–5 samples per second.");
    }

    public static async Task<string> RunAsync(GameWindow target, string dataRoot, string workspace, int seconds, int fps, CancellationToken token)
    {
        Validate(seconds, fps);
        await using var observer = new WindowObserver(target);
        var recording = TeachingRecording.Start(dataRoot, workspace, target.ProcessName);
        new GameplaySamplePlan("teaching", "Observe instructions, preview, selections and outcomes; inputs are not recorded.", null, null).Save(recording.DirectoryPath);
        var clock = Stopwatch.StartNew();
        var origin = FrameTimeline.TimestampMs();
        double? previous = null;
        long bytes = 0; var rejected = 0; double? maximumAge = null;
        string reason = "duration-limit";
        try
        {
            while (clock.Elapsed.TotalSeconds < seconds && recording.Frames.Count < seconds * fps)
            {
                token.ThrowIfCancellationRequested();
                if (!GameWindows.IsCurrent(target) || GameWindows.Minimized(target)) { reason = "target-unavailable"; break; }
                using var frame = await observer.ReadTimestampedAsync(latestOnly: true).ConfigureAwait(false);
                if (frame?.CaptureTime is { } time)
                    maximumAge = Math.Max(maximumAge ?? 0, FrameTimeline.TimestampMs() - time.TotalMilliseconds);
                if (frame?.CaptureTime is { } captured && captured.TotalMilliseconds >= origin &&
                    (!previous.HasValue || captured.TotalMilliseconds > previous.Value) &&
                    DodgeLiveRun.IsFreshCapture(captured.TotalMilliseconds, FrameTimeline.TimestampMs(), 500))
                {
                    if (!recording.AddImportedFrame(frame.Image, captured.TotalMilliseconds - origin)) { reason = "recording-limit"; break; }
                    previous = captured.TotalMilliseconds;
                    bytes += new FileInfo(Path.Combine(recording.DirectoryPath, recording.Frames[^1].File)).Length;
                    if (recording.Frames.Count == 1)
                        File.WriteAllText(Path.Combine(recording.DirectoryPath, "ready.txt"), DateTime.UtcNow.ToString("O"));
                    if (bytes >= 128L * 1024 * 1024) { reason = "storage-limit"; break; }
                }
                else { rejected++; if (clock.Elapsed.TotalSeconds > 5 && previous == null) { reason = "no-fresh-frames"; break; } }
                await Task.Delay(1000 / fps, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { reason = "cancelled"; }
        catch (Exception ex) { reason = "failed: " + ex.Message; throw; }
        finally
        {
            recording.Finish();
            GameExperiments.WriteAtomic(Path.Combine(recording.DirectoryPath, "passive-sample.json"), new
            {
                target = new { handle = target.Handle.ToInt64(), target.ProcessId, target.StartedUtcTicks, target.ProcessName },
                reason,
                requested_seconds = seconds,
                maximum_fps = fps,
                frames = recording.Frames.Count,
                elapsed_ms = clock.Elapsed.TotalMilliseconds,
                bytes,
                rejected,
                maximum_frame_age_ms = maximumAge,
                input_recorded = false,
                inputs_sent = 0,
                note = "Window-only still samples. Capture timestamps relative to sampler start; not a real-time video or verified action labels."
            });
        }
        return recording.DirectoryPath;
    }
}
