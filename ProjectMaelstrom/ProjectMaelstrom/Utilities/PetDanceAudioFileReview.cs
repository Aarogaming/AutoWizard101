using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NAudio.Wave;

namespace ProjectMaelstrom.Utilities;

/// <summary>Offline-only bounded replay of finalized audio packets. No device, game or input APIs.</summary>
internal static class PetDanceAudioFileReview
{
    private const int MaximumTimingBytes = 24 * 1024 * 1024;
    private const long MaximumWaveBytes = 84L * 1024 * 1024;
    private sealed record StoredPacket(long Offset, int Frames, ulong Qpc, ulong Device, uint Flags);

    /// <summary>
    /// Validate hashes, stored target provenance, PCM format and exact packet coverage before
    /// scanning. Publish a new report atomically; existing outputs are never overwritten.
    /// Stored identity is not compared with a current process, and no visual labels are read.
    /// </summary>
    public static object Run(string wavPath, string timingPath, string comparisonPath, string outputPath)
    {
        foreach (var path in new[] { wavPath, timingPath, comparisonPath, outputPath })
            if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Audio review paths must be absolute.");
        if (File.Exists(outputPath) || Directory.Exists(outputPath)) throw new IOException("Choose a new audio review output.");
        var clock = Stopwatch.StartNew();
        var detector = PetDanceAudioDetector.LoadFrozenComparison(comparisonPath);
        using var timingStream = new FileStream(timingPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (timingStream.Length is <= 0 or > MaximumTimingBytes) throw new InvalidDataException("Timing report exceeds its 24 MiB limit.");
        var timingBytes = new byte[(int)timingStream.Length];
        timingStream.ReadExactly(timingBytes);
        using var json = JsonDocument.Parse(timingBytes, new JsonDocumentOptions { MaxDepth = 32 });
        var root = json.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1 || root.GetProperty("kind").GetString() != "selected-process-audio" ||
            root.GetProperty("scope").GetString() != "selected-process-tree" || root.GetProperty("status").GetString() != "complete" ||
            !root.GetProperty("finalized").GetBoolean() || root.GetProperty("microphone").GetBoolean() || root.GetProperty("system_mix_fallback").GetBoolean() ||
            root.GetProperty("wav_file").GetString() != Path.GetFileName(wavPath))
            throw new InvalidDataException("A finalized selected-process WAV without microphone or mix fallback is required.");
        var pid = root.GetProperty("target_process_id").GetInt32();
        var startedTicks = root.GetProperty("target_process_start_utc_ticks").GetInt64();
        if (pid <= 0 || startedTicks <= 0 || startedTicks > DateTime.MaxValue.Ticks)
            throw new InvalidDataException("Stored target identity is missing or invalid.");
        var drops = root.GetProperty("queue_dropped").GetInt64();
        if (drops < 0) throw new InvalidDataException("Invalid queue-drop count.");
        var format = root.GetProperty("format");
        if (format.GetProperty("sample_rate").GetInt32() != 44100 || format.GetProperty("channels").GetInt32() != 2 ||
            format.GetProperty("bits_per_sample").GetInt32() != 16 || format.GetProperty("encoding").GetString() != "pcm16")
            throw new InvalidDataException("Detector requires PCM16 stereo at 44100 Hz.");

