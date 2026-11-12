using System.Buffers.Binary;
using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record VisualPrediction(string? State, double Similarity, double Margin, string Reason);
internal sealed record VisualExample(string State, string Hash, int Width, int Height, byte[] Features, Rectangle? Region = null);

/// <summary>Versioned, order-independent full-frame exemplar matcher. Never dispatches actions.</summary>
internal sealed class VisualStateRecognizer
{
    internal const double MinimumSimilarity = .94;
    internal const double MinimumMargin = .025;
    internal const string Algorithm = "rgb-grid-32x24-mae-fixed-region-v2";
    private readonly VisualExample[] _examples;
    public string PackName { get; }
    public string PackHash { get; }
    public int ExampleCount => _examples.Length;
    public int StateCount => _examples.Select(e => e.State).Distinct().Count();
    internal VisualStateRecognizer Snapshot() => new(PackName, PackHash, _examples.Select(e => e with { Features = e.Features.ToArray() }));
    internal void SaveSnapshot(string path) => GameExperiments.WriteAtomic(path, new { algorithm = Algorithm, PackName, PackHash, examples = _examples });
    public VisualStateRecognizer(string packName, string packHash, IEnumerable<VisualExample> examples)
    {
        PackName = packName;
        PackHash = packHash;
        _examples = examples.ToArray();
        if (_examples.Length == 0) throw new InvalidDataException("This pack has no labeled image examples. Record and label frames first.");
        if (StateCount < 2) throw new InvalidDataException("Teach at least two different states so the recognizer can compare alternatives.");
        if (_examples.GroupBy(e => (e.Hash, e.Region)).Any(g => g.Select(e => e.State).Distinct().Count() > 1))
            throw new InvalidDataException("The same image has conflicting labels. Correct the examples before learning.");
        _examples = _examples.DistinctBy(e => (e.Hash, e.Region)).ToArray();
    }

