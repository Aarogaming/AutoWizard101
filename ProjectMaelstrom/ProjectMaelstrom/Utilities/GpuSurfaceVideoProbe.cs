using System.Collections.Concurrent;
using System.Text.Json;
using Windows.Graphics.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;

namespace ProjectMaelstrom.Utilities;

/// <summary>Isolated experiment: retain capture surfaces until Windows reports encoder processing.</summary>
internal static class GpuSurfaceVideoProbe
{
    private sealed record TimedFrame(SharedLease<Direct3D11CaptureFrame> Lease, double TimeMs) : IDisposable
    {
        public void Dispose() => Lease.Dispose();
    }

    public static async Task RunAsync(GameWindow target, string directory, TimeSpan duration, bool warm = false, bool direct = false, CancellationToken stop = default, Action? onFirstFrame = null,
        Action<double, double>? onCaptureStarted = null, Action<double>? onCaptureStopped = null, bool allowRateConvertedOutput = false,
        Func<Task>? prepareSidecars = null)
    {
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Choose a new probe folder.");
        Directory.CreateDirectory(directory);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(stop);
        using var firstFrameWatch = new CancellationTokenSource();
        cancel.CancelAfter(duration + TimeSpan.FromSeconds(30));
        if (warm)
        {
            var size = WindowObserver.GetCaptureSize(target);
            await EncoderWarmupProcess.RunAsync(size.Width, size.Height, cancel.Token);
        }
        if (prepareSidecars != null) await prepareSidecars().ConfigureAwait(false);
        cancel.Token.ThrowIfCancellationRequested();
        var captured = new List<double>();
        var submitted = new List<double>();
        var deliveryDelays = new List<double>();
        var callbackDurations = new List<double>();
        const int poolCapacity = 16;
        var queue = new FrameBuffer<TimedFrame>(13);
        var inFlight = new ConcurrentDictionary<int, (MediaStreamSample Sample, TimedFrame Frame)>();
        SharedLease<Direct3D11CaptureFrame>? latestFrame = null;
        double? latestTime = null;
        var terminalFrameAdded = false;
        // Allow setup time, then measure the requested duration from the first captured frame.
        await using var observer = new WindowObserver(target, framePoolCapacity: poolCapacity);
        var width = observer.Width;
        var height = observer.Height;
        var encoded = VideoGeometry.EncodedSize(width, height);
        var properties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)width, (uint)height);
        properties.FrameRate.Numerator = 60;
        properties.FrameRate.Denominator = 1;
        var source = new MediaStreamSource(new VideoStreamDescriptor(properties)) { BufferTime = TimeSpan.Zero, CanSeek = false };
        await using var reader = queue.ReadAllAsync().GetAsyncEnumerator();
        using var sampleGate = new SemaphoreSlim(1, 1);
        Exception? callbackFailure = null;
        Exception? firstFrameFailure = null;
        Exception? sourceFailure = null;
        double? origin = null;
        var sequence = 0;
        var processed = 0;
        void Accept(Direct3D11CaptureFrame frame)
        {
            if (cancel.IsCancellationRequested) { frame.Dispose(); return; }
            var entered = FrameTimeline.TimestampMs();
            try
            {
                var time = frame.SystemRelativeTime.TotalMilliseconds;
                if (captured.Count == 0) { firstFrameWatch.Cancel(); cancel.CancelAfter(duration); onCaptureStarted?.Invoke(time, entered); onFirstFrame?.Invoke(); }
                captured.Add(time);
                deliveryDelays.Add(entered - time);
                var lease = new SharedLease<Direct3D11CaptureFrame>(frame);
                var retained = lease.Retain();
                var queued = new TimedFrame(lease, time);
                if (queue.TryWrite(queued))
                {
                    var previous = latestFrame;
                    latestFrame = retained;
                    latestTime = time;
                    previous?.Dispose();
                }
                else { queued.Dispose(); retained.Dispose(); }
            }
            finally { callbackDurations.Add(FrameTimeline.TimestampMs() - entered); }
        }
        source.Starting += (_, args) => args.Request.SetActualStartPosition(TimeSpan.Zero);
        source.SampleRequested += async (_, args) =>
        {
            var deferral = args.Request.GetDeferral();
            await sampleGate.WaitAsync();
            try
            {
                if (!await reader.MoveNextAsync()) { args.Request.Sample = null; return; }
                var frame = reader.Current;
                int? retainedId = null;
                try
                {
                    var time = frame.TimeMs;
                    origin ??= time;
                    if (submitted.Count > 0 && time <= submitted[^1]) throw new InvalidDataException("Capture timestamps must increase.");
                    var sample = MediaStreamSample.CreateFromDirect3D11Surface(frame.Lease.Resource.Surface, TimeSpan.FromMilliseconds(time - origin.Value));
                    sample.Duration = TimeSpan.FromMilliseconds(1000.0 / 60);
                    var id = sequence++;
                    if (!inFlight.TryAdd(id, (sample, frame))) throw new InvalidOperationException("Duplicate frame identity.");
                    retainedId = id;
                    sample.Processed += (_, _) =>
                    {
                        if (inFlight.TryRemove(id, out var held)) { held.Frame.Dispose(); Interlocked.Increment(ref processed); }
                    };
                    submitted.Add(time);
                    args.Request.Sample = sample;
                }
                catch
                {
                    if (retainedId.HasValue) { if (inFlight.TryRemove(retainedId.Value, out var retained)) retained.Frame.Dispose(); }
                    else frame.Dispose();
                    throw;
                }
            }
            catch (Exception ex) { callbackFailure = ex; cancel.Cancel(); args.Request.Sample = null; }
            finally { sampleGate.Release(); deferral.Complete(); }
        };
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
        profile.Audio = null;
        profile.Video.Width = (uint)encoded.Width;
        profile.Video.Height = (uint)encoded.Height;
        profile.Video.FrameRate.Numerator = 60;
        profile.Video.FrameRate.Denominator = 1;
        profile.Video.Bitrate = 4_000_000;
        var path = Path.Combine(directory, "capture.mp4");
        Task? producer = null;
        double? stoppedAtMs = null;
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var stream = file.AsRandomAccessStream())
            {
                var transcoder = new MediaTranscoder { HardwareAccelerationEnabled = true };
                var prepared = await transcoder.PrepareMediaStreamSourceTranscodeAsync(source, stream, profile);
                if (!prepared.CanTranscode) throw new InvalidOperationException("Windows encoder unavailable: " + prepared.FailureReason);
                if (direct)
                {
                    observer.SetSurfaceReceiver(Accept, error => { sourceFailure = error; queue.Complete(error); cancel.Cancel(); });
                }
                producer = Task.Run(async () =>
                {
                    try
                    {
                        while (!cancel.IsCancellationRequested)
                        {
                            if (direct)
                            {
                                if (!GameWindows.IsCurrent(target)) throw new InvalidOperationException("Game disconnected.");
                                if (GameWindows.Minimized(target)) throw new InvalidOperationException("Game is minimized.");
                                await Task.Delay(250, cancel.Token);
                                continue;
                            }
                            var frame = await observer.ReadSurfaceAsync();
                            if (frame == null) { await observer.WaitForFrameAsync(cancel.Token); continue; }
                            Accept(frame);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { sourceFailure = ex; queue.Complete(ex); }
                    finally
                    {
                        if (direct) observer.SetSurfaceReceiver(null, null);
                        stoppedAtMs = FrameTimeline.TimestampMs();
                        onCaptureStopped?.Invoke(stoppedAtMs.Value);
                        if (latestFrame != null && latestTime.HasValue && stoppedAtMs.Value - latestTime.Value > 50 && sourceFailure == null && callbackFailure == null && firstFrameFailure == null)
                        {
                            // Explicitly hold the last observed image through the stop time. This is
                            // a synthetic terminal frame, not a new observation of the game.
                            var terminal = new TimedFrame(latestFrame.Retain(), stoppedAtMs.Value);
                            try
                            {
                                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                                await queue.WriteAsync(terminal, deadline.Token);
                                terminalFrameAdded = true;
                            }
                            catch (Exception ex) { terminal.Dispose(); queue.Complete(ex); }
                        }
                        latestFrame?.Dispose();
                        latestFrame = null;
                        queue.Complete();
                    }
                });
                var watchdog = Task.Run(async () =>
                {
                    try { await Task.Delay(TimeSpan.FromSeconds(10), firstFrameWatch.Token); }
                    catch (OperationCanceledException) { return; }
                    if (cancel.IsCancellationRequested) return;
                    firstFrameFailure = new InvalidOperationException("No video frame arrived. The window may be static, hidden, or not rendering; make it visible or try again after it changes.");
                    queue.Complete(firstFrameFailure);
                    cancel.Cancel();
                });
                using var timeout = new CancellationTokenSource(duration + TimeSpan.FromSeconds(30));
                try { await prepared.TranscodeAsync().AsTask(timeout.Token); }
                catch (Exception ex) when (firstFrameFailure != null) { throw new InvalidOperationException(firstFrameFailure.Message, ex); }
                finally { firstFrameWatch.Cancel(); await watchdog; }
                await producer;
                if (callbackFailure != null) throw new InvalidOperationException("Encoder source failed: " + callbackFailure.Message, callbackFailure);
                for (var i = 0; i < 40 && !inFlight.IsEmpty; i++) await Task.Delay(50);
                if (!inFlight.IsEmpty || processed != submitted.Count) throw new InvalidOperationException("The encoder did not release all captured surfaces.");
                await stream.FlushAsync();
            }
            var ffmpeg = VideoTools.FindFfmpeg(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH")) ?? throw new InvalidOperationException("ffprobe is needed to inspect the result.");
            var inspection = await VideoFileInspection.InspectAsync(Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe"), path);
            // Resizing an odd surface can introduce frame-rate conversion in Windows.
            // Preserve the source/submission/decoded clocks even when strict verification rejects it.
            GameExperiments.WriteAtomic(Path.Combine(directory, "source-timestamps.json"), new
            {
                width,
                height,
                encoded_width = encoded.Width,
                encoded_height = encoded.Height,
                captured,
                submitted,
                decoded = inspection.FrameTimesMs,
                stopped_at_ms = stoppedAtMs,
                queue_rejected = queue.Dropped,
                synthetic_terminal_frame = terminalFrameAdded
            });
            var rateConverted = VideoGeometry.IsRateConverted(inspection, submitted,
                allowRateConvertedOutput && encoded != new Size(width, height));
            var timing = rateConverted ? null : VideoTimingError.Create(submitted, inspection.FrameTimesMs);
            var coverage = stoppedAtMs.HasValue ? VideoCoverageSummary.Create(captured, stoppedAtMs.Value, inspection.DurationSeconds) : null;
            var report = JsonSerializer.Serialize(new
            {
                status = "complete",
                failure = (string?)null,
                width,
                height,
                encoded_width = encoded.Width,
                encoded_height = encoded.Height,
                source_to_video = new
                {
                    scale_x = encoded.Width / (double)width,
                    scale_y = encoded.Height / (double)height,
                    ratio_scope = "output/source dimensions only; not a measured pixel transform",
                    transform_verified = false,
                    crop = (bool?)null,
                    explicit_crop_requested = false
                },
                frame_count_matches_submission = !rateConverted,
                source_frame_alignment = rateConverted ? "unverified-rate-conversion; extra encoded frames are not new observations" : "one-to-one-timestamps",
                pool_capacity = poolCapacity,
                captured = captured.Count,
                submitted = submitted.Count,
                verified = inspection.Frames,
                verified_video_frames = inspection.Frames,
                verified_video_duration_seconds = inspection.DurationSeconds,
                processed,
                queue_rejected = queue.Dropped,
                capture_timestamp_ms = captured,
                submitted_timestamp_ms = submitted,
                video_timestamp_ms = inspection.FrameTimesMs,
                capture_timing = VideoTimingSummary.Create(captured),
                frame_delivery_delay_ms = TimingDistribution.Create(deliveryDelays),
                capture_callback_duration_ms = TimingDistribution.Create(callbackDurations),
                playback_timing_error = timing,
                recording_coverage = coverage,
                synthetic_terminal_frame = terminalFrameAdded,
                encoder = "Windows Media Transcoder (GPU surfaces)",
                target_fps = 60,
                playback_warning = onCaptureStarted == null
                    ? "A synthetic terminal frame may hold the last observed image through the stop time; it is not a new game observation. The MP4 has no audio stream; separately requested audio is described in audio-timing.json. No input events were requested."
                    : "A synthetic terminal frame may hold the last observed image through the stop time; it is not a new game observation. The MP4 has no audio stream; separately requested audio is described in audio-timing.json, and input observations in input-events.json."
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(directory, "result.json"), report);
            File.WriteAllText(Path.Combine(directory, "timing.json"), report);
        }
        finally
        {
            cancel.Cancel();
            if (direct) observer.SetSurfaceReceiver(null, null);
            queue.Complete();
            if (producer != null) { try { await producer; } catch { } }
            latestFrame?.Dispose();
            latestFrame = null;
            try { await foreach (var frame in queue.ReadAllAsync()) frame.Dispose(); } catch { }
            foreach (var id in inFlight.Keys) if (inFlight.TryRemove(id, out var held)) held.Frame.Dispose();
        }
    }
}
