using System.Buffers.Binary;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace ProjectMaelstrom.Utilities;

internal sealed record DanceAsset(string File, string Label, string Sha256);
internal sealed record DanceMatch(DanceCue Cue, double Score, double Margin);
internal sealed record DanceCalibration(int Version, int Width, int Height, int[] Region, double Scale,
    string NeutralPng, string NeutralSha256, string SourceFrameSha256);

internal static class PetDanceWork
{
    private static readonly SemaphoreSlim Worker = new(1, 1);
    public static IDisposable Acquire()
    {
        if (!Worker.Wait(0)) throw new InvalidOperationException("A Pet Dance review is already running. Stop or finish it first.");
        return new Lease();
    }
    private sealed class Lease : IDisposable
    {
        private bool _released;
        public void Dispose() { if (!_released) { _released = true; Worker.Release(); } }
    }
}

/// <summary>Imports only eight pinned image assets. Never loads legacy code, models or executables.</summary>
internal static class PetDanceAssets
{
    internal static readonly DanceAsset[] Reviewed = [
        new("assets/800x600/Down.png", "down", "5588a4a7edf618a306ecc9d06d7cef31b7b7aa7e39c28249780a743263f07050"),
        new("assets/800x600/Left.png", "left", "4c6e7be9ae70361669d295685b8d3cd71845798c3cca1457acc7d922eda25a40"),
        new("assets/800x600/Right.png", "right", "fc2ee220ad72571faae3e9460b4ce5c43d82a6aebca1d79f9fabfecf9630381e"),
        new("assets/800x600/Up.png", "up", "4af6d3ed1de54c34726ce5f422be6217117627fd556db3a6732668eedaa4e945"),
        new("assets/1280x800/down.png", "down", "dd1f54ac73f32eb3451e90bef01ebec5f48e9c82fca635800366b1f121329f94"),
        new("assets/1280x800/left.png", "left", "9baea498d88eea6a07e977766bd3d5e4958ed4900a4bdc2f309f4e15f867d2ba"),
        new("assets/1280x800/right.png", "right", "0dc3b206410255fa87229862ae3d59b4d4df7dcb0cec8435907170ad2f3cb4e9"),
        new("assets/1280x800/up.png", "up", "38b88310f702aa15eb89615d7d2d7dc38cbd82f553b8eb11af3a139ef2f3aa58")];

