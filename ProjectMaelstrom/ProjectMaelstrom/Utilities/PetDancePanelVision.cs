using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record DancePanelReference(string Label, string File, string Sha256, string? Source = null);
internal sealed record DancePanelCaptureGeometry(int Width, int Height, string Transform, string Evidence);
internal sealed record DancePanelProfile(int Version, int Width, int Height, int[] Top, int[] Bottom,
    int[] Cue, int[] Phase, int[] Feedback, int[] Mask, DancePanelReference[] References, string SourceVideoSha256,
    DancePanelCaptureGeometry? CaptureGeometry = null, DancePanelUiLayout? UiLayout = null);
internal sealed record DancePanelObservation(DancePhase Phase, DanceCue Cue, DanceCue FadeOf,
    double Opacity, double Error, double Margin, double PhaseError,
    int CueOffsetX = 0, int CueOffsetY = 0, bool CueAlignmentQualified = false,
    DanceUiLayoutObservation? Layout = null, PetDanceArrowShapeResult? ArrowShape = null);

/// <summary>Small calibrated pixel regions, not a model. Explicitly fits arrow opacity against
/// current-game arrow/neutral examples. A fading arrow never becomes a new directional cue.</summary>
internal sealed class PetDancePanelVision : IDisposable
{
    private readonly DancePanelProfile _profile;
    private readonly Dictionary<string, string> _pins = [];
    public IReadOnlyDictionary<string, string> Pins => _pins;
    public void VerifyInputsUnchanged()
    {
        foreach (var pin in _pins) if (!GameExperiments.Digest(pin.Key).Equals(pin.Value, StringComparison.OrdinalIgnoreCase))
            throw new IOException("A panel calibration input changed during the run.");
    }
    private readonly Dictionary<string, Bitmap> _references = [];
    private readonly Dictionary<DanceCue, double[]> _arrows = [];
    private readonly Dictionary<DancePhase, double[]> _phases = [];
    private readonly Dictionary<DancePhase, bool[]> _feedback = [];
    private readonly double[] _neutral;
    private readonly DanceCueAlignment _cueAlignment;
    private readonly PetDanceUiLayout? _uiLayout;
    private readonly PetDanceArrowShapeMatcher? _arrowShape;
    private Bitmap? _layoutPanel;
    private DanceUiLayoutObservation? _layoutObservation;
    private bool _allowLoadingComparison = true;
    private readonly double[]? _loading;
    private readonly DanceOutcomeGate _outcomes = new();
    public Size PanelSize { get; }
    public Size CanonicalSize => new(_profile.Width, _profile.Height);
    public DancePanelCaptureGeometry? CaptureGeometry => _profile.CaptureGeometry;
    public bool UsesShapeSequence => _profile.UiLayout != null;

