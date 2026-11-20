using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceAudioObservation(double CandidateQpcMs, double AvailableQpcMs,
    string Status, string? Direction, double Similarity, double Margin, bool Emitted, double? ProcessedQpcMs = null);

internal sealed record PetDanceAudioFeatures(string Status, double[] Descriptor, double Rms,
    double NoveltyFraction, double BackgroundSimilarity, double PeakHz);

/// <summary>
/// Bounded, single-consumer PCM analysis. Candidates are discovered on an independent
/// 50 ms grid; no visual timestamps, game state, input or device APIs are accepted.
/// Run on a bounded consumer, never on the audio capture callback. Drop/gap recovery
/// must call Reset; it invalidates pending evidence rather than joining missing audio.
/// A reported direction is experimental sound evidence, not authority to play it.
/// </summary>
internal sealed class PetDanceAudioDetector
{
    public const string FeatureVersion = "spectral-novelty-v2-boundaries";
    public const int SampleRate = 44100;
    public const int Channels = 2;
    public const int MaximumPacketFrames = SampleRate / 4;
    public const int DescriptorLength = 84;
    private const int BaselineFrames = 5292; // 120 ms
    private const int LookBehindFrames = 8820; // 200 ms
    private const int SoundFrames = 9702; // 220 ms
    private const int HopFrames = 2205; // 50 ms
    private const int RingFrames = SampleRate;
    private const long MaximumLifetimeFrames = 210L * SampleRate;
    private static readonly string[] Directions = ["Down", "Left", "Right", "Up"];
    private readonly Dictionary<string, double[]> _templates;
    private readonly short[] _ring = new short[RingFrames * Channels];
    private readonly double[] _sound = new double[SoundFrames * Channels];
    private readonly double[] _baseline = new double[BaselineFrames * Channels];
    private readonly SpectralWorkspace _spectral = new();
    private long _segmentFrames;
    private long _lifetimeFrames;
    private long _nextCandidateFrame = LookBehindFrames;
    private double? _segmentQpcMs;
    private double? _lastPacketQpcMs;
    private string? _pendingDirection;
    private int _pendingWindows;
    private int _quietWindows;
    private bool _armed = true;
    private double? _lastEmissionQpcMs;
    private bool _expired;

    public string LastStatus { get; private set; } = "warming-up";
    public string? TemplateSourceSha256 { get; private set; }
    public long AnalyzedWindows { get; private set; }
    public long EmittedCount { get; private set; }
    public long Discontinuities { get; private set; }

    public PetDanceAudioDetector(IReadOnlyDictionary<string, double[]> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        if (templates.Count != Directions.Length || Directions.Any(d => !templates.ContainsKey(d)))
            throw new ArgumentException("Exactly four named direction templates are required.", nameof(templates));
        _templates = new(StringComparer.Ordinal);
        foreach (var direction in Directions)
        {
            var vector = templates[direction];
            if (vector == null || vector.Length != DescriptorLength || vector.Any(v => !double.IsFinite(v) || v < 0))
                throw new ArgumentException("Templates need 84 finite nonnegative bins.", nameof(templates));
            var length = Math.Sqrt(vector.Sum(v => v * v));
            if (!double.IsFinite(length) || Math.Abs(length - 1) > 0.001)
                throw new ArgumentException("Frozen templates must already be unit length.", nameof(templates));
            _templates.Add(direction, (double[])vector.Clone());
        }
    }

