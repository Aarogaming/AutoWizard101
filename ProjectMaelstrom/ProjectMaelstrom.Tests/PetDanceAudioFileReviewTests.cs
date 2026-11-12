using System.Security.Cryptography;
using System.Text.Json;
using NAudio.Wave;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceAudioFileReviewTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Maelstrom-Audio-Review-" + Guid.NewGuid().ToString("N"));
    private string WavePath => Path.Combine(_folder, "game-audio.wav");
    private string TimingPath => Path.Combine(_folder, "audio-timing.json");
    private string ComparisonPath => Path.Combine(_folder, "comparison.json");
    private string OutputPath => Path.Combine(_folder, "review.json");

    public PetDanceAudioFileReviewTests()
    {
        Directory.CreateDirectory(_folder);
        using (var writer = new WaveFileWriter(WavePath, new WaveFormat(44100, 16, 2))) writer.Write(new byte[882 * 4]);
        var templates = new Dictionary<string, object>();
        var names = new[] { "Up", "Down", "Left", "Right" };
        for (var i = 0; i < names.Length; i++)
        {
            var vector = new double[84];
            vector[i] = 1;
            templates[names[i]] = new { descriptor = vector };
        }
        File.WriteAllText(ComparisonPath, JsonSerializer.Serialize(new
        {
            schema_version = 1,
            kind = "experimental-four-direction-whole-round-evaluation",
            feature_version = PetDanceAudioDetector.FeatureVersion,
            evaluation = new { templates }
        }));
        WriteTiming();
    }

    private void WriteTiming(int pid = 123, int secondOffset = 441, string? hash = null, bool silentMismatch = false)
    {
        File.WriteAllText(TimingPath, JsonSerializer.Serialize(new
        {
            schema_version = 1,
            kind = "selected-process-audio",
            scope = "selected-process-tree",
            status = "complete",
            finalized = true,
            microphone = false,
            system_mix_fallback = false,
            wav_file = "game-audio.wav",
            wav_sha256 = hash ?? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(WavePath))),
            target_process_id = pid,
            target_process_start_utc_ticks = 638000000000000000L,
            queue_dropped = 0,
            format = new { sample_rate = 44100, channels = 2, bits_per_sample = 16, encoding = "pcm16" },
            frames = 882,
            packets = new[]
            {
                new { frame_offset = 0, frame_count = 441, qpc_position_100ns = 10000000UL, qpc_ms = 1000, device_position = 0, flags = 2, silent = true, discontinuity = false, timestamp_error = false },
                new { frame_offset = secondOffset, frame_count = 441, qpc_position_100ns = 10100000UL, qpc_ms = 1010, device_position = 0, flags = 2, silent = !silentMismatch, discontinuity = false, timestamp_error = false }
            }
        }));
    }

    [Fact]
    public void ValidFinalizedSilentRecordingProducesProvenanceWithoutClaimingCues()
    {
        PetDanceAudioFileReview.Run(WavePath, TimingPath, ComparisonPath, OutputPath);
        using var json = JsonDocument.Parse(File.ReadAllText(OutputPath));
        var root = json.RootElement;
        Assert.Equal("native-continuous-pet-dance-audio-review", root.GetProperty("kind").GetString());
        Assert.Equal(64, root.GetProperty("comparison_sha256").GetString()!.Length);
        Assert.True(root.GetProperty("stored_identity_validated").GetBoolean());
        Assert.False(root.GetProperty("live_identity_checked").GetBoolean());
        Assert.Equal(0, root.GetProperty("analyzed_windows").GetInt64());
        Assert.Equal(0, root.GetProperty("emitted_candidates").GetInt64());
        Assert.Equal(0, root.GetProperty("gameplay_inputs_sent").GetInt32());
        Assert.False(root.GetProperty("live_gameplay_qualified").GetBoolean());
        Assert.False(root.GetProperty("visual_onsets_used_for_detection").GetBoolean());
        Assert.False(root.GetProperty("analysis_completion_qpc_measured").GetBoolean());
    }

    [Fact]
    public void WavHashMismatchFailsBeforePublishing()
    {
        WriteTiming(hash: new string('0', 64));
        Assert.Throws<InvalidDataException>(() => PetDanceAudioFileReview.Run(WavePath, TimingPath, ComparisonPath, OutputPath));
        Assert.False(File.Exists(OutputPath));
    }

    [Fact]
    public void MissingStoredTargetIdentityFailsBeforePublishing()
    {
        WriteTiming(pid: 0);
        Assert.Throws<InvalidDataException>(() => PetDanceAudioFileReview.Run(WavePath, TimingPath, ComparisonPath, OutputPath));
        Assert.False(File.Exists(OutputPath));
    }

    [Fact]
    public void NoncontiguousPacketMapFailsBeforePublishing()
    {
        WriteTiming(secondOffset: 442);
        Assert.Throws<InvalidDataException>(() => PetDanceAudioFileReview.Run(WavePath, TimingPath, ComparisonPath, OutputPath));
        Assert.False(File.Exists(OutputPath));
    }

    [Fact]
    public void InconsistentSilentFlagFailsBeforePublishing()
    {
        WriteTiming(silentMismatch: true);
        Assert.Throws<InvalidDataException>(() => PetDanceAudioFileReview.Run(WavePath, TimingPath, ComparisonPath, OutputPath));
        Assert.False(File.Exists(OutputPath));
    }

    [Fact]
    public void ExistingOutputAndRelativePathsAreNotAccepted()
    {
        File.WriteAllText(OutputPath, "preserve previous review");
        Assert.Throws<IOException>(() => PetDanceAudioFileReview.Run(WavePath, TimingPath, ComparisonPath, OutputPath));
        Assert.Equal("preserve previous review", File.ReadAllText(OutputPath));
        Assert.Throws<ArgumentException>(() => PetDanceAudioFileReview.Run("game-audio.wav", TimingPath, ComparisonPath, OutputPath));
        Assert.Empty(Directory.GetFiles(_folder, ".audio-review-*.tmp"));
    }

    public void Dispose() => Directory.Delete(_folder, true);
}