    public static string Import(string archive, string dataRoot, CancellationToken token)
    {
        using var work = PetDanceWork.Acquire();
        token.ThrowIfCancellationRequested();
        using var file = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > 512L * 1024 * 1024) throw new InvalidDataException("Teacher archive exceeds 512 MiB.");
        var digest = Convert.ToHexString(SHA256.HashData(file)); file.Position = 0;
        var images = new Dictionary<string, byte[]>(); int count = 0;
        void Read(string name, long length, Stream? stream)
        {
            token.ThrowIfCancellationRequested();
            if (++count > 5000) throw new InvalidDataException("Too many archive entries.");
            var clean = name.Replace('\\', '/');
            var asset = Reviewed.SingleOrDefault(a => clean == a.File || clean.EndsWith("/" + a.File, StringComparison.Ordinal));
            if (asset == null) return;
            if (clean.Split('/').Any(part => part is "." or "..") || length is < 24 or > 262144 || stream == null || images.ContainsKey(asset.File))
                throw new InvalidDataException("Invalid or duplicate teacher asset.");
            var bytes = new byte[(int)length]; stream.ReadExactly(bytes);
            Verify(asset, bytes); images.Add(asset.File, bytes);
        }
        var signature = new byte[4]; file.ReadExactly(signature); file.Position = 0;
        if (signature[0] == 'P' && signature[1] == 'K')
        {
            using var zip = new ZipArchive(file, ZipArchiveMode.Read, true);
            foreach (var entry in zip.Entries)
            { using var input = entry.Open(); Read(entry.FullName, entry.Length, input); }
        }
        else
        {
            using var tar = new TarReader(file, true);
            TarEntry? entry;
            while ((entry = tar.GetNextEntry()) != null)
                Read(entry.Name, entry.Length, entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile ? entry.DataStream : null);
        }
        if (images.Count != Reviewed.Length) throw new InvalidDataException("This archive does not contain all eight reviewed Pet Dance images.");
        token.ThrowIfCancellationRequested();
        var directory = Path.Combine(dataRoot, "game-packs", "wizard101-pet-dance-teacher-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(directory);
        foreach (var image in images)
        {
            var path = Path.Combine(directory, image.Key); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, image.Value);
        }
        GameExperiments.WriteAtomic(Path.Combine(directory, "pack.json"), new
        {
            schema_version = 2,
            pack_id = "wizard101-pet-dance-teacher",
            game_id = "wizard101",
            title = "Pet Dance · legacy visual teacher",
            version = "0.2.0",
            status = "draft",
            actions = new[] { "up", "down", "left", "right" }.Select(d => new { id = "dance-" + d, title = "Dance " + d, key = d.ToUpperInvariant(), duration_ms = 100 }),
            capabilities = Array.Empty<string>(),
            skills = Array.Empty<object>(),
            fixtures = Array.Empty<string>(),
            dependencies = Array.Empty<string>(),
            teacher_bootstrap = new { teacher = "Wizard101_DanceBot", source_archive_sha256 = digest, candidate_assets = Reviewed.Select(a => new { file = a.File, label = a.Label, sha256 = a.Sha256, label_source = "legacy_asset_filename" }), live_qualified = false, license_status = "Local user-supplied assets. No upstream license found; excluded from distribution.", phase_gate_required = "Watch, stable separated cues, then recognized Go. Proposals only." }
        });
        return Path.Combine(directory, "pack.json");
    }

    public static byte[][] Read(string packPath)
    {
        using var pack = JsonDocument.Parse(GameExperiments.BoundedBytes(packPath, 1048576));
        ValidatePack(pack.RootElement);
        var root = Path.GetDirectoryName(Path.GetFullPath(packPath))!;
        return Reviewed.Select(asset =>
        {
            var path = Path.Combine(root, asset.File);
            // No links in any path component; filenames are pinned, never supplied by the pack.
            for (var part = path; part != root; part = Path.GetDirectoryName(part)!)
                if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Teacher assets cannot use links.");
            var bytes = GameExperiments.BoundedBytes(path, 262144); Verify(asset, bytes); return bytes;
        }).ToArray();
    }

    internal static void ValidatePack(JsonElement pack)
    {
        if (pack.GetProperty("schema_version").GetInt32() != 2 || pack.GetProperty("status").GetString() != "draft" ||
            pack.GetProperty("game_id").GetString() != "wizard101" ||
            pack.GetProperty("teacher_bootstrap").GetProperty("teacher").GetString() != "Wizard101_DanceBot" ||
            pack.GetProperty("teacher_bootstrap").GetProperty("live_qualified").GetBoolean() ||
            new[] { "capabilities", "skills", "dependencies" }.Any(name => pack.GetProperty(name).GetArrayLength() != 0))
            throw new InvalidDataException("Choose an observation-only Pet Dance teacher draft with no execution capabilities.");
        var actions = pack.GetProperty("actions").EnumerateArray().ToArray();
        if (actions.Length != 4) throw new InvalidDataException("Teacher needs exactly four reviewed dance actions.");
        foreach (var direction in new[] { "up", "down", "left", "right" })
        {
            var matching = actions.Where(action => action.GetProperty("id").GetString() == "dance-" + direction).ToArray();
            if (matching.Length != 1 || matching[0].GetProperty("key").GetString() != direction.ToUpperInvariant() || matching[0].GetProperty("duration_ms").GetInt32() != 100 ||
                matching[0].EnumerateObject().Any(property => property.Name is not ("id" or "title" or "key" or "duration_ms")))
                throw new InvalidDataException("Dance action binding differs from the reviewed teacher.");
        }
    }

    internal static void Verify(DanceAsset asset, byte[] bytes)
    {
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Legacy image differs from the reviewed source: " + asset.File);
        using var image = Decode(bytes);
    }
    internal static Bitmap Decode(byte[] bytes)
    {
        if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new InvalidDataException("Expected a bounded PNG image.");
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)); var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        if (width is < 8 or > 256 || height is < 8 or > 256) throw new InvalidDataException("Teacher images must be 8–256 pixels per side.");
        using var input = new MemoryStream(bytes); using var image = new Bitmap(input); return new Bitmap(image);
    }
}

