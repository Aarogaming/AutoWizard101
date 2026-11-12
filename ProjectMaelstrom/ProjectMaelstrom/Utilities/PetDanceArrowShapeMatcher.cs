namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceArrowShapeResult(DanceCue Cue, DanceCue FadeOf, double Opacity,
    double ShapeScore, double Margin, double RelativeError, string Reason);

/// <summary>
/// Small, single-consumer icon matcher for an independently located UI layout.
/// Resampling and a soft edge filter absorb rendering/antialias differences;
/// direction-discriminating neutral deltas prevent the shared ring/background
/// from deciding direction. No image-wide search, model, input, or mutable profile.
/// </summary>
internal sealed class PetDanceArrowShapeMatcher
{
    private const int Grid = 32, Channels = 3, Length = Grid * Grid * Channels;
    private static readonly DanceCue[] Directions = [DanceCue.Up, DanceCue.Down, DanceCue.Left, DanceCue.Right];
    private static readonly double[] Kernel = [1.0 / 16, 4.0 / 16, 6.0 / 16, 4.0 / 16, 1.0 / 16];
    private readonly Rectangle _cue;
    private readonly Point _center;
    private readonly int _radius;
    private readonly Size _panelSize;
    private readonly double[] _neutral = new double[Length], _weights = new double[Length], _common = new double[Length];
    private readonly double[][] _models = new double[4][], _shapeModels = new double[4][];
    private readonly double[] _norms = new double[4], _shapeNorms = new double[4];
    private readonly double[] _sample = new double[Length], _smooth = new double[Length], _scratch = new double[Length];
    private double _weightSum, _commonNorm;

    public PetDanceArrowShapeMatcher(Rectangle cue, int[] mask, IReadOnlyDictionary<string, Bitmap> references)
    {
        ArgumentNullException.ThrowIfNull(mask); ArgumentNullException.ThrowIfNull(references);
        if (cue.Width is < 16 or > 128 || cue.Height is < 16 or > 128 || mask.Length != 3 ||
            mask[2] < 8 || mask[0] - mask[2] < 0 || mask[1] - mask[2] < 0 ||
            (long)mask[0] + mask[2] >= cue.Width || (long)mask[1] + mask[2] >= cue.Height)
            throw new ArgumentException("Use a bounded icon region with an interior circular mask.");
        if (!references.TryGetValue("Neutral", out var neutral) || neutral == null)
            throw new ArgumentException("A neutral icon reference is required.", nameof(references));
        _cue = cue; _center = new(mask[0], mask[1]); _radius = mask[2]; _panelSize = neutral.Size;
        if (cue.Left < 0 || cue.Top < 0 || cue.Right > neutral.Width || cue.Bottom > neutral.Height ||
            neutral.Width > 512 || neutral.Height > 512)
            throw new ArgumentException("The icon region must lie inside a small reference panel.");
        Read(neutral, cue.X + _center.X, cue.Y + _center.Y, _radius, _radius);
        Array.Copy(_smooth, _neutral, Length);
        for (var direction = 0; direction < Directions.Length; direction++)
        {
            if (!references.TryGetValue(Directions[direction].ToString(), out var reference) || reference == null || reference.Size != _panelSize)
                throw new ArgumentException("Neutral and all four direction references must share panel geometry.", nameof(references));
            Read(reference, cue.X + _center.X, cue.Y + _center.Y, _radius, _radius);
            _models[direction] = new double[Length];
            for (var i = 0; i < Length; i++) _models[direction][i] = _smooth[i] - _neutral[i];
        }
        // Common ring/green-fill pixels receive only a small weight. Directional
        // differences are determined once from all four references, not per frame.
        var variation = new double[Length]; double largest = 0;
        for (var i = 0; i < Length; i++)
        {
            var mean = _models.Average(model => model[i]);
            _common[i] = mean;
            variation[i] = _models.Sum(model => (model[i] - mean) * (model[i] - mean)) / 4;
            largest = Math.Max(largest, variation[i]);
        }
        if (largest < 25) throw new ArgumentException("Direction references do not contain distinct arrow shapes.", nameof(references));
        for (var y = 0; y < Grid; y++)
            for (var x = 0; x < Grid; x++)
            {
                var dx = (x + .5 - Grid / 2.0) / (Grid / 2.0);
                var dy = (y + .5 - Grid / 2.0) / (Grid / 2.0);
                var radius = Math.Sqrt(dx * dx + dy * dy);
                var edge = Math.Clamp((1 - radius) / .12, 0, 1);
                for (var channel = 0; channel < Channels; channel++)
                {
                    var i = (y * Grid + x) * Channels + channel;
                    _weights[i] = edge * (.05 + .95 * variation[i] / largest);
                    _weightSum += _weights[i];
                    for (var direction = 0; direction < 4; direction++) _norms[direction] += _weights[i] * _models[direction][i] * _models[direction][i];
                }
            }
        if (_weightSum <= 0 || _norms.Any(norm => norm / _weightSum < 25))
            throw new ArgumentException("Arrow references have insufficient shape contrast.", nameof(references));
        // Remove the shared appearance as a linear nuisance component. Unlike
        // subtracting the unscaled mean, projection preserves a fading arrow's
        // direction at any opacity. Original deltas still measure that opacity.
        for (var i = 0; i < Length; i++) _commonNorm += _weights[i] * _common[i] * _common[i];
        for (var direction = 0; direction < 4; direction++)
        {
            _shapeModels[direction] = new double[Length];
            _shapeNorms[direction] = RemoveCommon(_models[direction], _shapeModels[direction]);
            if (_shapeNorms[direction] / _weightSum < 25)
                throw new ArgumentException("Arrow references have insufficient direction-specific contrast.", nameof(references));
        }
        for (var first = 0; first < 4; first++) for (var second = first + 1; second < 4; second++)
        {
            double dot = 0;
            for (var i = 0; i < Length; i++) dot += _weights[i] * _shapeModels[first][i] * _shapeModels[second][i];
            if (dot / Math.Sqrt(_shapeNorms[first] * _shapeNorms[second]) > .92)
                throw new ArgumentException("Arrow references cannot satisfy the required direction separation.", nameof(references));
        }
    }