    public PetDancePanelVision(string profilePath)
    {
        var profileBytes = GameExperiments.BoundedBytes(profilePath, 65536);
        _pins[Path.GetFullPath(profilePath)] = Convert.ToHexString(SHA256.HashData(profileBytes));
        _profile = JsonSerializer.Deserialize<DancePanelProfile>(profileBytes) ?? throw new InvalidDataException("Missing panel profile.");
        WindowObserver.CheckSize(_profile.Width, _profile.Height);
        if (_profile.Version != 1 || _profile.References is not { Length: 10 or 11 } || _profile.Mask is not { Length: 3 })
            throw new InvalidDataException("Unsupported panel profile.");
        var top = Rect(_profile.Top); var bottom = Rect(_profile.Bottom);
        Inside(top, CanonicalSize); Inside(bottom, CanonicalSize);
        ValidateCaptureGeometry(CanonicalSize, _profile.CaptureGeometry, top, bottom);
        if (top.Width != bottom.Width || top.Width > 256 || top.Height + bottom.Height > 256)
            throw new InvalidDataException("Panel must fit within 256 × 256 pixels.");
        PanelSize = new(top.Width, top.Height + bottom.Height);
        foreach (var region in new[] { _profile.Cue, _profile.Phase, _profile.Feedback }) Inside(Rect(region), PanelSize);
        var cue = Rect(_profile.Cue); var mask = _profile.Mask;
        if (mask[2] < 8 || mask[2] > Math.Min(cue.Width, cue.Height) / 2 || (long)mask[0] - mask[2] < 0 || (long)mask[1] - mask[2] < 0 || (long)mask[0] + mask[2] >= cue.Width || (long)mask[1] + mask[2] >= cue.Height)
            throw new InvalidDataException("Cue mask must be inside its region.");
        var root = Path.GetDirectoryName(Path.GetFullPath(profilePath))!;
        try
        {
            foreach (var reference in _profile.References)
            {
                if (reference == null) throw new InvalidDataException("Missing panel reference.");
                if (reference.File != reference.Label + ".png" || reference.File.IndexOfAny(['/', '\\', ':']) >= 0)
                    throw new InvalidDataException("Use a plain named local reference image.");
                var path = Path.Combine(root, reference.File);
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked references are not supported.");
                var bytes = GameExperiments.BoundedBytes(path, 262144);
                if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(reference.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Panel reference changed.");
                _pins[path] = reference.Sha256;
                var image = PetDanceAssets.Decode(bytes);
                if (image.Size != PanelSize) { image.Dispose(); throw new InvalidDataException("Reference dimensions changed."); }
                if (!_references.TryAdd(reference.Label, image)) { image.Dispose(); throw new InvalidDataException("Duplicate reference."); }
            }
            _neutral = CuePixels(_references["Neutral"]);
            if (_references.TryGetValue("Loading", out var loading))
                _loading = Pixels(loading, new Rectangle(Point.Empty, PanelSize));
            if (_references.Count == 11 && _loading == null)
                throw new InvalidDataException("The optional eleventh reference must be Loading.");
            foreach (var direction in new[] { DanceCue.Up, DanceCue.Down, DanceCue.Left, DanceCue.Right })
            {
                var pixels = CuePixels(_references[direction.ToString()]);
                var delta = pixels.Select((value, i) => value - _neutral[i]).ToArray();
                if (delta.Sum(v => v * v) / delta.Length < 100) throw new InvalidDataException("Arrow is too similar to neutral.");
                _arrows[direction] = delta;
            }
            _cueAlignment = new(Rect(_profile.Cue), _profile.Mask, _references);
            if (_profile.UiLayout != null)
            {
                _uiLayout = new(_profile, _references, _profile.UiLayout);
                _arrowShape = new(Rect(_profile.Cue), _profile.Mask, _references);
            }
            foreach (var phase in new[] { DancePhase.Watch, DancePhase.Go, DancePhase.Resolving })
                _phases[phase] = Pixels(_references[phase.ToString()], Rect(_profile.Phase));
            foreach (var phase in new[] { DancePhase.Success, DancePhase.Failure })
            {
                _feedback[phase] = FeedbackPixels(_references[phase.ToString()], phase);
                if (_feedback[phase].Count(v => v) < 30) throw new InvalidDataException("Feedback reference has insufficient text.");
            }
        }
        catch { Dispose(); throw; }
    }

    internal static Rectangle Rect(int[]? r) => r is { Length: 4 } ? new(r[0], r[1], r[2], r[3]) : throw new InvalidDataException("Invalid panel region.");
    private static void Inside(Rectangle r, Size size)
    {
        if (r.X < 0 || r.Y < 0 || r.Width < 4 || r.Height < 4 || (long)r.X + r.Width > size.Width || (long)r.Y + r.Height > size.Height)
            throw new InvalidDataException("Panel region is outside its image.");
    }
    internal static void ValidateCaptureGeometry(Size canonical, DancePanelCaptureGeometry? geometry, params Rectangle[] regions)
    {
        if (geometry == null) return;
        // Encoded dimensions alone do not identify a pixel transform. A reviewed
        // profile must name one mapping; matching never tries competing mappings.
        if (geometry.Width != canonical.Width || geometry.Height != canonical.Height - 1 ||
            geometry.Transform is not ("preserve-pixels" or "scale-one-pixel-height") ||
            string.IsNullOrWhiteSpace(geometry.Evidence) || geometry.Evidence.Length > 512 || geometry.Evidence.Any(char.IsControl))
            throw new InvalidDataException("Declare a supported source geometry, transform and calibration evidence.");
        if (geometry.Transform == "preserve-pixels")
            foreach (var region in regions) Inside(region, new Size(geometry.Width, geometry.Height));
    }

    public Bitmap Extract(Bitmap source)
    {
        // Layout evidence belongs only to the exact panel returned for this frame.
        _layoutPanel = null; _layoutObservation = null; _allowLoadingComparison = true;
        Bitmap? normalized = null;
        try
        {
            if (source.Size != CanonicalSize)
            {
                var geometry = _profile.CaptureGeometry;
                if (geometry == null || source.Width != geometry.Width || source.Height != geometry.Height)
                    throw new InvalidDataException("Window geometry differs from calibration; a reviewed capture mapping is required.");
                if (geometry.Transform == "scale-one-pixel-height")
                {
                    normalized = new Bitmap(_profile.Width, _profile.Height, PixelFormat.Format24bppRgb);
                    using var scale = Graphics.FromImage(normalized);
                    scale.DrawImage(source, new Rectangle(Point.Empty, normalized.Size));
                }
            }
            if (_uiLayout != null && _uiLayout.TryExtract(normalized ?? source, out var located, out _layoutObservation))
            {
                _layoutPanel = located ?? throw new InvalidDataException("Qualified layout did not supply a panel.");
                return _layoutPanel;
            }
            var panel = new Bitmap(PanelSize.Width, PanelSize.Height, PixelFormat.Format24bppRgb);
            using var draw = Graphics.FromImage(panel);
            var top = Rect(_profile.Top); var bottom = Rect(_profile.Bottom);
            draw.DrawImage(normalized ?? source, new Rectangle(0, 0, top.Width, top.Height), top, GraphicsUnit.Pixel);
            draw.DrawImage(normalized ?? source, new Rectangle(0, top.Height, bottom.Width, bottom.Height), bottom, GraphicsUnit.Pixel);
            _layoutPanel = panel;
            return panel;
        }
        finally { normalized?.Dispose(); }
    }

    /// <summary>Offline native-resolution extraction using explicit measured client
    /// geometry. No runtime launch gate is changed. Unrecognized layouts produce an
    /// unknown panel, never a guessed fixed crop or a fabricated loading pause.</summary>
    public Bitmap Extract(Bitmap source, PetDanceResolutionMap geometry, double sourceUiScaleMultiplier = 1)
    {
        _layoutPanel = null; _layoutObservation = null; _allowLoadingComparison = false;
        if (_uiLayout == null) throw new InvalidDataException("Native-resolution extraction requires a layout-aware profile.");
        if (_uiLayout.TryExtract(source, geometry, out var located, out _layoutObservation, sourceUiScaleMultiplier))
        {
            _layoutPanel = located ?? throw new InvalidDataException("Qualified native layout did not supply a panel.");
            return _layoutPanel;
        }
        _layoutPanel = new Bitmap(PanelSize.Width, PanelSize.Height, PixelFormat.Format24bppRgb);
        using (var draw = Graphics.FromImage(_layoutPanel)) draw.Clear(Color.Black);
        return _layoutPanel;
    }

    private double[] CuePixels(Bitmap panel, Point offset = default)
    {
        var r = Rect(_profile.Cue); var mask = _profile.Mask; var pixels = new List<double>();
        for (var y = 0; y < r.Height; y += 2) for (var x = 0; x < r.Width; x += 2)
            if ((x - mask[0]) * (x - mask[0]) + (y - mask[1]) * (y - mask[1]) < mask[2] * mask[2])
            { var p = panel.GetPixel(r.X + x + offset.X, r.Y + y + offset.Y); pixels.Add(p.R); pixels.Add(p.G); pixels.Add(p.B); }
        return pixels.ToArray();
    }
    private static double[] Pixels(Bitmap panel, Rectangle region)
    {
        var values = new List<double>();
        for (var y = region.Top; y < region.Bottom; y += 2) for (var x = region.Left; x < region.Right; x += 2)
        { var p = panel.GetPixel(x, y); values.Add(p.R); values.Add(p.G); values.Add(p.B); }
        return values.ToArray();
    }
    private bool[] FeedbackPixels(Bitmap panel, DancePhase phase)
    {
        var r = Rect(_profile.Feedback); var values = new List<bool>();
        for (var y = r.Top; y < r.Bottom; y++) for (var x = r.Left; x < r.Right; x++)
        {
            var p = panel.GetPixel(x, y);
            values.Add(phase == DancePhase.Success ? p.G > 120 && p.G > 1.25 * p.R && p.G > 1.1 * p.B : p.R > 120 && p.R > 1.6 * p.G && p.R > 1.4 * p.B);
        }
        return values.ToArray();
    }
    private static double Rms(double[] a, double[] b) => Math.Sqrt(a.Select((v, i) => (v - b[i]) * (v - b[i])).Average());
    internal static (DanceCue Cue, DanceCue FadeOf) Classify(double blank, double opacity, double error, double margin, DanceCue direction)
    {
        if (new[] { blank, opacity, error, margin }.Any(v => !double.IsFinite(v) || v < 0)) return (DanceCue.Unknown, DanceCue.Unknown);
        if (blank <= 12 && opacity < .15) return (DanceCue.Neutral, DanceCue.Unknown);
        if (direction is not (DanceCue.Up or DanceCue.Down or DanceCue.Left or DanceCue.Right)) return (DanceCue.Unknown, DanceCue.Unknown);
        if (opacity is >= .7 and <= 1.5 && error <= 16 && margin >= 8) return (direction, DanceCue.Unknown);
        if (opacity < .7 && error <= 12 && margin >= 1.5) return (DanceCue.Fading, direction);
        return (DanceCue.Unknown, DanceCue.Unknown);
    }
    public DancePanelObservation Read(Bitmap panel, double timeMs)
    {
        if (panel.Size != PanelSize || !double.IsFinite(timeMs)) throw new InvalidDataException("Invalid panel observation.");
        var alignment = _cueAlignment.Locate(panel);
        var pixels = CuePixels(panel, alignment.Offset); var delta = pixels.Select((v, i) => v - _neutral[i]).ToArray();
        var blank = Math.Sqrt(delta.Average(v => v * v));
        var fits = _arrows.Select(pair =>
        {
            var model = pair.Value; var norm = model.Sum(v => v * v);
            var alpha = Math.Clamp(delta.Select((v, i) => v * model[i]).Sum() / norm, 0, 1.5);
            var error = Math.Sqrt(delta.Select((v, i) => Math.Pow(v - alpha * model[i], 2)).Average());
            return (Direction: pair.Key, Alpha: alpha, Error: error);
        }).OrderBy(row => row.Error).ToArray();
        var best = fits[0]; var margin = fits[1].Error - best.Error;
        var classification = alignment.Accepted ? Classify(blank, best.Alpha, best.Error, margin, best.Direction)
            : (Cue: DanceCue.Unknown, FadeOf: DanceCue.Unknown);
        var phasePixels = Pixels(panel, Rect(_profile.Phase));
        var phases = _phases.Select(pair => (Phase: pair.Key, Error: Rms(phasePixels, pair.Value))).OrderBy(row => row.Error).ToArray();
        var phase = phases[0].Error <= 12 && phases[1].Error - phases[0].Error >= 8 ? phases[0].Phase : DancePhase.Unknown;
        var layout = ReferenceEquals(panel, _layoutPanel) ? _layoutObservation : null;
        PetDanceArrowShapeResult? shape = null;
        if (_uiLayout != null)
        {
            // An earlier frame's layout or an exact old crop cannot authorize a new input.
            phase = layout?.Qualified == true ? layout.Phase : DancePhase.Unknown;
            classification = (DanceCue.Unknown, DanceCue.Unknown);
            if (layout?.Qualified == true)
            {
                shape = _arrowShape!.Match(panel);
                classification = (shape.Cue, shape.FadeOf);
            }
        }
        // Require a calibrated static loading image. Unknown gameplay is never a pause.
        if (phase == DancePhase.Unknown && _allowLoadingComparison && _loading != null &&
            Rms(Pixels(panel, new Rectangle(Point.Empty, PanelSize)), _loading) <= 8)
            phase = DancePhase.Loading;
        var candidate = DancePhase.Unknown;
        if (phase == DancePhase.Resolving)
        {
            var matches = _feedback.Select(pair =>
            {
                var mask = FeedbackPixels(panel, pair.Key); var count = mask.Count(v => v) + pair.Value.Count(v => v);
                var overlap = mask.Where((v, i) => v && pair.Value[i]).Count();
                return (Phase: pair.Key, Score: count > 0 ? 2.0 * overlap / count : 0);
            }).OrderByDescending(row => row.Score).ToArray();
            candidate = matches[0].Score >= .65 && matches[0].Score - matches[1].Score >= .15 ? matches[0].Phase : DancePhase.Unknown;
        }
        phase = _outcomes.Observe(timeMs, phase, candidate);
        return new(phase, phase == DancePhase.Loading ? DanceCue.Unknown : classification.Cue,
            phase == DancePhase.Loading ? DanceCue.Unknown : classification.FadeOf, shape?.Opacity ?? best.Alpha, best.Error, margin, phases[0].Error,
            alignment.Offset.X, alignment.Offset.Y, _cueAlignment.Enabled && alignment.Accepted, layout, shape);
    }
    public void Dispose() { foreach (var image in _references.Values) image.Dispose(); _references.Clear(); }
}

/// <summary>
/// Locates only the stable outer button ring, independently of arrow direction.
/// One offset is shared by all cue fits; phase/feedback coordinates never move.
/// References without a textured ring keep their existing exact-pixel behavior.
/// </summary>
internal sealed class DanceCueAlignment
{
    internal const int MaximumShift = 3, MaximumAnchorSamples = 256;
    private readonly Rectangle _cue;
    private readonly Point[] _points;
    private readonly byte[] _reference, _current;
    public bool Enabled { get; }

