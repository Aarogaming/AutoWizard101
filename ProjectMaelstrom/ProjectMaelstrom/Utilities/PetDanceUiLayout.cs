using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

/// <summary>Explicit opt-in layout hypotheses; no game settings are read or changed.</summary>
internal sealed record DancePanelUiLayout(double[] Scales, int Columns = 3, int Rows = 4,
    bool AllowSixthFallback = true, double TopAnchorY = 38);

internal sealed record DanceUiLayoutObservation(bool Qualified, DancePhase Phase, double Scale,
    Rectangle SearchRegion, Rectangle Bounds, double PhaseScore, double PhaseMargin,
    double RingContrast, int RingSupportingSpokes, string Status,
    bool NativeResolution = false, int SearchSamplingStep = 1, long SearchPixels = 0);

/// <summary>
/// Finds a direction-independent phase plate in the bottom-middle grid region,
/// confirms the nearby circular button, then normalizes that UI group once.
/// It never scales the game scene or searches a different transform per arrow.
/// Mutable scratch buffers belong to one observer/consumer, not concurrent readers.
/// </summary>
internal sealed class PetDanceUiLayout
{
    private const int MaximumSearchPixels = 250000, MaximumComponents = 512;
    private readonly DancePanelProfile _profile;
    private readonly DancePanelUiLayout _options;
    private readonly Rectangle _top, _bottom, _phase, _canonicalPlate;
    private readonly Size _panelSize;
    private readonly PointF _canonicalCenter, _buttonCenter;
    private readonly byte[] _background;
    private readonly Dictionary<DancePhase, byte[]> _phaseMasks = [];
    private byte[] _search = [], _visited = [];
    private int[] _queue = [];