    /// <summary>Input is the small canonical panel returned by the shared UI locator.</summary>
    public PetDanceArrowShapeResult Match(Bitmap normalizedPanel)
    {
        ArgumentNullException.ThrowIfNull(normalizedPanel);
        if (normalizedPanel.Size != _panelSize) throw new ArgumentException("Use the independently normalized panel geometry.", nameof(normalizedPanel));
        Read(normalizedPanel, _cue.X + _center.X, _cue.Y + _center.Y, _radius, _radius);
        return Classify();
    }

    /// <summary>Pure scale-test seam: the whole supplied bitmap is the already located
    /// icon region, not a screen to search. Reference-relative center/radius are retained.</summary>
    internal PetDanceArrowShapeResult MatchIcon(Bitmap icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        if (icon.Width is < 16 or > 256 || icon.Height is < 16 or > 256 ||
            Math.Abs(icon.Width / (double)icon.Height - _cue.Width / (double)_cue.Height) > .02)
            throw new ArgumentException("Use a bounded icon patch with the reference aspect ratio.", nameof(icon));
        var scaleX = icon.Width / (double)_cue.Width; var scaleY = icon.Height / (double)_cue.Height;
        Read(icon, (_center.X + .5) * scaleX - .5, (_center.Y + .5) * scaleY - .5, _radius * scaleX, _radius * scaleY);
        return Classify();
    }

