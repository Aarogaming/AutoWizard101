using System.Buffers.Binary;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class ProcessAudioVerificationTests
{
    private const int Rate = 48000;
    private const double Target = 697, Other = 1209;

    private static byte[] Samples(double targetAmplitude, double otherAmplitude, int channels = 1, bool invertSecond = false, double phase = 0)
    {
        const int frames = Rate / 5;
        var bytes = new byte[frames * channels * sizeof(short)];
        for (var frame = 0; frame < frames; frame++)
        {
            var sample = targetAmplitude * Math.Sin(2 * Math.PI * Target * frame / Rate + phase) +
                otherAmplitude * Math.Sin(2 * Math.PI * Other * frame / Rate - phase);
            for (var channel = 0; channel < channels; channel++)
            {
                var sign = invertSecond && channel == 1 ? -1 : 1;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan((frame * channels + channel) * sizeof(short)), (short)Math.Round(sample * 32767 * sign));
            }
        }
        return bytes;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.23)]
    public void SelectedToneIsPresentWithoutInventingUnrelatedAudio(double phase)
    {
        var check = ProcessAudioVerification.AnalyzePcm16(Samples(0.03, 0, phase: phase), Rate, 1, Target, Other);
        Assert.True(check.IsolationPassed);
        Assert.True(check.TargetPresent);
        Assert.True(check.UnrelatedBelowThreshold);
        Assert.False(check.Silent);
        Assert.InRange(check.TargetAmplitude, 0.029, 0.031);
        Assert.InRange(check.UnrelatedAmplitude, 0, 0.0001);
        Assert.Equal(200, check.DurationMs);
        Assert.Equal(Rate / 5, check.SampleFrames);
    }

    [Fact]
    public void ASystemMixFailsTheUnrelatedToneGate()
    {
        var check = ProcessAudioVerification.AnalyzePcm16(Samples(0.03, 0.03), Rate, 1, Target, Other);
        Assert.True(check.TargetPresent);
        Assert.False(check.UnrelatedBelowThreshold);
        Assert.False(check.IsolationPassed);
        Assert.InRange(check.UnrelatedAmplitude, 0.029, 0.031);
    }

    [Fact]
    public void OnlyUnrelatedAudioCannotPassAsSelectedCapture()
    {
        var check = ProcessAudioVerification.AnalyzePcm16(Samples(0, 0.03), Rate, 1, Target, Other);
        Assert.False(check.TargetPresent);
        Assert.False(check.UnrelatedBelowThreshold);
        Assert.False(check.IsolationPassed);
    }

    [Fact]
    public void StereoOppositePhaseCannotCancelTheEvidence()
    {
        var check = ProcessAudioVerification.AnalyzePcm16(Samples(0.03, 0.03, 2, true), Rate, 2, Target, Other);
        Assert.True(check.TargetPresent);
        Assert.False(check.UnrelatedBelowThreshold);
        Assert.False(check.IsolationPassed);
    }

    [Fact]
    public void ActualSilentSamplesAreReportedAsSilenceNotIsolationSuccess()
    {
        var check = ProcessAudioVerification.AnalyzePcm16(Samples(0, 0), Rate, 1, Target, Other);
        Assert.True(check.Silent);
        Assert.False(check.TargetPresent);
        Assert.True(check.UnrelatedBelowThreshold);
        Assert.False(check.IsolationPassed);
        Assert.Equal(0, check.Rms);
        Assert.Equal(0, check.Peak);
    }

    [Fact]
    public void SilenceInOneChannelCannotHideALeakInTheOther()
    {
        var mono = Samples(0.03, 0.03);
        var stereo = new byte[mono.Length * 2];
        for (var i = 0; i < mono.Length; i += 2) mono.AsSpan(i, 2).CopyTo(stereo.AsSpan(i * 2 + 2, 2));
        var check = ProcessAudioVerification.AnalyzePcm16(stereo, Rate, 2, Target, Other);
        Assert.True(check.TargetPresent);
        Assert.False(check.UnrelatedBelowThreshold);
    }

    [Fact]
    public void MissingTruncatedOrUnsupportedInputCannotClaimSilenceOrSuccess()
    {
        Assert.Throws<ArgumentException>(() => ProcessAudioVerification.AnalyzePcm16([], Rate, 1, Target, Other));
        Assert.Throws<ArgumentException>(() => ProcessAudioVerification.AnalyzePcm16(new byte[3], Rate, 1, Target, Other));
        Assert.Throws<ArgumentException>(() => ProcessAudioVerification.AnalyzePcm16(new byte[200], Rate, 2, Target, Other));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProcessAudioVerification.AnalyzePcm16(Samples(0, 0), Rate, 0, Target, Other));
        Assert.Throws<ArgumentException>(() => ProcessAudioVerification.AnalyzePcm16(Samples(0, 0), Rate, 1, Target, Target));
        Assert.Throws<ArgumentException>(() => ProcessAudioVerification.AnalyzePcm16(Samples(0, 0), Rate, 1, double.NaN, Other));
    }

    [Theory]
    [InlineData(-25.125)]
    [InlineData(0)]
    [InlineData(1234.5678)]
    public void QpcTimelineRoundtripPreservesSubMillisecondAndPreOriginOffsets(double milliseconds)
    {
        const ulong origin = 12_345_678_901_234;
        var qpc = ProcessAudioVerification.ElapsedMsToQpc100ns(milliseconds, origin);
        Assert.InRange(Math.Abs(ProcessAudioVerification.Qpc100nsToElapsedMs(qpc, origin) - milliseconds), 0, 0.000051);
    }

    [Fact]
    public void QpcConversionRejectsInvalidOffsetsAndOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProcessAudioVerification.ElapsedMsToQpc100ns(double.NaN, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProcessAudioVerification.ElapsedMsToQpc100ns(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProcessAudioVerification.ElapsedMsToQpc100ns(1, ulong.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProcessAudioVerification.ElapsedMsToQpc100ns(86_400_001, 0));
    }

    [Fact]
    public void ImpossibleDevicePositionRangeCannotWrapIntoApparentlyValidTimeline()
    {
        var timeline = new GameAudioPacketTimeline(4);
        var packet = new ProcessAudioPacket(new byte[441 * 4], 441, ulong.MaxValue - 100, 10_000_000, 0);
        Assert.Throws<InvalidDataException>(() => timeline.Append(packet));
        Assert.Empty(timeline.Packets);
        Assert.Equal(0, timeline.Frames);
    }
}