    /// <summary>Accept only the bounded, versioned frozen comparison artifact; never fit on a live stream.</summary>
    public static PetDanceAudioDetector LoadFrozenComparison(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("An absolute comparison path is required.", nameof(path));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 262144) throw new InvalidDataException("Comparison exceeds its 256 KiB budget.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        var root = json.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1 ||
            root.GetProperty("kind").GetString() != "experimental-four-direction-whole-round-evaluation" ||
            root.GetProperty("feature_version").GetString() != FeatureVersion)
            throw new InvalidDataException("Unsupported frozen audio comparison.");
        var templates = new Dictionary<string, double[]>(StringComparer.Ordinal);
        foreach (var property in root.GetProperty("evaluation").GetProperty("templates").EnumerateObject())
        {
            if (!templates.TryAdd(property.Name, property.Value.GetProperty("descriptor").EnumerateArray().Select(v => v.GetDouble()).ToArray()))
                throw new InvalidDataException("Duplicate direction template.");
        }
        return new PetDanceAudioDetector(templates) { TemplateSourceSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() };
    }

    /// <summary>Invalidates pending windows/occurrences. Does not reset the lifetime budget or counters.</summary>
    public void Reset(string reason = "reset")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        _segmentFrames = 0;
        _nextCandidateFrame = LookBehindFrames;
        _segmentQpcMs = null;
        _pendingDirection = null;
        _pendingWindows = 0;
        _quietWindows = 0;
        _armed = true;
        _lastEmissionQpcMs = null;
        LastStatus = _expired ? "analysis-duration-limit" : reason;
    }

    /// <summary>PCM16 little-endian stereo, 44100 Hz. Source QPC is packet-first-frame time in 100 ns units.</summary>
    public IReadOnlyList<PetDanceAudioObservation> Push(ProcessAudioPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var at = packet.QpcPosition100Ns / 10000.0;
        var observations = new List<PetDanceAudioObservation>(8);
        if (_expired) return [Status(at, "analysis-duration-limit")];
        if (packet.PcmBytes == null || packet.FrameCount <= 0 || packet.FrameCount > MaximumPacketFrames ||
            packet.PcmBytes.Length != packet.FrameCount * 4)
        {
            Invalidate(at, "invalid-pcm-packet", observations);
            return observations;
        }
        if ((packet.Flags & ~7u) != 0 || (packet.Flags & 4) != 0)
        {
            Invalidate(at, (packet.Flags & 4) != 0 ? "timestamp-error" : "unsupported-packet-flags", observations);
            return observations;
        }
        if (_lastPacketQpcMs.HasValue && at <= _lastPacketQpcMs.Value)
        {
            Invalidate(at, "nonmonotonic-packet-time", observations);
            return observations;
        }
        if ((packet.Flags & 1) != 0)
        {
            _lastPacketQpcMs = at;
            Invalidate(at, "packet-discontinuity", observations);
            return observations;
        }
        if (_lifetimeFrames + packet.FrameCount > MaximumLifetimeFrames)
        {
            _expired = true;
            Reset("analysis-duration-limit");
            return [Status(at, "analysis-duration-limit")];
        }
        var expected = _segmentQpcMs + _segmentFrames * 1000.0 / SampleRate;
        if (expected.HasValue && Math.Abs(at - expected.Value) > 2)
            Invalidate(at, at > expected.Value ? "clock-gap" : "clock-overlap", observations);
        _lastPacketQpcMs = at;
        _segmentQpcMs ??= at;
        _lifetimeFrames += packet.FrameCount;
        var silent = (packet.Flags & 2) != 0;
        for (var frame = 0; frame < packet.FrameCount; frame++)
        {
            var index = (int)((_segmentFrames + frame) % RingFrames) * Channels;
            _ring[index] = silent ? (short)0 : BinaryPrimitives.ReadInt16LittleEndian(packet.PcmBytes.AsSpan(frame * 4, 2));
            _ring[index + 1] = silent ? (short)0 : BinaryPrimitives.ReadInt16LittleEndian(packet.PcmBytes.AsSpan(frame * 4 + 2, 2));
        }
        _segmentFrames += packet.FrameCount;
        var available = at + packet.FrameCount * 1000.0 / SampleRate;
        while (_nextCandidateFrame + SoundFrames <= _segmentFrames)
        {
            CopySamples(_nextCandidateFrame, SoundFrames, _sound);
            CopySamples(_nextCandidateFrame - LookBehindFrames, BaselineFrames, _baseline);
            var feature = _spectral.Describe(_sound, SoundFrames, _baseline, BaselineFrames);
            var candidate = _segmentQpcMs.Value + _nextCandidateFrame * 1000.0 / SampleRate;
            observations.Add(Classify(feature, candidate, available));
            AnalyzedWindows++;
            _nextCandidateFrame += HopFrames;
        }
        if (observations.Count == 0 && _segmentFrames < LookBehindFrames + SoundFrames) LastStatus = "warming-up";
        return observations;
    }

    private void Invalidate(double at, string reason, List<PetDanceAudioObservation> observations)
    {
        Discontinuities++;
        Reset(reason);
        observations.Add(Status(at, reason));
    }

    private PetDanceAudioObservation Status(double at, string reason)
    {
        LastStatus = reason;
        return new(at, at, reason, null, 0, 0, false);
    }

    private void CopySamples(long start, int frames, double[] destination)
    {
        if (start < 0 || start < _segmentFrames - RingFrames || start + frames > _segmentFrames)
            throw new InvalidOperationException("Audio window fell outside the bounded history.");
        for (var frame = 0; frame < frames; frame++)
        {
            var index = (int)((start + frame) % RingFrames) * Channels;
            destination[frame * 2] = _ring[index] / 32768.0;
            destination[frame * 2 + 1] = _ring[index + 1] / 32768.0;
        }
    }

    private PetDanceAudioObservation Classify(PetDanceAudioFeatures feature, double candidate, double available)
    {
        string? bestDirection = null;
        double best = 0, second = 0;
        var status = feature.Status;
        if (status == "ok")
        {
            foreach (var direction in Directions)
            {
                double score = 0;
                for (var i = 0; i < DescriptorLength; i++) score += feature.Descriptor[i] * _templates[direction][i];
                if (score > best) { second = best; best = score; bestDirection = direction; }
                else if (score > second) second = score;
            }
            if (best < 0.8 || best - second < 0.08)
            {
                status = "low_similarity_or_ambiguous_templates";
                bestDirection = null;
            }
        }
        var emitted = false;
        if (bestDirection == null)
        {
            _pendingDirection = null;
            _pendingWindows = 0;
            _quietWindows++;
            if (_quietWindows >= 3) _armed = true;
        }
        else
        {
            _quietWindows = 0;
            _pendingWindows = _pendingDirection == bestDirection ? _pendingWindows + 1 : 1;
            _pendingDirection = bestDirection;
            if (_armed && _pendingWindows >= 2 && (!_lastEmissionQpcMs.HasValue || candidate - _lastEmissionQpcMs.Value >= 250))
            {
                emitted = true;
                _armed = false;
                _lastEmissionQpcMs = candidate;
                EmittedCount++;
            }
            status = emitted ? "cue-candidate" : _armed ? "confirming-candidate" : "occurrence-held";
        }
        LastStatus = status;
        return new(candidate, available, status, bestDirection, best, best - second, emitted);
    }

    /// <summary>Pure feature seam for synthetic/golden parity tests, not a live calibration API.</summary>
    internal static PetDanceAudioFeatures DescribePcm16(ReadOnlySpan<byte> sound, ReadOnlySpan<byte> baseline)
    {
        if (sound.Length != SoundFrames * 4 || baseline.Length != BaselineFrames * 4)
            throw new ArgumentException("Feature windows must contain 220 ms of sound and 120 ms of baseline.");
        static double[] Decode(ReadOnlySpan<byte> bytes)
        {
            var values = new double[bytes.Length / 2];
            for (var i = 0; i < values.Length; i++) values[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(i * 2, 2)) / 32768.0;
            return values;
        }
        return new SpectralWorkspace().Describe(Decode(sound), SoundFrames, Decode(baseline), BaselineFrames);
    }

    // Double-precision radix-2 FFT avoids new dependencies and matches the offline
    // unnormalized NumPy transform. Scratch storage is reused for all live windows.
    private sealed class SpectralWorkspace
    {
        private const int FftSize = 8192, WindowSize = 2048, Bins = FftSize / 2 + 1;
        private readonly double[] _real = new double[FftSize], _imaginary = new double[FftSize];
        private readonly double[] _cos = new double[FftSize / 2], _sin = new double[FftSize / 2];
        private readonly int[] _reverse = new int[FftSize], _bands = new int[Bins];
        private readonly double[] _window = new double[WindowSize], _power = new double[Bins], _background = new double[Bins];
        private readonly int[] _peaks = new int[Bins];
        private readonly double _windowPower;
        private const int FirstBin = 15, LastBin = 1486; // inclusive 80..8000 Hz at 44100/8192

        public SpectralWorkspace()
        {
            for (var i = 0; i < FftSize; i++)
            {
                var value = i;
                for (var bit = 0; bit < 13; bit++) { _reverse[i] = (_reverse[i] << 1) | (value & 1); value >>= 1; }
            }
            for (var i = 0; i < _cos.Length; i++) { _cos[i] = Math.Cos(-2 * Math.PI * i / FftSize); _sin[i] = Math.Sin(-2 * Math.PI * i / FftSize); }
            for (var i = 0; i < WindowSize; i++)
            {
                _window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (WindowSize - 1));
                _windowPower += _window[i] * _window[i];
            }
            Array.Fill(_bands, -1);
            for (var i = FirstBin; i <= LastBin; i++)
            {
                var hz = i * (double)SampleRate / FftSize;
                for (var band = 0; band < DescriptorLength; band++)
                    if (hz >= 80 * Math.Pow(100, band / 84.0) && hz < 80 * Math.Pow(100, (band + 1) / 84.0)) { _bands[i] = band; break; }
            }
        }

        public PetDanceAudioFeatures Describe(double[] sound, int frames, double[] baseline, int baseFrames)
        {
            double squares = 0;
            foreach (var value in sound) squares += value * value;
            var rms = Math.Sqrt(squares / (frames * Channels));
            if (rms < 1e-4) return new("silence", new double[DescriptorLength], rms, 0, 0, 0);
            Spectrum(sound, frames, _power);
            Spectrum(baseline, baseFrames, _background);
            var unfiltered = 0;
            for (var i = 1; i < Bins; i++) if (_power[i] > _power[unfiltered]) unfiltered = i;
            var outside = unfiltered < FirstBin || unfiltered > LastBin;
            double total = 0, noveltySum = 0, aNorm = 0, bNorm = 0, dot = 0;
            var peak = FirstBin;
            var descriptor = new double[DescriptorLength];
            for (var i = 0; i < Bins; i++)
            {
                if (i < FirstBin || i > LastBin) { _power[i] = 0; _background[i] = 0; continue; }
                if (_power[i] > _power[peak]) peak = i;
                total += _power[i];
                var novelty = Math.Max(0, _power[i] - _background[i]);
                noveltySum += novelty;
                aNorm += _power[i] * _power[i];
                bNorm += _background[i] * _background[i];
                dot += _power[i] * _background[i];
                if (_bands[i] >= 0) descriptor[_bands[i]] += novelty;
            }
            var fraction = Math.Min(1, noveltySum / Math.Max(total, 1e-20));
            var norm = Math.Sqrt(aNorm) * Math.Sqrt(bNorm);
            var similarity = norm > 0 ? dot / norm : 0;
            var count = 0;
            for (var i = 1; i < Bins - 1; i++) if (_power[i] > _power[i - 1] && _power[i] >= _power[i + 1]) _peaks[count++] = i;
            Array.Sort(_peaks, 0, count, Comparer<int>.Create((a, b) => _power[b].CompareTo(_power[a])));
            Span<int> chosen = stackalloc int[4];
            var chosenCount = 0;
            for (var i = 0; i < count && chosenCount < 4; i++)
            {
                var candidate = _peaks[i];
                if (_power[candidate] < _power[peak] * 0.25) break;
                var separated = true;
                for (var j = 0; j < chosenCount; j++) if (Math.Abs(candidate - chosen[j]) * (double)SampleRate / FftSize < 40) separated = false;
                if (separated) chosen[chosenCount++] = candidate;
            }
            var ambiguous = false;
            for (var i = 1; i < chosenCount; i++)
            {
                var ratio = Math.Max(peak, chosen[i]) / (double)Math.Min(peak, chosen[i]);
                if (_power[chosen[i]] >= _power[peak] * 0.45 && Math.Abs(ratio - Math.Round(ratio)) > 0.04) ambiguous = true;
            }
            double lengthSquared = 0;
            for (var i = 0; i < descriptor.Length; i++) { descriptor[i] = Math.Sqrt(descriptor[i]); lengthSquared += descriptor[i] * descriptor[i]; }
            if (lengthSquared > 0)
            {
                var length = Math.Sqrt(lengthSquared);
                for (var i = 0; i < descriptor.Length; i++) descriptor[i] /= length;
            }
            var status = outside || peak == FirstBin || peak == LastBin ? "out_of_band_or_boundary_peak" :
                ambiguous ? "mixed_or_harmonic_ambiguity" : fraction < 0.15 && similarity > 0.9 ? "no_local_change_music_or_held_sound" : "ok";
            return new(status, descriptor, rms, fraction, similarity, peak * (double)SampleRate / FftSize);
        }

        private void Spectrum(double[] samples, int frames, double[] power)
        {
            Array.Clear(power);
            Span<double> means = stackalloc double[2];
            means.Clear();
            var anySignal = false;
            for (var frame = 0; frame < frames; frame++)
                for (var channel = 0; channel < Channels; channel++)
                {
                    var value = samples[frame * Channels + channel];
                    means[channel] += value;
                    anySignal |= value != 0;
                }
            if (!anySignal) return;
            means[0] /= frames; means[1] /= frames;
            var windows = 0;
            for (var start = 0; start + WindowSize <= frames; start += WindowSize / 2)
            {
                windows++;
                for (var channel = 0; channel < Channels; channel++)
                {
                    Array.Clear(_real); Array.Clear(_imaginary);
                    for (var i = 0; i < WindowSize; i++) _real[_reverse[i]] = (samples[(start + i) * Channels + channel] - means[channel]) * _window[i];
                    for (var size = 2; size <= FftSize; size *= 2)
                    {
                        var half = size / 2;
                        var stride = FftSize / size;
                        for (var block = 0; block < FftSize; block += size)
                            for (var j = 0; j < half; j++)
                            {
                                var a = block + j; var b = a + half; var twiddle = j * stride;
                                var real = _real[b] * _cos[twiddle] - _imaginary[b] * _sin[twiddle];
                                var imaginary = _real[b] * _sin[twiddle] + _imaginary[b] * _cos[twiddle];
                                _real[b] = _real[a] - real; _imaginary[b] = _imaginary[a] - imaginary;
                                _real[a] += real; _imaginary[a] += imaginary;
                            }
                    }
                    for (var i = 0; i < Bins; i++) power[i] += _real[i] * _real[i] + _imaginary[i] * _imaginary[i];
                }
            }
            var scale = windows * Channels * _windowPower;
            for (var i = 0; i < Bins; i++) power[i] /= scale;
        }
    }
}
