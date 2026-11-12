using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage.Streams;

namespace ProjectMaelstrom.Utilities;

/// <summary>Isolated synthetic test of Windows video encoding with explicit frame timestamps.</summary>
internal static class NativeTimestampProbe
{
    public static async Task RunAsync(string directory, int width = 320, int height = 180, int frames = 60)
    {
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Choose a new probe folder.");
        Directory.CreateDirectory(directory);
        WindowObserver.CheckSize(width, height);
        if (width % 2 != 0 || height % 2 != 0 || frames is < 2 or > 60) throw new ArgumentOutOfRangeException(nameof(frames));
        var expected = new double[frames];
        for (var i = 1; i < frames; i++) expected[i] = expected[i - 1] + (i % 3 == 0 ? 50 : i % 3 == 1 ? 17 : 33);
        var properties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)width, (uint)height);
        properties.FrameRate.Numerator = 60;
        properties.FrameRate.Denominator = 1;
        var source = new MediaStreamSource(new VideoStreamDescriptor(properties)) { BufferTime = TimeSpan.Zero, CanSeek = false };
        var index = 0;
        source.Starting += (_, args) => args.Request.SetActualStartPosition(TimeSpan.Zero);
        source.SampleRequested += (_, args) =>
        {
            if (index >= frames) { args.Request.Sample = null; return; }
            var pixels = new byte[width * height * 4];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = (byte)(index * 4);
                pixels[i + 1] = 100;
                pixels[i + 2] = 200;
                pixels[i + 3] = 255;
            }
            using var writer = new DataWriter();
            writer.WriteBytes(pixels);
            var sample = MediaStreamSample.CreateFromBuffer(writer.DetachBuffer(), TimeSpan.FromMilliseconds(expected[index]));
            sample.Duration = TimeSpan.FromMilliseconds(index + 1 < frames ? expected[index + 1] - expected[index] : 17);
            index++;
            args.Request.Sample = sample;
        };
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
        profile.Audio = null;
        profile.Video.Width = (uint)width;
        profile.Video.Height = (uint)height;
        profile.Video.FrameRate.Numerator = 60;
        profile.Video.FrameRate.Denominator = 1;
        profile.Video.Bitrate = 4_000_000;
        var video = Path.Combine(directory, "native-timestamp.mp4");
        using (var file = new FileStream(video, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        using (var stream = file.AsRandomAccessStream())
        {
            var transcoder = new MediaTranscoder { HardwareAccelerationEnabled = true };
            var prepared = await transcoder.PrepareMediaStreamSourceTranscodeAsync(source, stream, profile);
            if (!prepared.CanTranscode) throw new InvalidOperationException("Windows encoder unavailable: " + prepared.FailureReason);
            await prepared.TranscodeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            await stream.FlushAsync();
        }
        var ffmpeg = VideoTools.FindFfmpeg(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH")) ?? throw new InvalidOperationException("ffprobe is needed to inspect the result.");
        var inspection = await VideoFileInspection.InspectAsync(Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe"), video);
        inspection.EnsureMatchesSubmission(frames);
        var timing = VideoTimingError.Create(expected, inspection.FrameTimesMs);
        File.WriteAllText(Path.Combine(directory, "result.json"), System.Text.Json.JsonSerializer.Serialize(new { frames, timing }));
    }
}
