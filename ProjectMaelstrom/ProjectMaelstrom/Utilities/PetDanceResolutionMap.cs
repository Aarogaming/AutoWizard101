namespace ProjectMaelstrom.Utilities;

/// <summary>Caller-measured physical pixels, including the client origin inside a
/// selected-window capture. DPI and provenance are evidence, never inferred borders.</summary>
internal sealed record PetDanceCaptureGeometry(Size CaptureSize, Rectangle ClientBounds, int Dpi, string Evidence);
internal sealed record PetDanceGridRegion(Rectangle SourceBounds, Size SearchSize, int SamplingStep)
{
    public long SearchPixels => (long)SearchSize.Width * SearchSize.Height;
}

/// <summary>Offline geometry only; constructing this map does not qualify a profile
/// or authorize live input. Client aspect must match exactly. UI pixel scale is a
/// separate layout hypothesis, not the window-resolution or DPI ratio.</summary>
internal sealed class PetDanceResolutionMap
{
    public const int MaximumSearchPixels = 250000;
    public PetDanceCaptureGeometry Calibration { get; }
    public PetDanceCaptureGeometry Source { get; }
    public double ResolutionScale { get; }

    public PetDanceResolutionMap(PetDanceCaptureGeometry calibration, PetDanceCaptureGeometry source)
    {
        Validate(calibration);
        Validate(source);
        if ((long)source.ClientBounds.Width * calibration.ClientBounds.Height !=
            (long)calibration.ClientBounds.Width * source.ClientBounds.Height)
            throw new InvalidDataException("A different client aspect ratio requires its own qualified viewport mapping.");
        Calibration = calibration;
        Source = source;
        ResolutionScale = source.ClientBounds.Width / (double)calibration.ClientBounds.Width;
    }

    public PointF ToSource(PointF calibrationPoint)
    {
        Inside(calibrationPoint, Calibration.ClientBounds);
        return new((float)(Source.ClientBounds.X + (calibrationPoint.X - Calibration.ClientBounds.X) * ResolutionScale),
            (float)(Source.ClientBounds.Y + (calibrationPoint.Y - Calibration.ClientBounds.Y) * ResolutionScale));
    }

    public PointF ToCalibration(PointF sourcePoint)
    {
        Inside(sourcePoint, Source.ClientBounds);
        return new((float)(Calibration.ClientBounds.X + (sourcePoint.X - Source.ClientBounds.X) / ResolutionScale),
            (float)(Calibration.ClientBounds.Y + (sourcePoint.Y - Source.ClientBounds.Y) / ResolutionScale));
    }

    public PetDanceGridRegion Grid(int rows)
    {
        if (rows is not (2 or 4)) throw new ArgumentOutOfRangeException(nameof(rows));
        var client = Source.ClientBounds;
        var left = client.X + client.Width / 3;
        var right = client.X + client.Width * 2 / 3;
        var top = client.Y + client.Height * (rows - 1) / rows;
        var region = Rectangle.FromLTRB(left, top, right, client.Bottom);
        var step = 1;
        Size size;
        do
        {
            size = new((region.Width + step - 1) / step, (region.Height + step - 1) / step);
            if ((long)size.Width * size.Height <= MaximumSearchPixels) break;
            step++;
        } while (true);
        return new(region, size, step);
    }

    private static void Validate(PetDanceCaptureGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var size = geometry.CaptureSize;
        var client = geometry.ClientBounds;
        if (size.Width <= 0 || size.Height <= 0 || (long)size.Width * size.Height > 16777216 ||
            client.X < 0 || client.Y < 0 || client.Width < 12 || client.Height < 12 ||
            (long)client.X + client.Width > size.Width || (long)client.Y + client.Height > size.Height ||
            geometry.Dpi is < 48 or > 768 || string.IsNullOrWhiteSpace(geometry.Evidence) ||
            geometry.Evidence.Length > 512 || geometry.Evidence.Any(char.IsControl))
            throw new InvalidDataException("Provide bounded measured capture/client pixels, DPI and geometry provenance.");
    }

    private static void Inside(PointF point, Rectangle bounds)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || point.X < bounds.Left || point.X > bounds.Right ||
            point.Y < bounds.Top || point.Y > bounds.Bottom)
            throw new InvalidDataException("The point is outside the measured game client.");
    }
}
