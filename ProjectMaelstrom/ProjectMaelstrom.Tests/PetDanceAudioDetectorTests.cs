using System.Buffers.Binary;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceAudioDetectorTests
{
    private const int Rate = 44100;
    private static readonly double[] Frequencies = [697, 997, 1399, 1801];
    private static readonly string[] Directions = ["Up", "Down", "Left", "Right"];
    private static readonly Lazy<Dictionary<string, double[]>> Templates = new(() => Directions
        .Select((name, index) => (name, vector: PetDanceAudioDetector.DescribePcm16(Tone(220, Frequencies[index]), Tone(120, 0)).Descriptor))
        .ToDictionary(row => row.name, row => row.vector));

    private static byte[] Tone(int milliseconds, double hz, bool invertRight = false)
        => Samples(Rate * milliseconds / 1000, n => hz == 0 ? 0 : 0.1 * Math.Sin(2 * Math.PI * hz * n / Rate), invertRight);

    private static byte[] Samples(int frames, Func<int, double> signal, bool invertRight = false)
    {
        var bytes = new byte[frames * 4];
        for (var frame = 0; frame < frames; frame++)
        {
            var sample = (short)Math.Round(Math.Clamp(signal(frame), -1, 1) * 32767);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(frame * 4, 2), sample);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(frame * 4 + 2, 2), invertRight ? (short)-sample : sample);
        }
        return bytes;
    }

    private static ProcessAudioPacket Packet(byte[] bytes, double atMs, uint flags = 0)
        => new(bytes, bytes.Length / 4, 0, checked((ulong)Math.Round(atMs * 10000)), flags);

    private static List<PetDanceAudioObservation> Feed(PetDanceAudioDetector detector, byte[] bytes, int chunkMs = 50, double originMs = 1000)
    {
        var output = new List<PetDanceAudioObservation>();
        var chunkFrames = Rate * chunkMs / 1000;
        for (var frame = 0; frame < bytes.Length / 4; frame += chunkFrames)
        {
            var frames = Math.Min(chunkFrames, bytes.Length / 4 - frame);
            output.AddRange(detector.Push(Packet(bytes.AsSpan(frame * 4, frames * 4).ToArray(), originMs + frame * 1000.0 / Rate)));
        }
        return output;
    }

    [Fact]
    public void FeatureMatchesIndependentNumpyGoldenInsteadOfARefittedTemplate()
    {
        // Independent Python v2 golden: identical PCM16 stereo, 697 Hz, 0.1 amplitude,
        // 9702 event frames, 5292 zero baseline frames. Values are frozen, not fitted here.
        var feature = PetDanceAudioDetector.DescribePcm16(Tone(220, 697), Tone(120, 0));
        Assert.Equal("ok", feature.Status);
        Assert.Equal(694.44580078125, feature.PeakHz, 9);
        Assert.Equal(1, feature.NoveltyFraction, 9);
        Assert.Equal(-23.00885263615, 20 * Math.Log10(feature.Rms), 8);
        Assert.Equal(0.9276979767781398, feature.Descriptor[39], 8);
        Assert.Equal(0.3170691154626364, feature.Descriptor[38], 8);
        Assert.Equal(0.19674596805609768, feature.Descriptor[40], 8);
        Assert.Equal(0.009636385499840722, feature.Descriptor[37], 8);
        Assert.Equal(0.005744643263040404, feature.Descriptor[41], 8);
        Assert.Equal(1, feature.Descriptor.Sum(v => v * v), 9);
    }

    [Fact]
    public void OppositeStereoPhaseDoesNotEraseASound()
    {
        var mono = PetDanceAudioDetector.DescribePcm16(Tone(220, 697), Tone(120, 0));
        var inverted = PetDanceAudioDetector.DescribePcm16(Tone(220, 697, true), Tone(120, 0));
        Assert.Equal(mono.Status, inverted.Status);
        for (var i = 0; i < 84; i++) Assert.Equal(mono.Descriptor[i], inverted.Descriptor[i], 10);
    }

    [Fact]
    public void SilenceHeldToneLowBassAndMixedSoundsRemainExplicit()
    {
        Assert.Equal("silence", PetDanceAudioDetector.DescribePcm16(Tone(220, 0), Tone(120, 0)).Status);
        Assert.Equal("no_local_change_music_or_held_sound", PetDanceAudioDetector.DescribePcm16(Tone(220, 697), Tone(120, 697)).Status);
        Assert.Equal("out_of_band_or_boundary_peak", PetDanceAudioDetector.DescribePcm16(Tone(220, 35), Tone(120, 0)).Status);
        var mixed = Samples(Rate * 220 / 1000, n => 0.1 * Math.Sin(2 * Math.PI * 697 * n / Rate) + 0.1 * Math.Sin(2 * Math.PI * 997 * n / Rate));
        Assert.Equal("mixed_or_harmonic_ambiguity", PetDanceAudioDetector.DescribePcm16(mixed, Tone(120, 0)).Status);
    }

    [Fact]
    public void RepeatedSameDirectionRequiresRearmAndHasNoVisualOnsetInput()
    {
        var detector = new PetDanceAudioDetector(Templates.Value);
        var stream = Samples(Rate * 2, n =>
        {
            var time = n * 1000.0 / Rate;
            return time is >= 500 and < 800 or >= 1300 and < 1600 ? 0.1 * Math.Sin(2 * Math.PI * 697 * n / Rate) : 0;
        });
        var observations = Feed(detector, stream);
        var cues = observations.Where(o => o.Emitted).ToArray();
        Assert.Equal(2, cues.Length);
        Assert.All(cues, cue => Assert.Equal("Up", cue.Direction));
        Assert.All(cues, cue => Assert.True(cue.AvailableQpcMs >= cue.CandidateQpcMs + 220 - 0.001));
        Assert.True(cues[1].CandidateQpcMs - cues[0].CandidateQpcMs >= 250);
        Assert.Contains(observations, o => o.Status == "silence" && !o.Emitted);
        Assert.Equal(2, detector.EmittedCount);
    }

    [Fact]
    public void ALongHeldSoundDoesNotProduceRepeatedOccurrences()
    {
        var stream = Samples(Rate * 3 / 2, n => n < Rate / 2 ? 0 : 0.1 * Math.Sin(2 * Math.PI * 697 * n / Rate));
        var detector = new PetDanceAudioDetector(Templates.Value);
        Assert.Single(Feed(detector, stream), o => o.Emitted);
        Assert.Contains(detector.LastStatus, new[] { "no_local_change_music_or_held_sound", "low_similarity_or_ambiguous_templates" });
    }

    [Fact]
    public void AmbiguousTemplatesCannotInventADirection()
    {
        var same = Templates.Value["Up"];
        var detector = new PetDanceAudioDetector(Directions.ToDictionary(d => d, _ => same));
        var stream = Samples(Rate, n => n < Rate / 2 ? 0 : 0.1 * Math.Sin(2 * Math.PI * 697 * n / Rate));
        var output = Feed(detector, stream);
        Assert.DoesNotContain(output, o => o.Emitted);
        Assert.Contains(output, o => o.Status == "low_similarity_or_ambiguous_templates" && o.Direction == null);
    }

    [Fact]
    public void PacketChunkingDoesNotChangeIndependentGridPredictions()
    {
        var stream = Samples(Rate, n => n < Rate / 2 ? 0 : 0.1 * Math.Sin(2 * Math.PI * 697 * n / Rate));
        var small = Feed(new PetDanceAudioDetector(Templates.Value), stream, 10);
        var large = Feed(new PetDanceAudioDetector(Templates.Value), stream, 250);
        Assert.Equal(small.Select(o => (o.CandidateQpcMs, o.Direction, o.Emitted)), large.Select(o => (o.CandidateQpcMs, o.Direction, o.Emitted)));
        Assert.All(large, o => Assert.True(o.AvailableQpcMs >= o.CandidateQpcMs + 220));
    }

    [Theory]
    [InlineData(1, "packet-discontinuity")]
    [InlineData(4, "timestamp-error")]
    [InlineData(8, "unsupported-packet-flags")]
    public void FlaggedPacketsInvalidateHistoryAndCannotSupplyACue(uint flags, string expected)
    {
        var detector = new PetDanceAudioDetector(Templates.Value);
        Assert.Empty(detector.Push(Packet(Tone(200, 0), 1000)));
        var diagnostic = Assert.Single(detector.Push(Packet(Tone(200, 697), 1200, flags)));
        Assert.Equal(expected, diagnostic.Status);
        Assert.False(diagnostic.Emitted);
        Assert.Empty(detector.Push(Packet(Tone(200, 697), 1400)));
        Assert.Equal(0, detector.AnalyzedWindows);
    }

    [Fact]
    public void GapsOverlapAndLatePacketsCannotJoinOldWindows()
    {
        var detector = new PetDanceAudioDetector(Templates.Value);
        detector.Push(Packet(Tone(200, 0), 1000));
        Assert.Equal("clock-gap", Assert.Single(detector.Push(Packet(Tone(200, 697), 1500))).Status);
        Assert.Equal("clock-overlap", Assert.Single(detector.Push(Packet(Tone(100, 697), 1600))).Status);
        Assert.Equal("nonmonotonic-packet-time", Assert.Single(detector.Push(Packet(Tone(100, 697), 1590))).Status);
        Assert.Equal(0, detector.AnalyzedWindows);
        Assert.Equal(3, detector.Discontinuities);
    }

    [Fact]
    public void SilentFlagUsesSilenceRatherThanTheUnusedBufferContents()
    {
        var detector = new PetDanceAudioDetector(Templates.Value);
        var results = new List<PetDanceAudioObservation>();
        for (var i = 0; i < 3; i++) results.AddRange(detector.Push(Packet(Tone(200, 697), 1000 + i * 200, 2)));
        Assert.NotEmpty(results);
        Assert.All(results, o => Assert.Equal("silence", o.Status));
        Assert.Equal(0, detector.EmittedCount);
    }

    [Fact]
    public void InvalidOrOversizedPacketsFailClosedAndResetWarmup()
    {
        var detector = new PetDanceAudioDetector(Templates.Value);
        detector.Push(Packet(Tone(200, 0), 1000));
        Assert.Equal("invalid-pcm-packet", Assert.Single(detector.Push(new ProcessAudioPacket(new byte[3], 1, 0, 12000000, 0))).Status);
        Assert.Equal("invalid-pcm-packet", Assert.Single(detector.Push(Packet(Tone(251, 697), 1400))).Status);
        Assert.Empty(detector.Push(Packet(Tone(200, 697), 1700)));
        Assert.Throws<ArgumentException>(() => PetDanceAudioDetector.DescribePcm16(new byte[4], new byte[4]));
    }

    [Fact]
    public void ResetInvalidatesPendingEvidenceWithoutRefittingTemplates()
    {
        var detector = new PetDanceAudioDetector(Templates.Value);
        detector.Push(Packet(Tone(250, 697), 1000));
        detector.Reset("analysis-queue-gap");
        Assert.Equal("analysis-queue-gap", detector.LastStatus);
        Assert.Empty(detector.Push(Packet(Tone(250, 697), 1500)));
        Assert.Equal(0, detector.AnalyzedWindows);
    }

    [Fact]
    public void TemplateInputsAreBoundedVersionedAndDefensivelyCopied()
    {
        Assert.Throws<ArgumentException>(() => new PetDanceAudioDetector(new Dictionary<string, double[]>()));
        var malformed = Templates.Value.ToDictionary(pair => pair.Key, pair => (double[])pair.Value.Clone());
        malformed["Up"][0] = double.NaN;
        Assert.Throws<ArgumentException>(() => new PetDanceAudioDetector(malformed));
        malformed["Up"] = new double[84];
        Assert.Throws<ArgumentException>(() => new PetDanceAudioDetector(malformed));
        malformed["Up"] = [1];
        Assert.Throws<ArgumentException>(() => new PetDanceAudioDetector(malformed));

        var folder = Path.Combine(Path.GetTempPath(), "Maelstrom-Audio-Detector-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "comparison.json");
            var root = new
            {
                schema_version = 1,
                kind = "experimental-four-direction-whole-round-evaluation",
                feature_version = PetDanceAudioDetector.FeatureVersion,
                evaluation = new { templates = Templates.Value.ToDictionary(pair => pair.Key, pair => new { descriptor = pair.Value }) }
            };
            File.WriteAllText(path, JsonSerializer.Serialize(root));
            var loaded = PetDanceAudioDetector.LoadFrozenComparison(path);
            Assert.Equal(64, loaded.TemplateSourceSha256!.Length);
            File.WriteAllText(path, JsonSerializer.Serialize(root).Replace(PetDanceAudioDetector.FeatureVersion, "unknown-feature"));
            Assert.Throws<InvalidDataException>(() => PetDanceAudioDetector.LoadFrozenComparison(path));
            File.WriteAllText(path, new string(' ', 262145));
            Assert.Throws<InvalidDataException>(() => PetDanceAudioDetector.LoadFrozenComparison(path));
            Assert.Throws<ArgumentException>(() => PetDanceAudioDetector.LoadFrozenComparison("comparison.json"));
        }
        finally { Directory.Delete(folder, true); }
    }
}