    private PetDanceArrowShapeResult Classify()
    {
        double energy = 0;
        for (var i = 0; i < Length; i++) { _sample[i] = _smooth[i] - _neutral[i]; energy += _weights[i] * _sample[i] * _sample[i]; }
        var shapeEnergy = RemoveCommon(_sample, _scratch);
        Span<double> scores = stackalloc double[4]; Span<double> alpha = stackalloc double[4];
        var best = 0;
        for (var direction = 0; direction < 4; direction++)
        {
            double dot = 0, shapeDot = 0;
            for (var i = 0; i < Length; i++)
            {
                dot += _weights[i] * _sample[i] * _models[direction][i];
                shapeDot += _weights[i] * _scratch[i] * _shapeModels[direction][i];
            }
            // A large common green/background change cannot promote a tiny
            // directional remainder into a full arrow merely by normalization.
            // Both the complete delta and its independent directional amplitude
            // must support the reported opacity; a true fade scales both equally.
            alpha[direction] = Math.Clamp(Math.Min(dot / _norms[direction], shapeDot / _shapeNorms[direction]), 0, 1.5);
            scores[direction] = shapeEnergy > 1e-12 ? Math.Clamp(shapeDot / Math.Sqrt(shapeEnergy * _shapeNorms[direction]), -1, 1) : 0;
            if (scores[direction] > scores[best]) best = direction;
        }
        var second = -1.0;
        for (var direction = 0; direction < 4; direction++) if (direction != best) second = Math.Max(second, scores[direction]);
        var margin = scores[best] - second; var opacity = alpha[best];
        double residual = 0;
        for (var i = 0; i < Length; i++)
        {
            var difference = _sample[i] - opacity * _models[best][i];
            residual += _weights[i] * difference * difference;
        }
        var relativeError = Math.Sqrt(residual / _norms[best]) / Math.Max(.12, opacity);
        var blank = Math.Sqrt(energy / _weightSum);
        if (blank <= 8 && opacity < .15)
            return new(DanceCue.Neutral, DanceCue.Unknown, opacity, scores[best], margin, relativeError, "neutral-small-icon-delta");
        if (scores[best] < .9 || margin < .08)
            return new(DanceCue.Unknown, DanceCue.Unknown, opacity, scores[best], margin, relativeError, "shape-ambiguous-or-unmatched");
        if (opacity >= .7)
            return new(Directions[best], DanceCue.Unknown, opacity, scores[best], margin, relativeError, "soft-shape-separated");
        if (opacity >= .12)
            return new(DanceCue.Fading, Directions[best], opacity, scores[best], margin, relativeError, "known-soft-shape-fade");
        return new(DanceCue.Unknown, DanceCue.Unknown, opacity, scores[best], margin, relativeError, "too-little-shape-evidence");
    }

    private double RemoveCommon(double[] source, double[] target)
    {
        double dot = 0, energy = 0;
        for (var i = 0; i < Length; i++) dot += _weights[i] * source[i] * _common[i];
        var projection = _commonNorm > 1e-12 ? dot / _commonNorm : 0;
        for (var i = 0; i < Length; i++)
        {
            target[i] = source[i] - projection * _common[i];
            energy += _weights[i] * target[i] * target[i];
        }
        return energy;
    }

    private void Read(Bitmap image, double centerX, double centerY, double radiusX, double radiusY)
    {
        for (var y = 0; y < Grid; y++)
            for (var x = 0; x < Grid; x++)
            {
                var sx = Math.Clamp(centerX + ((x + .5) * 2 / Grid - 1) * radiusX, 0, image.Width - 1.0);
                var sy = Math.Clamp(centerY + ((y + .5) * 2 / Grid - 1) * radiusY, 0, image.Height - 1.0);
                var x0 = (int)Math.Floor(sx); var y0 = (int)Math.Floor(sy);
                var x1 = Math.Min(x0 + 1, image.Width - 1); var y1 = Math.Min(y0 + 1, image.Height - 1);
                var a = image.GetPixel(x0, y0); var b = image.GetPixel(x1, y0); var c = image.GetPixel(x0, y1); var d = image.GetPixel(x1, y1);
                var fx = sx - x0; var fy = sy - y0; var i = (y * Grid + x) * Channels;
                _sample[i] = Mix(a.R, b.R, c.R, d.R, fx, fy);
                _sample[i + 1] = Mix(a.G, b.G, c.G, d.G, fx, fy);
                _sample[i + 2] = Mix(a.B, b.B, c.B, d.B, fx, fy);
            }
        for (var y = 0; y < Grid; y++) for (var x = 0; x < Grid; x++) for (var channel = 0; channel < Channels; channel++)
        {
            double value = 0;
            for (var k = -2; k <= 2; k++) value += Kernel[k + 2] * _sample[(y * Grid + Math.Clamp(x + k, 0, Grid - 1)) * Channels + channel];
            _scratch[(y * Grid + x) * Channels + channel] = value;
        }
        for (var y = 0; y < Grid; y++) for (var x = 0; x < Grid; x++) for (var channel = 0; channel < Channels; channel++)
        {
            double value = 0;
            for (var k = -2; k <= 2; k++) value += Kernel[k + 2] * _scratch[(Math.Clamp(y + k, 0, Grid - 1) * Grid + x) * Channels + channel];
            _smooth[(y * Grid + x) * Channels + channel] = value;
        }
    }

    private static double Mix(double a, double b, double c, double d, double x, double y)
        => (a * (1 - x) + b * x) * (1 - y) + (c * (1 - x) + d * x) * y;
}