/// <summary>Small fixed-ROI matcher. Broad scale searches are explicit offline calibration only.</summary>
internal sealed class PetDanceMatcher : IDisposable
{
    private sealed record Template(DanceCue Cue, Mat Image, Mat Mask);
    private readonly List<Template> _templates = [];
    public PetDanceMatcher(byte[][] assets, double scale)
    {
        if (assets.Length != 8 || !double.IsFinite(scale) || scale is < .5 or > 1.5) throw new InvalidDataException("Invalid teacher templates or scale.");
        CvInvoke.NumThreads = 1; // Shared OpenCV CPU ceiling; never start a per-frame worker pool.
        try
        {
            for (var i = 0; i < assets.Length; i++)
            {
                using var bitmap = PetDanceAssets.Decode(assets[i]);
                using var pixels = bitmap.ToImage<Bgra, byte>();
                using var scaled = new Image<Bgra, byte>(Math.Max(8, (int)Math.Round(bitmap.Width * scale)), Math.Max(8, (int)Math.Round(bitmap.Height * scale)));
                CvInvoke.Resize(pixels, scaled, scaled.Size, 0, 0, scale < 1 ? Inter.Area : Inter.Linear);
                using var color = scaled.Convert<Bgr, byte>();
                using var mask = new Image<Gray, byte>(scaled.Size);
                var data = scaled.Data;
                for (var y = 0; y < scaled.Height; y++) for (var x = 0; x < scaled.Width; x++) mask.Data[y, x, 0] = data[y, x, 3] >= 240 ? (byte)255 : (byte)0;
                if (CvInvoke.CountNonZero(mask) < 16) throw new InvalidDataException("Teacher template has no visible pixels.");
                _templates.Add(new(Enum.Parse<DanceCue>(PetDanceAssets.Reviewed[i].Label, true), color.Mat.Clone(), mask.Mat.Clone()));
            }
        }
        catch { Dispose(); throw; }
    }
    public DanceMatch Match(Bitmap crop)
    {
        if (crop.Width is < 20 or > 256 || crop.Height is < 20 or > 256) throw new InvalidDataException("Use a cue region of 20–256 pixels per side.");
        using var input = crop.ToImage<Bgr, byte>();
        var best = new Dictionary<DanceCue, double>();
        foreach (var template in _templates)
        {
            if (template.Image.Width > input.Width || template.Image.Height > input.Height) continue;
            using var result = new Mat();
            CvInvoke.MatchTemplate(input, template.Image, result, TemplateMatchingType.CcorrNormed, template.Mask);
            using var scores = result.ToImage<Gray, float>(); double score = -1;
            var values = scores.Data;
            for (var y = 0; y < scores.Height; y++) for (var x = 0; x < scores.Width; x++)
                if (float.IsFinite(values[y, x, 0]) && values[y, x, 0] <= 1.001) score = Math.Max(score, Math.Min(1, values[y, x, 0]));
            best[template.Cue] = Math.Max(best.GetValueOrDefault(template.Cue, -1), score);
        }
        var ordered = best.OrderByDescending(p => p.Value).ToArray();
        if (ordered.Length < 4) return new(DanceCue.Unknown, 0, 0);
        var margin = ordered[0].Value - ordered[1].Value;
        return new(ordered[0].Value >= .96 && margin >= .025 ? ordered[0].Key : DanceCue.Unknown, ordered[0].Value, margin);
    }
    public void Dispose() { foreach (var template in _templates) { template.Image.Dispose(); template.Mask.Dispose(); } _templates.Clear(); }

    public static double Calibrate(byte[][] assets, Bitmap cue, DanceCue known, CancellationToken token)
    {
        if (known is DanceCue.Unknown or DanceCue.Neutral) throw new InvalidDataException("Choose the visible arrow direction.");
        double? scale = null; double score = -1;
        for (var i = 0; i <= 20; i++)
        {
            token.ThrowIfCancellationRequested();
            using var matcher = new PetDanceMatcher(assets, .5 + i * .05); var match = matcher.Match(cue);
            if (match.Cue == known && match.Score > score) { scale = .5 + i * .05; score = match.Score; }
        }
        return scale ?? throw new InvalidDataException("The known arrow could not be matched confidently. Change the cue region or teach new current-game assets.");
    }
}