    public DanceCueAlignment(Rectangle cue, int[] mask, IReadOnlyDictionary<string, Bitmap> references)
    {
        _cue = cue;
        _reference = new byte[checked(cue.Width * cue.Height * 3)];
        _current = new byte[_reference.Length];
        ReadRegion(references["Neutral"], _reference);
        string[] labels = ["Up", "Down", "Left", "Right", "Watch", "Go", "Resolving"];
        var comparison = labels.Select(label =>
        {
            var bytes = new byte[_reference.Length]; ReadRegion(references[label], bytes); return bytes;
        }).ToArray();
        var points = new List<Point>();
        var inner = mask[2] + 1; var outer = mask[2] + 8;
        for (var y = 0; y < cue.Height; y += 2)
            for (var x = 0; x < cue.Width; x += 2)
            {
                if (x < MaximumShift || y < MaximumShift || x + MaximumShift >= cue.Width || y + MaximumShift >= cue.Height) continue;
                var radius = (x - mask[0]) * (x - mask[0]) + (y - mask[1]) * (y - mask[1]);
                if (radius < inner * inner || radius > outer * outer) continue;
                var index = (y * cue.Width + x) * 3;
                if (comparison.Any(image => Enumerable.Range(0, 3).Any(channel => Math.Abs(image[index + channel] - _reference[index + channel]) > 24))) continue;
                points.Add(new(x, y));
            }
        var stride = Math.Max(1, (points.Count + MaximumAnchorSamples - 1) / MaximumAnchorSamples);
        _points = points.Where((_, index) => index % stride == 0).ToArray();
        // Both anchor and interior cue samples must remain within the declared ROI.
        var margin = Math.Min(Math.Min(mask[0] - mask[2], mask[1] - mask[2]),
            Math.Min(cue.Width - 1 - mask[0] - mask[2], cue.Height - 1 - mask[1] - mask[2]));
        if (_points.Length < 32 || margin < MaximumShift) return;
        double variation = 0;
        for (var channel = 0; channel < 3; channel++)
        {
            var average = _points.Average(point => (double)_reference[(point.Y * cue.Width + point.X) * 3 + channel]);
            variation += _points.Sum(point => Math.Pow(_reference[(point.Y * cue.Width + point.X) * 3 + channel] - average, 2));
        }
        Enabled = Math.Sqrt(variation / (_points.Length * 3)) >= 12;
    }

