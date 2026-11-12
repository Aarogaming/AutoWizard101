using System.Diagnostics;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Controlled-window research probe. Not a production recording path.</summary>
internal static class WindowVideoProbe
{
    public static async Task RunAsync(GameWindow target, string ffmpeg, string directory, TimeSpan duration, CancellationToken stop = default)
    {
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Choose a new probe folder; existing recordings are never overwritten.");
        Directory.CreateDirectory(directory);
        var observations = new List<double>();
        var encodedTimes = new List<double>();
        var buffer = new FrameBuffer<CapturedWindowPixels>(4);
        var timer = Stopwatch.StartNew();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stop);
        cancellation.CancelAfter(duration);
        WindowObserver? observer = null;
        Process? encoder = null;
        Task<string>? errors = null;
        var encoded = 0;
        var path = Path.Combine(directory, "capture.mp4");
        var status = "complete";
        string? failure = null;
        VideoFileInspection? inspection = null;
        VideoTimingError? timingError = null;
        Task? producer = null;
        try
        {
            observer = new WindowObserver(target);
            producer = Task.Run(async () =>
            {
                try
                {
                    while (!cancellation.IsCancellationRequested)
                    {
                        var frame = await observer.ReadPixelsAsync();
                        if (frame == null) { await Task.Delay(1, cancellation.Token); continue; }
                        if (!frame.CaptureTime.HasValue) { frame.Dispose(); throw new InvalidDataException("Capture timestamp unavailable."); }
                        lock (observations) observations.Add(frame.CaptureTime.Value.TotalMilliseconds);
                        if (!buffer.TryWrite(frame)) frame.Dispose();
                    }
                    buffer.Complete();
                }
                catch (OperationCanceledException) { buffer.Complete(); }
                catch (Exception ex) { buffer.Complete(ex); }
            });
            await foreach (var frame in buffer.ReadAllAsync())
            {
                using (frame)
                {
                    if (encoder == null)
                    {
                        var info = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
                        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-n", "-use_wallclock_as_timestamps", "1", "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", $"{frame.Width}x{frame.Height}", "-framerate", "60", "-i", "pipe:0", "-an", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-enc_time_base", "1:1000", "-fps_mode", "passthrough", path }) info.ArgumentList.Add(arg);
                        encoder = Process.Start(info) ?? throw new InvalidOperationException("FFmpeg did not start.");
                        errors = encoder.StandardError.ReadToEndAsync();
                    }
                    using var writeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await encoder.StandardInput.BaseStream.WriteAsync(frame.Bgra, writeTimeout.Token);
                    encoded++;
                    encodedTimes.Add(frame.CaptureTime!.Value.TotalMilliseconds);
                }
            }
            await producer;
            if (encoder == null) throw new InvalidOperationException("No capture frames received.");
            encoder.StandardInput.Close();
            await encoder.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            var stderr = await errors!;
            if (encoder.ExitCode != 0) throw new InvalidOperationException("Encoder failed: " + stderr[..Math.Min(stderr.Length, 1000)]);
            var ffprobe = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ffmpeg))!, "ffprobe.exe");
            inspection = await VideoFileInspection.InspectAsync(ffprobe, path);
            inspection.EnsureMatchesSubmission(encoded);
            timingError = VideoTimingError.Create(encodedTimes, inspection.FrameTimesMs);
        }
        catch (Exception ex)
        {
            status = "failed";
            failure = ex.Message;
            cancellation.Cancel();
            try { if (encoder is { HasExited: false }) encoder.Kill(true); } catch (InvalidOperationException) { }
        }
        finally
        {
            cancellation.Cancel();
            buffer.Complete();
            if (producer != null) { try { await producer; } catch { } }
            try { await foreach (var remaining in buffer.ReadAllAsync()) remaining.Dispose(); } catch { }
            if (observer != null) await observer.DisposeAsync();
            encoder?.Dispose();
            File.WriteAllText(Path.Combine(directory, "timing.json"), JsonSerializer.Serialize(new { status, failure, capture_timestamp_ms = observations, submitted_timestamp_ms = encodedTimes, video_timestamp_ms = inspection?.FrameTimesMs, captured = observations.Count, submitted = encoded, verified_video_frames = inspection?.Frames, verified_video_duration_seconds = inspection?.DurationSeconds, playback_timing_error = timingError, queue_rejected = buffer.Dropped, capture_timing = VideoTimingSummary.Create(observations), submitted_timing = VideoTimingSummary.Create(encodedTimes), elapsed_ms = timer.Elapsed.TotalMilliseconds, target_fps = 60, playback_warning = "Video PTS uses encoder arrival time, not capture time. Not synchronized to input." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (failure != null) throw new InvalidOperationException("Video probe failed: " + failure);
    }
}
