using System.Diagnostics;
using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProjectMaelstrom.Utilities;

internal sealed record TeachingFrame(string File, double TimeMs, int Width, int Height, string Sha256, string State = "", int[]? Region = null);
internal sealed record TeachingManifest(int Version, string Id, string Workspace, string ProcessName, string StartedUtc, bool Complete, List<TeachingFrame> Frames);

/// <summary>Bounded local frame recording and manual labels. Contains no input or inference.</summary>
internal sealed class TeachingRecording
{
    internal const int MaximumFrames = 600;
    internal const long MaximumBytes = 1024L * 1024 * 1024;
    internal const long MaximumPackImageBytes = 128L * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly object _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TeachingManifest _manifest;
    private long _bytes;
    public string DirectoryPath { get; }
    public string Workspace => _manifest.Workspace;
    public IReadOnlyList<TeachingFrame> Frames { get { lock (_gate) return _manifest.Frames.ToArray(); } }
    public bool Complete { get { lock (_gate) return _manifest.Complete; } }

    private TeachingRecording(string path, TeachingManifest manifest) { DirectoryPath = path; _manifest = manifest; }

    public static TeachingRecording Start(string dataRoot, string workspace, string processName)
    {
        var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
        var path = Path.Combine(dataRoot, "recordings", id);
        Directory.CreateDirectory(path);
        var recording = new TeachingRecording(path, new(1, id, workspace, processName, DateTime.UtcNow.ToString("O"), false, new()));
        recording.Persist();
        return recording;
    }

    public bool AddFrame(Bitmap image)
    {
        lock (_gate) return AddFrameAt(image, _clock.Elapsed.TotalMilliseconds, minimumIntervalMs: 1000);
    }

    internal bool AddImportedFrame(Bitmap image, double approximateTimeMs)
        => AddFrameAt(image, approximateTimeMs, minimumIntervalMs: 0);

    private bool AddFrameAt(Bitmap image, double time, int minimumIntervalMs)
    {
        lock (_gate)
        {
            if (_manifest.Complete) return false;
            WindowObserver.CheckSize(image.Width, image.Height);
            if (!double.IsFinite(time) || time < 0 || (_manifest.Frames.Count > 0 && time < _manifest.Frames[^1].TimeMs))
                throw new InvalidDataException("Frame timestamps must be finite, non-negative, and in order.");
            if (_manifest.Frames.Count > 0 && time - _manifest.Frames[^1].TimeMs < minimumIntervalMs) return true;
            using var encoded = new MemoryStream();
            image.Save(encoded, ImageFormat.Png);
            if (_manifest.Frames.Count >= MaximumFrames || _bytes + encoded.Length > MaximumBytes)
            {
                Finish();
                return false;
            }
            var bytes = encoded.ToArray();
            var file = $"frame-{_manifest.Frames.Count:D4}.png";
            File.WriteAllBytes(Path.Combine(DirectoryPath, file), bytes);
            _manifest.Frames.Add(new(file, time, image.Width, image.Height, Convert.ToHexString(SHA256.HashData(bytes))));
            try { Persist(); }
            catch { _manifest.Frames.RemoveAt(_manifest.Frames.Count - 1); throw; }
            _bytes += bytes.Length;
            if (_manifest.Frames.Count == MaximumFrames) Finish();
            return !_manifest.Complete;
        }
    }

    public void Finish()
    {
        lock (_gate) { _manifest = _manifest with { Complete = true }; Persist(); }
    }

    public void Label(int index, string state)
    {
        state = state.Trim();
        if (state.Length > 120 || state.Any(char.IsControl)) throw new InvalidDataException("Use a state label of at most 120 characters without line breaks.");
        lock (_gate)
        {
            if (!_manifest.Complete) throw new InvalidOperationException("Stop recording before labeling.");
            var previous = _manifest.Frames[index];
            _manifest.Frames[index] = previous with { State = state };
            try { Persist(); } catch { _manifest.Frames[index] = previous; throw; }
        }
    }

    public Bitmap ReadFrame(int index)
    {
        lock (_gate)
        {
            var frame = _manifest.Frames[index];
            var bytes = ReadVerifiedFrame(frame);
            using var stream = new MemoryStream(bytes);
            using var image = new Bitmap(stream);
            if (image.Width != frame.Width || image.Height != frame.Height) throw new InvalidDataException("Frame dimensions do not match the recording.");
            return new Bitmap(image);
        }
    }

    public void SelectRegion(int index, Rectangle region)
    {
        lock (_gate)
        {
            if (!_manifest.Complete) throw new InvalidOperationException("Stop recording first.");
            var frame = _manifest.Frames[index];
            if (region.Width < 4 || region.Height < 4 || region.X < 0 || region.Y < 0 || region.Right > frame.Width || region.Bottom > frame.Height)
                throw new InvalidDataException("Template region must be inside the frame and at least 4 × 4 pixels.");
            _manifest.Frames[index] = frame with { Region = new[] { region.X, region.Y, region.Width, region.Height } };
            try { Persist(); } catch { _manifest.Frames[index] = frame; throw; }
        }
    }

