using System.Buffers.Binary;
using NAudio.Wave;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class AudioRecognizerTests
{
    private static byte[] Floats(params float[] values)
    {
        var bytes = new byte[values.Length * sizeof(float)];
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * sizeof(float)), BitConverter.SingleToInt32Bits(values[i]));
        return bytes;
    }

    [Fact]
    public void FloatStereoUsesTheDeclaredFormatInsteadOfTreatingItsBytesAsPcm16()
    {
        var samples = Floats(0.01f, -0.75f);
        Assert.True(AudioRecognizer.TryReadPeak(samples, WaveFormat.CreateIeeeFloatWaveFormat(48000, 2), out var peak));
        Assert.Equal(0.75, peak, 6);
        Assert.True(AudioRecognizer.TryReadPeak(samples, new WaveFormatExtensible(48000, 32, 2), out peak));
        Assert.Equal(0.75, peak, 6);
    }

    [Fact]
    public void PcmIntegerWidthsHandleSignedMinimumAndUnsignedEightBit()
    {
        var cases = new[] {
            (Format: new WaveFormat(48000, 8, 1), Samples: new byte[] { 128, 0 }),
            (Format: new WaveFormat(48000, 16, 1), Samples: new byte[] { 0, 128 }),
            (Format: new WaveFormat(48000, 24, 1), Samples: new byte[] { 0, 0, 128 }),
            (Format: new WaveFormat(48000, 32, 1), Samples: new byte[] { 0, 0, 0, 128 }) };
        foreach (var item in cases)
        {
            Assert.True(AudioRecognizer.TryReadPeak(item.Samples, item.Format, out var peak));
            Assert.Equal(1, peak);
        }
        Assert.True(AudioRecognizer.TryReadPeak(new byte[] { 0, 128 }, new WaveFormatExtensible(48000, 16, 1), out var extensiblePeak));
        Assert.Equal(1, extensiblePeak);
    }

    [Fact]
    public void SilenceDoesNotCreateAmplitudeAndFiniteFloatOverrangeIsClamped()
    {
        Assert.True(AudioRecognizer.TryReadPeak(Floats(0, 0), WaveFormat.CreateIeeeFloatWaveFormat(48000, 2), out var peak));
        Assert.Equal(0, peak);
        Assert.True(AudioRecognizer.TryReadPeak(Floats(-2), WaveFormat.CreateIeeeFloatWaveFormat(48000, 1), out peak));
        Assert.Equal(1, peak);
    }

    [Fact]
    public void MalformedUnsupportedAndNonfinitePacketsFailClosed()
    {
        Assert.False(AudioRecognizer.TryReadPeak(Array.Empty<byte>(), new WaveFormat(48000, 16, 1), out _));
        Assert.False(AudioRecognizer.TryReadPeak(new byte[] { 1 }, new WaveFormat(48000, 16, 1), out _));
        Assert.False(AudioRecognizer.TryReadPeak(new byte[] { 1, 2 }, new WaveFormat(48000, 16, 2), out _));
        Assert.False(AudioRecognizer.TryReadPeak(new byte[] { 1, 2 }, null, out _));
        Assert.False(AudioRecognizer.TryReadPeak(new byte[] { 1, 2 }, new WaveFormat(48000, 12, 1), out _));
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Assert.False(AudioRecognizer.TryReadPeak(Floats(0.5f, value), WaveFormat.CreateIeeeFloatWaveFormat(48000, 2), out var peak));
            Assert.Equal(0, peak);
        }
    }

    [Fact]
    public void CooldownUsesMonotonicElapsedTimeAndRejectsInvalidOrBackwardsTime()
    {
        Assert.True(AudioRecognizer.CooldownReady(0, null));
        Assert.False(AudioRecognizer.CooldownReady(1999, 0));
        Assert.True(AudioRecognizer.CooldownReady(2000, 0));
        Assert.False(AudioRecognizer.CooldownReady(999, 1000));
        Assert.False(AudioRecognizer.CooldownReady(double.NaN, null));
        Assert.False(AudioRecognizer.CooldownReady(10000, double.PositiveInfinity));
        Assert.False(AudioRecognizer.CooldownReady(-1, null));
    }

    [Fact]
    public void FailedLoopbackRemainsOffAndNeverRequestsAnotherCaptureSource()
    {
        var source = new FakeCapture { FailStart = true };
        var factoryCalls = 0;
        using var recognizer = new AudioRecognizer(() => { factoryCalls++; return source; });
        Assert.False(recognizer.IsRunning);
        Assert.Equal(0, factoryCalls);
        recognizer.Start();
        Assert.False(recognizer.IsRunning);
        Assert.True(source.Disposed);
        Assert.Equal(1, source.StartCount);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public void FakeCaptureUsesItsFormatAndLatePacketsAfterStopAreIgnored()
    {
        var source = new FakeCapture();
        using var recognizer = new AudioRecognizer(() => source);
        var cues = 0;
        recognizer.CueDetected += _ => cues++;
        recognizer.Start();
        source.Emit(Floats(0.75f, -0.75f));
        Assert.Equal(1, cues);
        recognizer.Stop();
        Assert.False(recognizer.IsRunning);
        source.Emit(Floats(1, 1));
        Assert.Equal(1, cues);
    }

    private sealed class FakeCapture : IWaveIn
    {
        public bool FailStart { get; init; }
        public bool Disposed { get; private set; }
        public int StartCount { get; private set; }
        public WaveFormat WaveFormat { get; set; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        public event EventHandler<WaveInEventArgs>? DataAvailable;
        public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public void StartRecording()
        {
            StartCount++;
            if (FailStart) throw new InvalidOperationException("Synthetic unavailable loopback.");
        }
        public void StopRecording() => RecordingStopped?.Invoke(this, new StoppedEventArgs());
        public void Emit(byte[] bytes) => DataAvailable?.Invoke(this, new WaveInEventArgs(bytes, bytes.Length));
        public void Dispose() => Disposed = true;
    }
}