    internal (Point Offset, bool Accepted) Locate(Bitmap panel)
    {
        if (!Enabled) return (Point.Empty, true);
        ReadRegion(panel, _current);
        double Error(int dx, int dy)
        {
            double sum = 0;
            foreach (var point in _points)
            {
                var original = (point.Y * _cue.Width + point.X) * 3;
                var shifted = ((point.Y + dy) * _cue.Width + point.X + dx) * 3;
                for (var channel = 0; channel < 3; channel++)
                {
                    var difference = _current[shifted + channel] - _reference[original + channel];
                    sum += difference * difference;
                }
            }
            return Math.Sqrt(sum / (_points.Length * 3));
        }
        var offset = Point.Empty;
        var best = Error(0, 0); var second = double.PositiveInfinity;
        for (var dy = -MaximumShift; dy <= MaximumShift; dy++)
            for (var dx = -MaximumShift; dx <= MaximumShift; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var error = Error(dx, dy);
                if (error < best) { second = best; best = error; offset = new(dx, dy); }
                else if (error < second) second = error;
            }
        // No search over directions, no growing radius, no threshold relaxation.
        var accepted = best <= 16 && (offset == Point.Empty || second - best >= 2);
        return accepted ? (offset, true) : (Point.Empty, false);
    }

    private void ReadRegion(Bitmap image, byte[] bytes)
    {
        var data = image.LockBits(_cue, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var rowBytes = _cue.Width * 3;
            for (var y = 0; y < _cue.Height; y++)
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), bytes, y * rowBytes, rowBytes);
        }
        finally { image.UnlockBits(data); }
    }
}

