using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Independently checks the decoded frame count and duration of a probe recording.</summary>
internal sealed record VideoFileInspection(long Frames, double DurationSeconds, IReadOnlyList<double> FrameTimesMs)
{
    public void EnsureMatchesSubmission(long submitted)
    {
        if (Frames != submitted)
            throw new InvalidDataException($"Video contains {Frames} frames, but {submitted} were submitted.");
    }

    public static async Task<VideoFileInspection> InspectAsync(string ffprobe, string videoPath)
    {
        var info = new ProcessStartInfo(ffprobe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[] { "-v", "error", "-threads", "1", "-select_streams", "v:0", "-count_frames", "-show_frames", "-show_entries", "frame=best_effort_timestamp_time:stream=nb_read_frames,duration", "-of", "json", videoPath })
            info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Video inspection did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
        catch (TimeoutException)
        {
            try { process.Kill(true); } catch (InvalidOperationException) { }
            throw new TimeoutException("Video inspection timed out.");
        }
        var json = await output;
        var error = await errors;
        if (process.ExitCode != 0)
            throw new InvalidDataException("Video inspection failed: " + error[..Math.Min(error.Length, 1000)]);
        return Parse(json);
    }

    internal static VideoFileInspection Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var streams = document.RootElement.GetProperty("streams");
            if (streams.GetArrayLength() != 1) throw new InvalidDataException("Expected one video stream.");
            var stream = streams[0];
            var framesText = stream.GetProperty("nb_read_frames").GetString();
            var durationText = stream.GetProperty("duration").GetString();
            if (!long.TryParse(framesText, NumberStyles.None, CultureInfo.InvariantCulture, out var frames) || frames <= 0 ||
                !double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) || !double.IsFinite(duration) || duration <= 0)
                throw new InvalidDataException("Video inspection returned an invalid frame count or duration.");
            var times = new List<double>();
            foreach (var frame in document.RootElement.GetProperty("frames").EnumerateArray())
            {
                var value = frame.GetProperty("best_effort_timestamp_time").GetString();
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || !double.IsFinite(seconds) || seconds < 0 ||
                    (times.Count > 0 && seconds * 1000 < times[^1]))
                    throw new InvalidDataException("Video inspection returned invalid frame timestamps.");
                times.Add(seconds * 1000);
            }
            if (times.Count != frames) throw new InvalidDataException("Video timestamp count does not match decoded frames.");
            return new VideoFileInspection(frames, duration, times);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("Video inspection returned an invalid report.", ex);
        }
    }
}
