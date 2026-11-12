using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage.Streams;

namespace ProjectMaelstrom.Utilities;

/// <summary>Initializes the Windows encoder with two synthetic frames in memory.</summary>
internal static class NativeEncoderWarmup
{
    public static async Task RunAsync(int width, int height, CancellationToken token)
    {
        WindowObserver.CheckSize(width, height);
        var encoded = VideoGeometry.EncodedSize(width, height);
        var properties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)width, (uint)height);
        properties.FrameRate.Numerator = 60;
        properties.FrameRate.Denominator = 1;
        var source = new MediaStreamSource(new VideoStreamDescriptor(properties)) { BufferTime = TimeSpan.Zero, CanSeek = false };
        var pixels = new byte[checked(width * height * 4)];
        var index = 0;
        source.Starting += (_, args) => args.Request.SetActualStartPosition(TimeSpan.Zero);
        source.SampleRequested += (_, args) =>
        {
            if (index >= 2 || token.IsCancellationRequested) { args.Request.Sample = null; return; }
            using var writer = new DataWriter();
            writer.WriteBytes(pixels);
            var sample = MediaStreamSample.CreateFromBuffer(writer.DetachBuffer(), TimeSpan.FromMilliseconds(index++ * 17));
            sample.Duration = TimeSpan.FromMilliseconds(17);
            args.Request.Sample = sample;
        };
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
        profile.Audio = null;
        profile.Video.Width = (uint)encoded.Width;
        profile.Video.Height = (uint)encoded.Height;
        profile.Video.FrameRate.Numerator = 60;
        profile.Video.FrameRate.Denominator = 1;
        profile.Video.Bitrate = 4_000_000;
        using var stream = new InMemoryRandomAccessStream();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var transcoder = new MediaTranscoder { HardwareAccelerationEnabled = true };
        var prepared = await transcoder.PrepareMediaStreamSourceTranscodeAsync(source, stream, profile).AsTask(timeout.Token);
        if (!prepared.CanTranscode) throw new InvalidOperationException("Windows encoder unavailable: " + prepared.FailureReason);
        await prepared.TranscodeAsync().AsTask(timeout.Token);
        token.ThrowIfCancellationRequested();
    }
}
