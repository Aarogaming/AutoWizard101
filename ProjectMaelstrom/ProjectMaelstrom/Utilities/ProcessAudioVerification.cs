using System.Buffers.Binary;

namespace ProjectMaelstrom.Utilities;

internal sealed record ProcessAudioSignalCheck(int SampleFrames, double DurationMs, double Rms, double Peak,
    double TargetAmplitude, double UnrelatedAmplitude, double MinimumTargetAmplitude,
    double MaximumUnrelatedAmplitude, bool TargetPresent, bool UnrelatedBelowThreshold, bool Silent)
{
    public bool IsolationPassed => TargetPresent && UnrelatedBelowThreshold && !Silent;
}

/// <summary>Offline checks for controlled test tones, not a gameplay/audio cue classifier.
/// Amplitudes are fractions of full scale. Below threshold is not proof of absolute absence.</summary>
internal static class ProcessAudioVerification
{
    public static ProcessAudioSignalCheck AnalyzePcm16(ReadOnlySpan<byte> pcm, int sampleRate, int channels,
        double targetHz, double unrelatedHz)
    {
        if (sampleRate is < 8000 or > 192000 || channels is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Use bounded PCM16 sample rate and channel count.");
        if (!double.IsFinite(targetHz) || !double.IsFinite(unrelatedHz) || targetHz < 50 || unrelatedHz < 50 ||
            targetHz >= sampleRate / 2.0 || unrelatedHz >= sampleRate / 2.0 || Math.Abs(targetHz - unrelatedHz) < 50)
            throw new ArgumentException("Use two distinct finite test frequencies below Nyquist.");
        var blockAlign = channels * sizeof(short);
        if (pcm.Length == 0 || pcm.Length > 16 * 1024 * 1024 || pcm.Length % blockAlign != 0)
            throw new ArgumentException("Supply a bounded whole-frame PCM16 payload, without a WAV header.", nameof(pcm));
        var frames = pcm.Length / blockAlign;
        if (frames < sampleRate / 10 || frames > sampleRate * 10)
            throw new ArgumentException("Analyze 0.1 to 10 seconds of actual recorded samples.", nameof(pcm));

        var targetCoefficient = 2 * Math.Cos(2 * Math.PI * targetHz / sampleRate);
        var unrelatedCoefficient = 2 * Math.Cos(2 * Math.PI * unrelatedHz / sampleRate);
        var targetPrevious = new double[channels];
        var targetPrevious2 = new double[channels];
        var otherPrevious = new double[channels];
        var otherPrevious2 = new double[channels];
        double windowSum = 0, sumSquares = 0, peak = 0;
        for (var frame = 0; frame < frames; frame++)
        {
            // Hann weighting reduces leakage when capture starts midway through a tone.
            var window = 0.5 - 0.5 * Math.Cos(2 * Math.PI * frame / (frames - 1));
            windowSum += window;
            for (var channel = 0; channel < channels; channel++)
            {
                var offset = frame * blockAlign + channel * sizeof(short);
                var sample = BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(offset, sizeof(short))) / 32768.0;
                peak = Math.Max(peak, Math.Abs(sample));
                sumSquares += sample * sample;
                var weighted = sample * window;
                var target = weighted + targetCoefficient * targetPrevious[channel] - targetPrevious2[channel];
                targetPrevious2[channel] = targetPrevious[channel];
                targetPrevious[channel] = target;
                var other = weighted + unrelatedCoefficient * otherPrevious[channel] - otherPrevious2[channel];
                otherPrevious2[channel] = otherPrevious[channel];
                otherPrevious[channel] = other;
            }
        }

        double targetAmplitude = 0, unrelatedAmplitude = 0;
        for (var channel = 0; channel < channels; channel++)
        {
            targetAmplitude = Math.Max(targetAmplitude, Amplitude(targetPrevious[channel], targetPrevious2[channel], targetCoefficient, windowSum));
            unrelatedAmplitude = Math.Max(unrelatedAmplitude, Amplitude(otherPrevious[channel], otherPrevious2[channel], unrelatedCoefficient, windowSum));
        }
        var rms = Math.Sqrt(sumSquares / (frames * (double)channels));
        const double minimumTargetAmplitude = 0.0015;
        // Bound leakage both absolutely and relative to the observed target; allow two PCM16 LSBs.
        var maximumUnrelatedAmplitude = Math.Min(0.001, Math.Max(2.0 / 32768, targetAmplitude * 0.08));
        return new(frames, frames * 1000.0 / sampleRate, rms, peak, targetAmplitude, unrelatedAmplitude,
            minimumTargetAmplitude, maximumUnrelatedAmplitude, targetAmplitude >= minimumTargetAmplitude,
            unrelatedAmplitude <= maximumUnrelatedAmplitude, rms < 0.0001 && peak < 0.0005);
    }

    /// <summary>WASAPI packet QPC values already use 100ns units, not Stopwatch ticks.</summary>
    public static double Qpc100nsToElapsedMs(ulong packetQpc100ns, ulong originQpc100ns) =>
        packetQpc100ns >= originQpc100ns ? (packetQpc100ns - originQpc100ns) / 10000.0 :
            -(double)(originQpc100ns - packetQpc100ns) / 10000.0;

    public static ulong ElapsedMsToQpc100ns(double elapsedMs, ulong originQpc100ns)
    {
        if (!double.IsFinite(elapsedMs) || Math.Abs(elapsedMs) > 86_400_000)
            throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Bound the relative test timeline to one day.");
        var result = originQpc100ns + decimal.Round((decimal)elapsedMs * 10000, 0, MidpointRounding.AwayFromZero);
        if (result < 0 || result > ulong.MaxValue) throw new ArgumentOutOfRangeException(nameof(elapsedMs));
        return (ulong)result;
    }

    private static double Amplitude(double previous, double previous2, double coefficient, double windowSum) =>
        2 * Math.Sqrt(Math.Max(0, previous * previous + previous2 * previous2 - coefficient * previous * previous2)) / windowSum;
}