        using var source = new FileStream(wavPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length is <= 0 or > MaximumWaveBytes) throw new InvalidDataException("WAV exceeds its 84 MiB limit.");
        var waveHash = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
        if (!string.Equals(waveHash, root.GetProperty("wav_sha256").GetString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("WAV hash disagrees with its timing report.");
        source.Position = 0;
        using var wave = new WaveFileReader(source);
        if (wave.WaveFormat.Encoding != WaveFormatEncoding.Pcm || wave.WaveFormat.SampleRate != 44100 ||
            wave.WaveFormat.Channels != 2 || wave.WaveFormat.BitsPerSample != 16 || wave.WaveFormat.BlockAlign != 4 || wave.Length % 4 != 0)
            throw new InvalidDataException("WAV header disagrees with its declared PCM format.");
        var frames = wave.Length / 4;
        if (frames <= 0 || frames > 210L * 44100 || root.GetProperty("frames").GetInt64() != frames)
            throw new InvalidDataException("WAV must contain positive audio lasting at most 210 seconds.");
        var packetArray = root.GetProperty("packets");
        if (packetArray.ValueKind != JsonValueKind.Array || packetArray.GetArrayLength() is <= 0 or > 22000)
            throw new InvalidDataException("Audio packet count is outside its limit.");
        var packets = new List<StoredPacket>(packetArray.GetArrayLength());
        long expected = 0;
        double? firstGood = null, lastGoodEnd = null;
        foreach (var row in packetArray.EnumerateArray())
        {
            var offset = row.GetProperty("frame_offset").GetInt64();
            var count = row.GetProperty("frame_count").GetInt32();
            var qpc = row.GetProperty("qpc_position_100ns").GetUInt64();
            var milliseconds = row.GetProperty("qpc_ms").GetDouble();
            var device = row.GetProperty("device_position").GetUInt64();
            var flags = row.GetProperty("flags").GetUInt32();
            if (offset != expected || count <= 0 || count > PetDanceAudioDetector.MaximumPacketFrames || offset + count > frames)
                throw new InvalidDataException("Packets must cover the WAV contiguously with bounded sizes.");
            if ((flags & ~7u) != 0 || !double.IsFinite(milliseconds) || Math.Abs(milliseconds - qpc / 10000.0) > 0.001 ||
                row.GetProperty("silent").GetBoolean() != ((flags & 2) != 0) ||
                row.GetProperty("discontinuity").GetBoolean() != ((flags & 1) != 0) ||
                row.GetProperty("timestamp_error").GetBoolean() != ((flags & 4) != 0))
                throw new InvalidDataException("Packet timing units or flag fields disagree.");
            if ((flags & 5) == 0)
            {
                if (lastGoodEnd.HasValue && milliseconds < lastGoodEnd.Value - 2)
                    throw new InvalidDataException("Packet clocks overlap or move backwards.");
                firstGood ??= milliseconds;
                lastGoodEnd = milliseconds + count * 1000.0 / 44100;
                if (lastGoodEnd - firstGood > 210000) throw new InvalidDataException("Audio source clock span exceeds 210 seconds.");
            }
            else lastGoodEnd = null;
            expected += count;
            packets.Add(new(offset, count, qpc, device, flags));
        }
        if (expected != frames) throw new InvalidDataException("Packet metadata does not cover the complete WAV.");
        var observations = new List<PetDanceAudioObservation>();
        foreach (var packet in packets)
        {
            var bytes = new byte[packet.Frames * 4];
            wave.ReadExactly(bytes);
            if ((packet.Flags & 2) != 0 && bytes.Any(b => b != 0)) throw new InvalidDataException("A silent packet contains nonzero samples.");
            observations.AddRange(detector.Push(new(bytes, packet.Frames, packet.Device, packet.Qpc, packet.Flags)));
            if (observations.Count > 27000) throw new InvalidDataException("Analysis observation limit exceeded.");
        }
        var report = new
        {
            schema_version = 1,
            kind = "native-continuous-pet-dance-audio-review",
            created_utc = DateTime.UtcNow,
            feature_version = PetDanceAudioDetector.FeatureVersion,
            detector_contract = "independent-50ms-two-confirm-three-rearm-250ms-v1",
            source_wav = Path.GetFileName(wavPath),
            wav_sha256 = waveHash,
            audio_timing_sha256 = Convert.ToHexString(SHA256.HashData(timingBytes)).ToLowerInvariant(),
            comparison_sha256 = detector.TemplateSourceSha256,
            target_process_id = pid,
            target_process_start_utc_ticks = startedTicks,
            stored_identity_validated = true,
            live_identity_checked = false,
            frames,
            packet_count = packets.Count,
            queue_dropped = drops,
            analyzed_windows = detector.AnalyzedWindows,
            emitted_candidates = detector.EmittedCount,
            invalidations = detector.Discontinuities,
            observations,
            processing_elapsed_ms = clock.Elapsed.TotalMilliseconds,
            analysis_completion_qpc_measured = false,
            visual_onsets_used_for_detection = false,
            watch_phase_corroborated = false,
            gameplay_inputs_sent = 0,
            live_gameplay_qualified = false,
            note = "Audio candidates only. Candidate time is the confirming scan anchor; availability is source packet end, not measured compute completion. Missing/flagged spans invalidate history. No visual state, controls or live device were used."
        };
        var directory = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".audio-review-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(output, report, new JsonSerializerOptions { WriteIndented = true });
            File.Move(temporary, outputPath, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return report;
    }
}