    public PetDanceUiLayout(DancePanelProfile profile, IReadOnlyDictionary<string, Bitmap> references,
        DancePanelUiLayout options)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Columns != 3 || options.Rows is not (2 or 4) || options.Scales is not { Length: > 0 and <= 5 } ||
            options.Scales.Any(scale => !double.IsFinite(scale) || scale is < .5 or > 2) ||
            options.Scales.Distinct().Count() != options.Scales.Length || !double.IsFinite(options.TopAnchorY) ||
            options.TopAnchorY < 0 || options.TopAnchorY >= profile.Height)
            throw new InvalidDataException("Use at most five explicit UI scales and a middle 1/12 or 1/6 search region.");
        _profile = profile;
        _options = options with { Scales = (double[])options.Scales.Clone() };
        _top = PetDancePanelVision.Rect(profile.Top); _bottom = PetDancePanelVision.Rect(profile.Bottom);
        _phase = PetDancePanelVision.Rect(profile.Phase);
        _panelSize = new(_top.Width, _top.Height + _bottom.Height);
        if (_top.Width != _bottom.Width || _panelSize.Width > 256 || _panelSize.Height > 256 ||
            _phase.Y < _top.Height || _phase.Bottom > _panelSize.Height)
            throw new InvalidDataException("UI layout needs a bounded phase region in the bottom panel.");
        var baseline = Pixels.Read(references["Go"], _phase);
        var histogram = new int[4096];
        for (var index = 0; index < baseline.Bytes.Length; index += 3)
            histogram[(baseline.Bytes[index] >> 4) | ((baseline.Bytes[index + 1] >> 4) << 4) | ((baseline.Bytes[index + 2] >> 4) << 8)]++;
        var mode = Array.IndexOf(histogram, histogram.Max());
        _background = [(byte)((mode & 15) * 16 + 8), (byte)(((mode >> 4) & 15) * 16 + 8), (byte)(((mode >> 8) & 15) * 16 + 8)];
        var plate = Components(references["Go"], _phase).OrderByDescending(item => item.Count).FirstOrDefault();
        if (plate == null || plate.Bounds.Width < 32 || plate.Bounds.Height < 8 || plate.Fill < .5)
            throw new InvalidDataException("A stable, filled phase plate is required for automatic UI layout.");
        _canonicalPlate = new(_bottom.X + plate.Bounds.X, _bottom.Y + plate.Bounds.Y - _top.Height, plate.Bounds.Width, plate.Bounds.Height);
        _canonicalCenter = Center(_canonicalPlate);
        var cue = PetDancePanelVision.Rect(profile.Cue);
        _buttonCenter = new(_bottom.X + cue.X + profile.Mask[0], _bottom.Y + cue.Y - _top.Height + profile.Mask[1]);
        foreach (var phase in new[] { DancePhase.Watch, DancePhase.Go, DancePhase.Resolving })
        {
            var mask = TextMask(Pixels.Read(references[phase.ToString()], _phase));
            if (mask.Count(value => value != 0) < 20) throw new InvalidDataException("Phase text reference is too small.");
            _phaseMasks.Add(phase, mask);
        }
    }

    public static Rectangle SearchRegion(Size source, int rows)
    {
        if (source.Width < 12 || source.Height < 12 || rows is not (2 or 4)) throw new ArgumentOutOfRangeException(nameof(source));
        var left = source.Width / 3; var right = source.Width * 2 / 3; var top = source.Height * (rows - 1) / rows;
        return new(left, top, right - left, source.Height - top);
    }

    /// <summary>Caller owns the returned panel. A recognized layout may still have Unknown phase.
    /// Failed or ambiguous acquisition never returns a cached phase or transform.</summary>
    public bool TryExtract(Bitmap source, out Bitmap? panel, out DanceUiLayoutObservation? observation)
    {
        panel = null; observation = null;
        if (source.Size != new Size(_profile.Width, _profile.Height)) return false;
        int[] rows = _options.Rows == 4 && _options.AllowSixthFallback ? [4, 2] : [_options.Rows];
        foreach (var rowCount in rows)
        {
            var region = SearchRegion(source.Size, rowCount);
            var candidates = Components(source, region).Where(item => item.Fill >= .5 &&
                item.Bounds.Left > region.Left && item.Bounds.Right < region.Right &&
                item.Bounds.Top > region.Top && item.Bounds.Bottom < region.Bottom).Take(32).ToArray();
            Bitmap? accepted = null; DanceUiLayoutObservation? acceptedObservation = null;
            try
            {
                foreach (var candidate in candidates)
                {
                    var scale = _options.Scales.MinBy(value => Math.Abs(candidate.Bounds.Width - _canonicalPlate.Width * value));
                    if (Math.Abs(candidate.Bounds.Width - _canonicalPlate.Width * scale) > Math.Max(3, _canonicalPlate.Width * scale * .02) ||
                        Math.Abs(candidate.Bounds.Height - _canonicalPlate.Height * scale) > Math.Max(3, _canonicalPlate.Height * scale * .12)) continue;
                    var center = Center(candidate.Bounds);
                    var bottomPixels = TransformedRegion(source, _bottom, scale, _canonicalCenter, center);
                    if (bottomPixels == null) continue;
                    var ring = RingEvidence(bottomPixels, scale, center);
                    if (ring.Contrast < 20 || ring.Supporting < 8) continue;
                    var topAnchor = new PointF(_profile.Width / 2f, (float)_options.TopAnchorY);
                    var topPixels = TransformedRegion(source, _top, scale, topAnchor, topAnchor);
                    if (topPixels == null) continue;
                    var current = Render(topPixels, bottomPixels, scale, topAnchor, center);
                    var phase = ClassifyPhase(Pixels.Read(current, _phase));
                    if (accepted != null)
                    {
                        current.Dispose();
                        observation = new(false, DancePhase.Unknown, 0, region, Rectangle.Empty, 0, 0, 0, 0, "ambiguous-layout");
                        return false;
                    }
                    accepted = current;
                    acceptedObservation = new(true, phase.Phase, scale, region, candidate.Bounds,
                        phase.Score, phase.Margin, ring.Contrast, ring.Supporting,
                        phase.Phase == DancePhase.Unknown ? "layout-found-phase-unknown" : "layout-and-phase-qualified");
                }
                if (accepted != null) { panel = accepted; accepted = null; observation = acceptedObservation; return true; }
            }
            finally { accepted?.Dispose(); }
        }
        return false;
    }

    /// <summary>Offline native-resolution path. Borders are measured; client aspect
    /// is validated by the map. UI pixel scale stays independent of resolution/DPI.
    /// Only bounded broad-grid acquisition and qualified native panel crops are read.
    /// This overload does not qualify new live configurations.</summary>
    public bool TryExtract(Bitmap source, PetDanceResolutionMap geometry, out Bitmap? panel,
        out DanceUiLayoutObservation? observation, double sourceUiScaleMultiplier = 1)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(geometry);
        panel = null; observation = null;
        if (source.Size != geometry.Source.CaptureSize || geometry.Calibration.CaptureSize != new Size(_profile.Width, _profile.Height))
            throw new InvalidDataException("The measured geometry does not match this frame and calibration profile.");
        if (!double.IsFinite(sourceUiScaleMultiplier) || sourceUiScaleMultiplier is < .25 or > 4)
            throw new InvalidDataException("Provide an explicit bounded source UI scale hypothesis.");
        var calibrationClient = geometry.Calibration.ClientBounds;
        if (!calibrationClient.Contains(_top) || !calibrationClient.Contains(_bottom))
            throw new InvalidDataException("The calibrated panels must lie inside the measured game client.");
        var sourceClient = geometry.Source.ClientBounds;
        var topAnchor = new PointF(calibrationClient.X + calibrationClient.Width / 2f, calibrationClient.Y);
        var sourceTopAnchor = new PointF(sourceClient.X + sourceClient.Width / 2f, sourceClient.Y);
        var scales = _options.Scales.Select(scale => scale * sourceUiScaleMultiplier).ToArray();
        int[] rows = _options.Rows == 4 && _options.AllowSixthFallback ? [4, 2] : [_options.Rows];
        foreach (var rowCount in rows)
        {
            var grid = geometry.Grid(rowCount);
            Bitmap? accepted = null;
            DanceUiLayoutObservation? acceptedObservation = null;
            try
            {
                foreach (var candidate in NativeCandidates(source, grid, scales))
                {
                    var scale = scales.MinBy(value => Math.Abs(candidate.Bounds.Width - _canonicalPlate.Width * value));
                    if (!MatchesPlate(candidate.Bounds, scale, 1)) continue;
                    var center = Center(candidate.Bounds);
                    var bottomPixels = TransformedRegion(source, _bottom, scale, _canonicalCenter, center, sourceClient);
                    if (bottomPixels == null) continue;
                    var ring = RingEvidence(bottomPixels, scale, center);
                    if (ring.Contrast < 20 || ring.Supporting < 8) continue;
                    var topPixels = TransformedRegion(source, _top, scale, topAnchor, sourceTopAnchor, sourceClient);
                    if (topPixels == null) continue;
                    var current = Render(topPixels, bottomPixels, scale, topAnchor, center, sourceTopAnchor);
                    var phase = ClassifyPhase(Pixels.Read(current, _phase));
                    if (accepted != null)
                    {
                        current.Dispose();
                        observation = new(false, DancePhase.Unknown, 0, grid.SourceBounds, Rectangle.Empty,
                            0, 0, 0, 0, "ambiguous-native-layout", true, grid.SamplingStep, grid.SearchPixels);
                        return false;
                    }
                    accepted = current;
                    acceptedObservation = new(true, phase.Phase, scale, grid.SourceBounds, candidate.Bounds,
                        phase.Score, phase.Margin, ring.Contrast, ring.Supporting,
                        phase.Phase == DancePhase.Unknown ? "native-layout-found-phase-unknown" : "native-layout-and-phase-qualified",
                        true, grid.SamplingStep, grid.SearchPixels);
                }
                if (accepted != null) { panel = accepted; accepted = null; observation = acceptedObservation; return true; }
            }
            finally { accepted?.Dispose(); }
        }
        return false;
    }

    private bool MatchesPlate(Rectangle bounds, double scale, int samplingStep) =>
        Math.Abs(bounds.Width - _canonicalPlate.Width * scale) <= Math.Max(3 * samplingStep, _canonicalPlate.Width * scale * .02) &&
        Math.Abs(bounds.Height - _canonicalPlate.Height * scale) <= Math.Max(3 * samplingStep, _canonicalPlate.Height * scale * .12);

    private IReadOnlyList<Component> NativeCandidates(Bitmap source, PetDanceGridRegion grid, double[] scales)
    {
        var region = grid.SourceBounds;
        if (grid.SamplingStep == 1)
            return Components(source, region).Where(item => item.Fill >= .5 &&
                item.Bounds.Left > region.Left && item.Bounds.Right < region.Right &&
                item.Bounds.Top > region.Top && item.Bounds.Bottom < region.Bottom).Take(32).ToArray();

        using var search = new Bitmap(grid.SearchSize.Width, grid.SearchSize.Height, PixelFormat.Format24bppRgb);
        using (var draw = Graphics.FromImage(search))
        {
            // Uniform sampling, with at most one partial padding row/column. Do not
            // independently stretch width and height to fit integer output bounds.
            draw.Clear(Color.Black);
            draw.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            draw.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            draw.DrawImage(source, new RectangleF(0, 0, region.Width / (float)grid.SamplingStep,
                region.Height / (float)grid.SamplingStep), region, GraphicsUnit.Pixel);
        }
        var result = new List<Component>();
        var refinementRemaining = MaximumSearchPixels;
        foreach (var coarse in Components(search, new(Point.Empty, search.Size)).Where(item => item.Fill >= .5).Take(32))
        {
            var step = grid.SamplingStep;
            var estimated = new Rectangle(region.X + coarse.Bounds.X * step, region.Y + coarse.Bounds.Y * step,
                coarse.Bounds.Width * step, coarse.Bounds.Height * step);
            if (!scales.Any(scale => MatchesPlate(estimated, scale, step))) continue;
            var refinement = Rectangle.Inflate(estimated, step * 3, step * 3);
            refinement.Intersect(region);
            var pixels = (long)refinement.Width * refinement.Height;
            if (pixels <= 0 || pixels > refinementRemaining) continue;
            refinementRemaining -= (int)pixels;
            foreach (var candidate in Components(source, refinement).Where(item => item.Fill >= .5 &&
                item.Bounds.Left > refinement.Left && item.Bounds.Right < refinement.Right &&
                item.Bounds.Top > refinement.Top && item.Bounds.Bottom < refinement.Bottom &&
                scales.Any(scale => MatchesPlate(item.Bounds, scale, 1))))
            {
                if (result.All(existing => existing.Bounds != candidate.Bounds)) result.Add(candidate);
                if (result.Count >= 32) return result;
            }
        }
        return result;
    }

    private (DancePhase Phase, double Score, double Margin) ClassifyPhase(Pixels pixels)
    {
        var mask = TextMask(pixels);
        var scores = _phaseMasks.Select(pair => (Phase: pair.Key, Score: ShapeAgreement(mask, pair.Value, _phase.Width, _phase.Height)))
            .OrderByDescending(item => item.Score).ToArray();
        var margin = scores[0].Score - scores[1].Score;
        return (scores[0].Score >= .72 && margin >= .15 ? scores[0].Phase : DancePhase.Unknown, scores[0].Score, margin);
    }

    private static byte[] TextMask(Pixels pixels)
    {
        var mask = new byte[pixels.Width * pixels.Height];
        // Exclude the fixed plate border; colored glyph shape supplies the phase.
        for (var y = 3; y < pixels.Height - 3; y++)
            for (var x = 8; x < pixels.Width - 8; x++)
            {
                var offset = (y * pixels.Width + x) * 3;
                var b = pixels.Bytes[offset]; var g = pixels.Bytes[offset + 1]; var r = pixels.Bytes[offset + 2];
                if (g > 110 && g > 1.35 * r && g > 1.35 * b) mask[y * pixels.Width + x] = 2;
                else if (r > 150 && g > 100 && b < 130 && r > 1.6 * b && g > 1.5 * b) mask[y * pixels.Width + x] = 1;
            }
        return mask;
    }

    internal static double ShapeAgreement(byte[] first, byte[] second, int width, int height)
    {
        if (width is <= 0 or > 256 || height is <= 0 or > 256 || first.Length != width * height || second.Length != first.Length)
            throw new ArgumentException("Equal bounded text masks are required.");
        var count = 0; var matched = 0;
        void Match(byte[] from, byte[] to)
        {
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var value = from[y * width + x]; if (value == 0) continue;
                    count++; var found = false;
                    for (var dy = -1; dy <= 1 && !found; dy++)
                        for (var dx = -1; dx <= 1; dx++)
                            if (x + dx >= 0 && x + dx < width && y + dy >= 0 && y + dy < height &&
                                to[(y + dy) * width + x + dx] == value) { found = true; break; }
                    if (found) matched++;
                }
        }
        Match(first, second); Match(second, first);
        return count >= 40 ? matched / (double)count : 0;
    }

    private (double Contrast, int Supporting) RingEvidence(Pixels pixels, double scale, PointF center)
    {
        var inner = _profile.Mask[2] + 3; var outer = inner + 2;
        double difference = 0; var supporting = 0;
        for (var index = 0; index < 16; index++)
        {
            var angle = index * Math.PI / 8; var cos = Math.Cos(angle); var sin = Math.Sin(angle);
            double Luminance(int radius)
            {
                var position = Map(new((float)(_buttonCenter.X + cos * radius), (float)(_buttonCenter.Y + sin * radius)), scale, _canonicalCenter, center);
                return (pixels.Sample(position.X, position.Y, 0) + pixels.Sample(position.X, position.Y, 1) + pixels.Sample(position.X, position.Y, 2)) / 3;
            }
            var contrast = Luminance(outer) - Luminance(inner);
            difference += contrast; if (contrast >= 20) supporting++;
        }
        return (difference / 16, supporting);
    }

    private Pixels? TransformedRegion(Bitmap source, Rectangle region, double scale, PointF origin, PointF target,
        Rectangle? allowed = null)
    {
        var first = Map(new(region.Left, region.Top), scale, origin, target);
        var last = Map(new(region.Right - 1, region.Bottom - 1), scale, origin, target);
        var bounds = Rectangle.FromLTRB((int)Math.Floor(first.X), (int)Math.Floor(first.Y), (int)Math.Floor(last.X) + 2, (int)Math.Floor(last.Y) + 2);
        if (bounds.Left < 0 || bounds.Top < 0 || bounds.Right > source.Width || bounds.Bottom > source.Height ||
            (allowed.HasValue && (!allowed.Value.Contains(bounds) || (long)bounds.Width * bounds.Height > MaximumSearchPixels))) return null;
        // GDI's RGB conversion preserves hidden colors below alpha=0. Only the new
        // measured-client path checks opacity, on these bounded crops, so a partially
        // transparent/invalid surface cannot masquerade as a visible arrow panel.
        if (allowed.HasValue && !Pixels.IsOpaque(source, bounds)) return null;
        return Pixels.Read(source, bounds);
    }

    private Bitmap Render(Pixels topPixels, Pixels bottomPixels, double scale, PointF topAnchor, PointF center,
        PointF? sourceTopAnchor = null)
    {
        var output = new Bitmap(_panelSize.Width, _panelSize.Height, PixelFormat.Format24bppRgb);
        try
        {
            var bytes = new byte[_panelSize.Width * _panelSize.Height * 3];
            void Fill(Pixels pixels, Rectangle region, int outputY, PointF origin, PointF target)
            {
                for (var y = 0; y < region.Height; y++)
                    for (var x = 0; x < region.Width; x++)
                    {
                        var position = Map(new(region.X + x, region.Y + y), scale, origin, target);
                        var offset = ((y + outputY) * _panelSize.Width + x) * 3;
                        for (var channel = 0; channel < 3; channel++)
                            bytes[offset + channel] = (byte)Math.Clamp((int)Math.Round(pixels.Sample(position.X, position.Y, channel)), 0, 255);
                    }
            }
            Fill(topPixels, _top, 0, topAnchor, sourceTopAnchor ?? topAnchor);
            Fill(bottomPixels, _bottom, _top.Height, _canonicalCenter, center);
            var data = output.LockBits(new Rectangle(Point.Empty, output.Size), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try { for (var y = 0; y < output.Height; y++) Marshal.Copy(bytes, y * output.Width * 3, IntPtr.Add(data.Scan0, y * data.Stride), output.Width * 3); }
            finally { output.UnlockBits(data); }
            return output;
        }
        catch { output.Dispose(); throw; }
    }

    private sealed record Component(Rectangle Bounds, int Count) { public double Fill => Count / (double)(Bounds.Width * Bounds.Height); }
    private IReadOnlyList<Component> Components(Bitmap source, Rectangle region)
    {
        var count = checked(region.Width * region.Height);
        if (count > MaximumSearchPixels) throw new InvalidDataException("UI search region exceeds its pixel budget.");
        if (_search.Length < count * 3) _search = new byte[count * 3];
        if (_visited.Length < count) _visited = new byte[count];
        if (_queue.Length < count) _queue = new int[count];
        Pixels.Copy(source, region, _search);
        Array.Clear(_visited, 0, count);
        var result = new List<Component>(); var components = 0;
        bool IsBackground(int index)
        {
            var at = index * 3;
            return Math.Abs(_search[at] - _background[0]) <= 32 && Math.Abs(_search[at + 1] - _background[1]) <= 32 && Math.Abs(_search[at + 2] - _background[2]) <= 32;
        }
        for (var index = 0; index < count; index++)
        {
            if (_visited[index] != 0 || !IsBackground(index)) continue;
            if (++components > MaximumComponents) return [];
            var read = 0; var written = 1; _queue[0] = index; _visited[index] = 1;
            var minX = region.Width; var minY = region.Height; var maxX = 0; var maxY = 0;
            while (read < written)
            {
                var next = _queue[read++]; var x = next % region.Width; var y = next / region.Width;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                void Visit(int neighbor)
                {
                    if (_visited[neighbor] != 0 || !IsBackground(neighbor)) return;
                    _visited[neighbor] = 1; _queue[written++] = neighbor;
                }
                if (x > 0) Visit(next - 1); if (x + 1 < region.Width) Visit(next + 1);
                if (y > 0) Visit(next - region.Width); if (y + 1 < region.Height) Visit(next + region.Width);
            }
            if (written >= 80) result.Add(new(new(region.X + minX, region.Y + minY, maxX - minX + 1, maxY - minY + 1), written));
        }
        return result;
    }

    private static PointF Center(Rectangle bounds) => new(bounds.X + (bounds.Width - 1) / 2f, bounds.Y + (bounds.Height - 1) / 2f);
    private static PointF Map(PointF point, double scale, PointF origin, PointF target) =>
        new((float)(target.X + (point.X - origin.X) * scale), (float)(target.Y + (point.Y - origin.Y) * scale));

    private sealed class Pixels(Rectangle bounds, byte[] bytes)
    {
        public byte[] Bytes { get; } = bytes;
        public int Width => bounds.Width;
        public int Height => bounds.Height;
        public double Sample(double sourceX, double sourceY, int channel)
        {
            var x = sourceX - bounds.X; var y = sourceY - bounds.Y;
            var left = (int)Math.Floor(x); var top = (int)Math.Floor(y);
            if (left < 0 || top < 0 || left + 1 >= Width || top + 1 >= Height) throw new InvalidDataException("UI sample is outside its measured region.");
            var fx = x - left; var fy = y - top; var first = (top * Width + left) * 3 + channel;
            return Bytes[first] * (1 - fx) * (1 - fy) + Bytes[first + 3] * fx * (1 - fy) +
                Bytes[first + Width * 3] * (1 - fx) * fy + Bytes[first + Width * 3 + 3] * fx * fy;
        }
        public static Pixels Read(Bitmap image, Rectangle region)
        {
            var bytes = new byte[checked(region.Width * region.Height * 3)]; Copy(image, region, bytes); return new(region, bytes);
        }
        public static bool IsOpaque(Bitmap image, Rectangle region)
        {
            if (!Image.IsAlphaPixelFormat(image.PixelFormat)) return true;
            var row = new byte[checked(region.Width * 4)];
            var data = image.LockBits(region, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (var y = 0; y < region.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    for (var alpha = 3; alpha < row.Length; alpha += 4) if (row[alpha] != 255) return false;
                }
                return true;
            }
            finally { image.UnlockBits(data); }
        }
        public static void Copy(Bitmap image, Rectangle region, byte[] bytes)
        {
            var data = image.LockBits(region, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try { for (var y = 0; y < region.Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), bytes, y * region.Width * 3, region.Width * 3); }
            finally { image.UnlockBits(data); }
        }
    }
}