/// <summary>Only continuous, fresh result evidence may latch an outcome.</summary>
internal sealed class DanceOutcomeGate
{
    private double _last = -1, _since;
    private DancePhase _candidate, _result;
    public DancePhase Observe(double timeMs, DancePhase phase, DancePhase feedback)
    {
        if (!double.IsFinite(timeMs) || timeMs < 0 || timeMs <= _last) throw new InvalidDataException("Outcome timestamps must increase.");
        if (_last >= 0 && timeMs - _last > PetDanceSequence.MaximumGapMs) { _candidate = _result = DancePhase.Unknown; _since = timeMs; }
        _last = timeMs;
        if (phase != DancePhase.Resolving)
        {
            _candidate = DancePhase.Unknown; _since = timeMs;
            if (phase is DancePhase.Watch or DancePhase.Unknown or DancePhase.Loading) _result = DancePhase.Unknown;
            return phase;
        }
        if (feedback is not (DancePhase.Success or DancePhase.Failure)) feedback = DancePhase.Unknown;
        if (feedback != _candidate) { _candidate = feedback; _since = timeMs; }
        if (_result == DancePhase.Unknown && feedback != DancePhase.Unknown && timeMs - _since >= 50) _result = feedback;
        return _result == DancePhase.Unknown ? DancePhase.Resolving : _result;
    }
}