    private byte[] ReadVerifiedFrame(TeachingFrame frame)
    {
        if (!Regex.IsMatch(frame.File, @"\Aframe-\d{4}\.png\z")) throw new InvalidDataException("Invalid frame filename.");
        var path = Path.Combine(DirectoryPath, frame.File);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).Length > MaximumBytes)
            throw new InvalidDataException("Frame is a link or exceeds the recording limit.");
        var bytes = File.ReadAllBytes(path);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != frame.Sha256) throw new InvalidDataException("Frame changed since capture; cannot use it as teaching evidence.");
        if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) != frame.Width || BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) != frame.Height)
            throw new InvalidDataException("PNG dimensions do not match the bounded recording metadata.");
        return bytes;
    }

    public static TeachingRecording Open(string manifestPath)
    {
        if (new FileInfo(manifestPath).Length > 1_048_576) throw new InvalidDataException("Recording manifest is too large.");
        var data = JsonSerializer.Deserialize<TeachingManifest>(File.ReadAllText(manifestPath)) ?? throw new InvalidDataException("Invalid recording.");
        if (data.Version != 1 || data.Frames == null || data.Frames.Count > MaximumFrames || string.IsNullOrWhiteSpace(data.Id)) throw new InvalidDataException("Unsupported recording.");
        double time = -1;
        var names = new HashSet<string>();
        foreach (var frame in data.Frames)
        {
            WindowObserver.CheckSize(frame.Width, frame.Height);
            if (frame.Region is { } r && (r.Length != 4 || r[0] < 0 || r[1] < 0 || r[2] < 4 || r[3] < 4 || (long)r[0] + r[2] > frame.Width || (long)r[1] + r[3] > frame.Height)) throw new InvalidDataException("Invalid template region.");
            if (!double.IsFinite(frame.TimeMs) || frame.TimeMs < 0 || frame.TimeMs < time || !names.Add(frame.File) || frame.State == null || frame.State.Length > 120)
                throw new InvalidDataException("Invalid recording frame metadata.");
            time = frame.TimeMs;
        }
        var recording = new TeachingRecording(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, data);
        foreach (var frame in data.Frames)
        {
            recording._bytes += recording.ReadVerifiedFrame(frame).Length;
            if (recording._bytes > MaximumBytes) throw new InvalidDataException("Recording exceeds 128 MiB.");
        }
        // A recovered recording is reviewable; it is never resumed automatically.
        recording._manifest = data with { Complete = true };
        return recording;
    }

    public string ExportPack(string dataRoot, string packId, string gameId, string title)
    {
        ValidateId(packId); ValidateId(gameId);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 120) throw new InvalidDataException("Enter a pack title (1–120 characters).");
        lock (_gate)
        {
            if (!_manifest.Complete) throw new InvalidOperationException("Stop recording before saving a pack.");
            var labeled = _manifest.Frames.Where(f => !string.IsNullOrWhiteSpace(f.State)).ToArray();
            if (labeled.Length == 0) throw new InvalidOperationException("Label at least one frame before saving a teaching pack.");
            long labeledBytes = 0;
            foreach (var frame in labeled)
            {
                labeledBytes += new FileInfo(Path.Combine(DirectoryPath, frame.File)).Length;
                if (labeledBytes > MaximumPackImageBytes) throw new InvalidOperationException("Labeled images exceed the 128 MiB pack limit. Leave some frames unlabeled or create multiple smaller packs; the recording is preserved.");
            }
            var version = "0.1.0-teaching-" + Guid.NewGuid().ToString("N")[..12];
            var parent = Path.Combine(dataRoot, "game-packs", packId);
            Directory.CreateDirectory(parent);
            var temporary = Path.Combine(parent, ".teaching-" + Guid.NewGuid().ToString("N"));
            var destination = Path.Combine(parent, version);
            Directory.CreateDirectory(temporary);
            try
            {
                var fixture = Path.Combine(temporary, "examples");
                Directory.CreateDirectory(fixture);
                var events = new List<string>();
                foreach (var frame in labeled)
                {
                    File.WriteAllBytes(Path.Combine(fixture, frame.File), ReadVerifiedFrame(frame));
                    events.Add(JsonSerializer.Serialize(new
                    {
                        event_id = "frame-" + events.Count,
                        kind = "observation",
                        sequence = events.Count,
                        time_ms = frame.TimeMs,
                        payload = new { state = frame.State, label_source = "human", image = frame.File, sha256 = frame.Sha256, width = frame.Width, height = frame.Height, region = frame.Region }
                    }));
                }
                File.WriteAllLines(Path.Combine(fixture, "events.jsonl"), events);
                File.WriteAllText(Path.Combine(fixture, "session.json"), JsonSerializer.Serialize(new { schema_version = 1, session_id = _manifest.Id, clock_origin = "recording_start_monotonic", events_file = "events.jsonl", source = "observe_manual_labels" }, Json));
                File.WriteAllText(Path.Combine(temporary, "pack.json"), JsonSerializer.Serialize(new { schema_version = 1, pack_id = packId, game_id = gameId, title, version, status = "draft", capabilities = Array.Empty<string>(), skills = Array.Empty<string>(), fixtures = new[] { "examples/session.json" }, dependencies = Array.Empty<string>() }, Json));
                Directory.Move(temporary, destination);
                return destination;
            }
            finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
        }
    }

    internal static void ValidateId(string id)
    {
        if (!Regex.IsMatch(id, @"\A[a-z0-9][a-z0-9._-]{0,63}\z") || id.EndsWith('.') || Regex.IsMatch(id.Split('.')[0], @"\A(con|prn|aux|nul|com[1-9]|lpt[1-9])\z"))
            throw new InvalidDataException("IDs use lowercase letters, digits, hyphens or underscores (up to 64 characters); Windows reserved names are not allowed.");
    }

    private void Persist()
    {
        var temporary = Path.Combine(DirectoryPath, "recording.tmp");
        File.WriteAllText(temporary, JsonSerializer.Serialize(_manifest, Json));
        File.Move(temporary, Path.Combine(DirectoryPath, "recording.json"), true);
    }
}