    public static VisualStateRecognizer Learn(string packPath)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(packPath))!;
        var packBytes = ReadBounded(packPath, 1_048_576);
        using var pack = JsonDocument.Parse(packBytes);
        var data = pack.RootElement;
        if (data.GetProperty("schema_version").GetInt32() is not (1 or 2) || data.GetProperty("status").GetString() != "draft")
            throw new InvalidDataException("Choose a supported draft teaching pack.");
        var examples = new List<VisualExample>();
        long total = 0;
        var fixtures = data.GetProperty("fixtures").EnumerateArray().ToArray();
        if (fixtures.Length > 16) throw new InvalidDataException("Too many example sessions.");
        foreach (var fixture in fixtures)
        {
            var sessionPath = LocalPath(root, fixture.GetString()!);
            using var session = JsonDocument.Parse(ReadBounded(sessionPath, 1_048_576));
            if (session.RootElement.GetProperty("schema_version").GetInt32() != 1) throw new InvalidDataException("Unsupported example session.");
            var sessionRoot = Path.GetDirectoryName(sessionPath)!;
            var eventBytes = ReadBounded(LocalPath(sessionRoot, session.RootElement.GetProperty("events_file").GetString()!), 16 * 1_048_576);
            using var reader = new StringReader(System.Text.Encoding.UTF8.GetString(eventBytes));
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var entry = JsonDocument.Parse(line);
                if (entry.RootElement.GetProperty("kind").GetString() != "observation") continue;
                var payload = entry.RootElement.GetProperty("payload");
                if (!payload.TryGetProperty("image", out var image)) continue;
                var state = payload.GetProperty("state").GetString();
                if (string.IsNullOrWhiteSpace(state) || state.Length > 120 || payload.GetProperty("label_source").GetString() != "human")
                    throw new InvalidDataException("Examples must have human state labels.");
                if (examples.Count >= 240) throw new InvalidDataException("This recognizer supports at most 240 examples.");
                var bytes = ReadBounded(LocalPath(sessionRoot, image.GetString()!), 64 * 1_048_576);
                total += bytes.Length;
                if (total > TeachingRecording.MaximumPackImageBytes) throw new InvalidDataException("Examples exceed 128 MiB.");
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                if (!string.Equals(hash, payload.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("An example image changed. Re-export the teaching pack.");
                if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new InvalidDataException("Example must be a PNG.");
                var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
                var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
                WindowObserver.CheckSize(width, height);
                if (width != payload.GetProperty("width").GetInt32() || height != payload.GetProperty("height").GetInt32()) throw new InvalidDataException("Example dimensions changed.");
                using var stream = new MemoryStream(bytes);
                using var bitmap = new Bitmap(stream);
                Rectangle? region = null;
                if (payload.TryGetProperty("region", out var regionJson) && regionJson.ValueKind != JsonValueKind.Null)
                {
                    var r = regionJson.EnumerateArray().Select(v => v.GetInt32()).ToArray();
                    if (r.Length != 4 || r[0] < 0 || r[1] < 0 || r[2] < 4 || r[3] < 4 || (long)r[0] + r[2] > width || (long)r[1] + r[3] > height) throw new InvalidDataException("Invalid template region.");
                    region = new Rectangle(r[0], r[1], r[2], r[3]);
                }
                using var crop = region.HasValue ? bitmap.Clone(region.Value, bitmap.PixelFormat) : null;
                examples.Add(new(state, hash, width, height, Features(crop ?? bitmap), region));
            }
        }
        return new(data.GetProperty("title").GetString() + " · " + data.GetProperty("version").GetString(), Convert.ToHexString(SHA256.HashData(packBytes)), examples);
    }

    public VisualPrediction Predict(Bitmap image)
    {
        var compatible = _examples.Where(e => Math.Abs((double)e.Width / e.Height / ((double)image.Width / image.Height) - 1) <= .02).ToArray();
        if (compatible.Select(e => e.State).Distinct().Count() < 2) return new(null, 0, 0, "Game shape differs from the examples. Record examples at this window shape.");
        var scores = compatible.GroupBy(e => e.State).Select(g => (State: g.Key, Score: g.Max(e =>
        {
            if (e.Region is not Rectangle region) return Similarity(Features(image), e.Features);
            var scaled = new Rectangle((int)Math.Round((double)region.X * image.Width / e.Width), (int)Math.Round((double)region.Y * image.Height / e.Height), Math.Max(1, (int)Math.Round((double)region.Width * image.Width / e.Width)), Math.Max(1, (int)Math.Round((double)region.Height * image.Height / e.Height)));
            scaled.Intersect(new Rectangle(0, 0, image.Width, image.Height));
            using var cropped = image.Clone(scaled, image.PixelFormat);
            return Similarity(Features(cropped), e.Features);
        })))
            .OrderByDescending(e => e.Score).ToArray();
        var best = scores[0];
        var margin = best.Score - scores[1].Score;
        if (best.Score < MinimumSimilarity) return new(null, best.Score, margin, "No close visual match. Teach more examples.");
        if (margin < MinimumMargin) return new(null, best.Score, margin, "Examples are visually ambiguous at this scale.");
        return new(best.State, best.Score, margin, "Closest labeled example; experimental fixed-region or whole-frame match.");
    }

    public object Evaluate(TeachingRecording recording)
    {
        var rows = new List<object>();
        var trainHashes = _examples.Select(e => e.Hash).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int labeled = 0, overlap = 0, correct = 0, unknown = 0;
        var frames = recording.Frames;
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            if (string.IsNullOrWhiteSpace(frame.State)) continue;
            if (trainHashes.Contains(frame.Sha256)) { overlap++; continue; }
            using var bitmap = recording.ReadFrame(i);
            var prediction = Predict(bitmap);
            labeled++;
            if (prediction.State == frame.State) correct++;
            if (prediction.State == null) unknown++;
            rows.Add(new { frame.File, expected = frame.State, predicted = prediction.State, prediction.Similarity, prediction.Margin, prediction.Reason });
        }
        return new
        {
            algorithm = Algorithm,
            pack = PackName,
            pack_sha256 = PackHash,
            training_image_hashes = trainHashes.Order().ToArray(),
            minimum_similarity = MinimumSimilarity,
            minimum_margin = MinimumMargin,
            tested = labeled,
            excluded_training_duplicates = overlap,
            correct,
            unknown,
            incorrect = labeled - correct - unknown,
            results = rows,
            qualification = "observe_only_not_gameplay_qualified",
            note = "Exact training images excluded. Adjacent frames may still be near-duplicates; use a separate recording for meaningful evaluation."
        };
    }

    internal static byte[] Features(Bitmap image)
    {
        using var small = new Bitmap(32, 24);
        using (var graphics = Graphics.FromImage(small))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(image, new Rectangle(0, 0, 32, 24));
        }
        var result = new byte[32 * 24 * 3];
        var index = 0;
        for (var y = 0; y < 24; y++) for (var x = 0; x < 32; x++)
        {
            var pixel = small.GetPixel(x, y);
            result[index++] = pixel.R; result[index++] = pixel.G; result[index++] = pixel.B;
        }
        return result;
    }

    internal static double Similarity(byte[] left, byte[] right)
    {
        long difference = 0;
        for (var i = 0; i < left.Length; i++) difference += Math.Abs(left[i] - right[i]);
        return 1 - difference / (255.0 * left.Length);
    }

    private static byte[] ReadBounded(string path, long maximum)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > maximum) throw new InvalidDataException("Teaching data exceeds its size limit.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static string LocalPath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':') || relative.Split('/', '\\').Any(p => p is ".." or "." or ""))
            throw new InvalidDataException("Example paths must stay inside the pack.");
        var path = root;
        foreach (var part in relative.Split('/', '\\'))
        {
            path = Path.Combine(path, part);
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked example files are not supported.");
        }
        return path;
    }
}
