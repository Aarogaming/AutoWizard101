using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Extracts bounded review samples with approximate video-relative timestamps.</summary>
internal static class VideoTeachingImport
{
    internal static int MaximumSeconds(int framesPerSecond)
    {
        if (framesPerSecond is < 1 or > 60) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        return Math.Min(180, TeachingRecording.MaximumFrames / framesPerSecond);
    }

    public static async Task<TeachingRecording> RunAsync(string dataRoot, string workspace, string video, string ffmpeg, CancellationToken token, int framesPerSecond = 1, int startSeconds = 0)
    {
        if (!File.Exists(video)) throw new FileNotFoundException("The selected video is unavailable.", video);
        if (new FileInfo(video).Length > 8L * 1024 * 1024 * 1024) throw new InvalidDataException("Choose a local clip smaller than 8 GiB.");
        if (startSeconds is < 0 or > 86400) throw new ArgumentOutOfRangeException(nameof(startSeconds), "Choose a start between 0 and 86400 seconds.");
        var seconds = MaximumSeconds(framesPerSecond);
        var recording = TeachingRecording.Start(dataRoot, workspace, "video-import");
        var staging = Path.Combine(recording.DirectoryPath, "video-samples");
        Directory.CreateDirectory(staging);
        void Source(string status) => File.WriteAllText(Path.Combine(recording.DirectoryPath, "video-source.json"), JsonSerializer.Serialize(new
        {
            source = Path.GetFullPath(video),
            status,
            start_seconds = startSeconds,
            sample_fps = framesPerSecond,
            maximum_width = 1280,
            maximum_height = 720,
            verified_inputs = false,
            sampling = $"{framesPerSecond} frames per second, up to {seconds} seconds",
            timing = "Review times are approximate video positions, not exact original capture timestamps. A video may contain held or repeated frames; inspect its timing report when available."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Source("sampling");
        try
        {
            var info = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var arg in Arguments(video, staging, framesPerSecond, startSeconds)) info.ArgumentList.Add(arg);
            using var job = new OwnedProcessJob();
            using var process = Process.Start(info) ?? throw new InvalidOperationException("FFmpeg did not start.");
            try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            try { job.Attach(process); }
            catch { if (!process.HasExited) process.Kill(true); throw; }
            var errors = process.StandardError.ReadToEndAsync();
            using var limited = CancellationTokenSource.CreateLinkedTokenSource(token);
            using var monitorStop = new CancellationTokenSource();
            string? limitReason = null;
            long StagedBytes() => Directory.EnumerateFiles(staging, "sample-*.png").Sum(path => new FileInfo(path).Length);
            async Task MonitorAsync()
            {
                while (!monitorStop.IsCancellationRequested)
                {
                    await Task.Delay(500, monitorStop.Token);
                    try
                    {
                        if (StagedBytes() > TeachingRecording.MaximumPackImageBytes)
                        { limitReason = "Video samples exceeded 128 MiB. Choose a shorter clip or lower sample rate."; limited.Cancel(); return; }
                    }
                    catch (IOException) { limitReason = "Video sample storage could not be monitored."; limited.Cancel(); return; }
                }
            }
            var monitor = MonitorAsync();
            try { await process.WaitForExitAsync(limited.Token).WaitAsync(TimeSpan.FromMinutes(3), limited.Token); }
            catch
            {
                if (!process.HasExited) process.Kill(true);
                if (limitReason != null) throw new InvalidDataException(limitReason);
                throw;
            }
            finally { monitorStop.Cancel(); try { await monitor; } catch (OperationCanceledException) { } }
            var error = await errors;
            if (process.ExitCode != 0) throw new InvalidOperationException("Video sampling failed: " + error[..Math.Min(error.Length, 1000)]);
            if (StagedBytes() > TeachingRecording.MaximumPackImageBytes) throw new InvalidDataException("Video samples exceed 128 MiB. Choose a shorter clip or lower sample rate.");
            var frames = Directory.GetFiles(staging, "sample-*.png").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (frames.Length is 0 or > TeachingRecording.MaximumFrames) throw new InvalidDataException("The video did not yield a supported number of samples.");
            for (var i = 0; i < frames.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                if (Path.GetFileName(frames[i]) != $"sample-{i:D4}.png") throw new InvalidDataException("Video sample numbering is incomplete.");
                using var image = new Bitmap(frames[i]);
                var canContinue = recording.AddImportedFrame(image, startSeconds * 1000.0 + i * 1000.0 / framesPerSecond);
                if (!canContinue && (recording.Frames.Count != i + 1 || i + 1 < frames.Length)) throw new InvalidDataException("Review recording reached its storage limit.");
            }
            recording.Finish();
            Source("complete");
            foreach (var frame in frames) File.Delete(frame);
            Directory.Delete(staging);
            return recording;
        }
        catch
        {
            try { recording.Finish(); } catch { }
            try { Source(token.IsCancellationRequested ? "cancelled" : "failed"); } catch { }
            throw;
        }
    }

    internal static string[] Arguments(string video, string staging, int framesPerSecond, int startSeconds)
    {
        var seconds = MaximumSeconds(framesPerSecond);
        if (startSeconds is < 0 or > 86400) throw new ArgumentOutOfRangeException(nameof(startSeconds));
        return ["-hide_banner", "-loglevel", "error", "-nostdin", "-n", "-protocol_whitelist", "file,pipe",
            "-threads", "1", "-filter_threads", "1", "-ss", startSeconds.ToString(CultureInfo.InvariantCulture), "-i", video,
            "-t", seconds.ToString(CultureInfo.InvariantCulture), "-vf", $"fps={framesPerSecond}:start_time=0,scale=w='min(1280,iw)':h='min(720,ih)':force_original_aspect_ratio=decrease",
            "-threads", "1", "-frames:v", TeachingRecording.MaximumFrames.ToString(CultureInfo.InvariantCulture), "-start_number", "0", Path.Combine(staging, "sample-%04d.png")];
    }
}
