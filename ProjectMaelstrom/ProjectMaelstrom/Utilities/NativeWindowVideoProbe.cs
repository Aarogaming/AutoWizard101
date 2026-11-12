using System.Text.Json;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage.Streams;

namespace ProjectMaelstrom.Utilities;

/// <summary>Experimental bounded recorder using capture timestamps for Windows encoder samples.</summary>
internal static class NativeWindowVideoProbe
{
    public static async Task RunAsync(GameWindow target, string directory, TimeSpan duration, CancellationToken stop = default, Action? onFirstFrame = null)
    {
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Choose a new recording folder.");
        Directory.CreateDirectory(directory);
        var captured = new List<double>();
        var submitted = new List<double>();
        var buffer = new FrameBuffer<CapturedWindowPixels>(4);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(stop);
        cancel.CancelAfter(duration + TimeSpan.FromSeconds(30));
        WindowObserver? observer = null;
        Task? producer = null;
        VideoFileInspection? inspection = null;
        VideoTimingError? timing = null;
        string? failure = null;
        int width = 0, height = 0;
        var path = Path.Combine(directory, "capture.mp4");
        try
        {
            observer = new WindowObserver(target);
            width = observer.Width;
            height = observer.Height;
            if (width % 2 != 0 || height % 2 != 0) throw new InvalidOperationException("Video preview needs an even-sized game window; resize it and reconnect.");
            var properties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)width, (uint)height);
            properties.FrameRate.Numerator = 60;
            properties.FrameRate.Denominator = 1;
            var source = new MediaStreamSource(new VideoStreamDescriptor(properties)) { BufferTime = TimeSpan.Zero, CanSeek = false };
            await using var reader = buffer.ReadAllAsync().GetAsyncEnumerator();
            using var sampleGate = new SemaphoreSlim(1, 1);
            Exception? callbackFailure = null;
            double? origin = null;
            source.Starting += (_, args) => args.Request.SetActualStartPosition(TimeSpan.Zero);
            source.SampleRequested += async (_, args) =>
            {
                var deferral = args.Request.GetDeferral();
                await sampleGate.WaitAsync();
                try
                {
                    if (!await reader.MoveNextAsync()) { args.Request.Sample = null; return; }
                    using var frame = reader.Current;
                    var time = frame.CaptureTime?.TotalMilliseconds ?? throw new InvalidDataException("Capture timestamp unavailable.");
                    origin ??= time;
                    if (submitted.Count > 0 && time <= submitted[^1]) throw new InvalidDataException("Capture timestamps must increase.");
                    using var writer = new DataWriter();
                    writer.WriteBytes(frame.Bgra);
                    var sample = MediaStreamSample.CreateFromBuffer(writer.DetachBuffer(), TimeSpan.FromMilliseconds(time - origin.Value));
                    sample.Duration = TimeSpan.FromMilliseconds(1000.0 / 60);
                    submitted.Add(time);
                    args.Request.Sample = sample;
                }
                catch (Exception ex) { callbackFailure = ex; cancel.Cancel(); args.Request.Sample = null; }
                finally { sampleGate.Release(); deferral.Complete(); }
            };
            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
            profile.Audio = null;
            profile.Video.Width = (uint)width;
            profile.Video.Height = (uint)height;
            profile.Video.FrameRate.Numerator = 60;
            profile.Video.FrameRate.Denominator = 1;
            profile.Video.Bitrate = 4_000_000;
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var stream = file.AsRandomAccessStream())
            {
                var transcoder = new MediaTranscoder { HardwareAccelerationEnabled = true };
                var prepared = await transcoder.PrepareMediaStreamSourceTranscodeAsync(source, stream, profile);
                if (!prepared.CanTranscode) throw new InvalidOperationException("Windows encoder unavailable: " + prepared.FailureReason);
                producer = Task.Run(async () =>
                {
                    try
                    {
                        while (!cancel.IsCancellationRequested)
                        {
                            var frame = await observer.ReadPixelsAsync();
                            if (frame == null) { await observer.WaitForFrameAsync(cancel.Token); continue; }
                            if (!frame.CaptureTime.HasValue) { frame.Dispose(); throw new InvalidDataException("Capture timestamp unavailable."); }
                            if (captured.Count == 0) cancel.CancelAfter(duration);
                            captured.Add(frame.CaptureTime.Value.TotalMilliseconds);
                            if (captured.Count == 1) onFirstFrame?.Invoke();
                            if (!buffer.TryWrite(frame)) frame.Dispose();
                        }
                        buffer.Complete();
                    }
                    catch (OperationCanceledException) { buffer.Complete(); }
                    catch (Exception ex) { buffer.Complete(ex); }
                });
                using var encoderTimeout = new CancellationTokenSource(duration + TimeSpan.FromSeconds(30));
                using var stopDeadline = cancel.Token.Register(() => encoderTimeout.CancelAfter(TimeSpan.FromSeconds(30)));
                await prepared.TranscodeAsync().AsTask(encoderTimeout.Token);
                await producer;
                if (callbackFailure != null) throw new InvalidOperationException("Encoder source failed: " + callbackFailure.Message, callbackFailure);
                await stream.FlushAsync();
            }
            var ffmpeg = VideoTools.FindFfmpeg(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH")) ?? throw new InvalidOperationException("FFmpeg and ffprobe are needed to verify the video.");
            inspection = await VideoFileInspection.InspectAsync(Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe"), path);
            inspection.EnsureMatchesSubmission(submitted.Count);
            timing = VideoTimingError.Create(submitted, inspection.FrameTimesMs);
        }
        catch (Exception ex) { failure = ex.Message; cancel.Cancel(); buffer.Complete(); }
        finally
        {
            cancel.Cancel();
            buffer.Complete();
            if (producer != null) { try { await producer; } catch { } }
            if (observer != null) await observer.DisposeAsync();
            File.WriteAllText(Path.Combine(directory, "timing.json"), JsonSerializer.Serialize(new
            {
                status = failure == null ? "complete" : "failed",
                failure,
                capture_timestamp_ms = captured,
                submitted_timestamp_ms = submitted,
                video_timestamp_ms = inspection?.FrameTimesMs,
                captured = captured.Count,
                submitted = submitted.Count,
                verified_video_frames = inspection?.Frames,
                verified_video_duration_seconds = inspection?.DurationSeconds,
                queue_rejected = buffer.Dropped,
                capture_timing = VideoTimingSummary.Create(captured),
                playback_timing_error = timing,
                encoder = "Windows Media Transcoder",
                width,
                height,
                target_fps = 60,
                playback_warning = "Capture timestamps are used for video frames; no audio or input events were recorded."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (failure != null) throw new InvalidOperationException("Video recording failed: " + failure);
    }
}
