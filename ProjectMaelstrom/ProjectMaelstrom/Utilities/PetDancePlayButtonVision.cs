namespace ProjectMaelstrom.Utilities;

internal enum PetDancePlayState { Unknown, Disabled, Enabled }
internal sealed record PetDancePlayButtonResult(PetDancePlayState State, double Similarity, double Margin,
    Rectangle? Region, string Reason);

/// <summary>Two qualified button exemplars, localized by observed PLAY text. Reuses
/// the shared small RGB-grid matcher; never scans a screen or infers a selected arena.
/// The owner supplies pinned references and independently verifies the instruction scene.</summary>
internal sealed class PetDancePlayButtonVision
{
    internal const int MaximumCropPixels = 65536;
    private readonly byte[] _disabled, _enabled;
    private readonly byte[]? _enabledHover;

    public PetDancePlayButtonVision(Bitmap disabledReference, Bitmap enabledReference, Bitmap? enabledHoverReference = null)
    {
        ArgumentNullException.ThrowIfNull(disabledReference);
        ArgumentNullException.ThrowIfNull(enabledReference);
        if (!Bounded(disabledReference.Size) || disabledReference.Size != enabledReference.Size ||
            !Opaque(disabledReference) || !Opaque(enabledReference))
            throw new ArgumentException("Use two equally sized, bounded, opaque button reference crops.");
        _disabled = VisualStateRecognizer.Features(disabledReference);
        _enabled = VisualStateRecognizer.Features(enabledReference);
        if (1 - VisualStateRecognizer.Similarity(_disabled, _enabled) < VisualStateRecognizer.MinimumMargin * 2)
            throw new ArgumentException("Enabled and disabled references are not distinguishable.");
        if (enabledHoverReference != null)
        {
            if (enabledHoverReference.Size != disabledReference.Size || !Opaque(enabledHoverReference))
                throw new ArgumentException("Hover reference must share the bounded opaque button geometry.");
            _enabledHover = VisualStateRecognizer.Features(enabledHoverReference);
            if (1 - VisualStateRecognizer.Similarity(_disabled, _enabledHover) < VisualStateRecognizer.MinimumMargin * 2)
                throw new ArgumentException("Enabled hover and disabled references are not distinguishable.");
        }
    }

    public PetDancePlayButtonResult Classify(Bitmap image, RectangleF observedPlayText)
    {
        ArgumentNullException.ThrowIfNull(image);
        var region = Region(image.Size, observedPlayText);
        if (region is null) return new(PetDancePlayState.Unknown, 0, 0, null, "play-region-unqualified");
        using var crop = image.Clone(region.Value, image.PixelFormat);
        if (!Opaque(crop)) return new(PetDancePlayState.Unknown, 0, 0, region, "play-region-transparent");
        var features = VisualStateRecognizer.Features(crop);
        var disabled = VisualStateRecognizer.Similarity(features, _disabled);
        var enabled = VisualStateRecognizer.Similarity(features, _enabled);
        if (_enabledHover != null) enabled = Math.Max(enabled, VisualStateRecognizer.Similarity(features, _enabledHover));
        var best = Math.Max(disabled, enabled); var margin = Math.Abs(enabled - disabled);
        if (best < VisualStateRecognizer.MinimumSimilarity)
            return new(PetDancePlayState.Unknown, best, margin, region, "play-appearance-not-matched");
        if (margin < VisualStateRecognizer.MinimumMargin)
            return new(PetDancePlayState.Unknown, best, margin, region, "play-appearance-ambiguous");
        return new(enabled > disabled ? PetDancePlayState.Enabled : PetDancePlayState.Disabled,
            best, margin, region, "qualified-button-reference-match");
    }

    internal static Rectangle? Region(Size image, RectangleF text)
    {
        if (!PetDancePostGameClassifier.ValidBounds(text, image) || text.Width is < 6 or > 256 ||
            text.Width / text.Height is < 2.2f or > 5.5f) return null;
        // Derived from the saved paired crops: pad the observed text relative to
        // its width, which is stable across the two OCR glyph-height variants.
        // This is an analysis crop only; action coordinates remain observed text.
        var crop = Rectangle.FromLTRB((int)Math.Floor(text.Left - text.Width * .55),
            (int)Math.Floor(text.Top - text.Width * .12), (int)Math.Ceiling(text.Right + text.Width * .55),
            (int)Math.Ceiling(text.Top + text.Width * .45));
        return new Rectangle(Point.Empty, image).Contains(crop) && Bounded(crop.Size) ? crop : null;
    }

    private static bool Bounded(Size size) => size.Width is >= 8 and <= 512 && size.Height is >= 4 and <= 128 &&
        (long)size.Width * size.Height <= MaximumCropPixels;

    private static bool Opaque(Bitmap image)
    {
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                if (image.GetPixel(x, y).A < 250) return false;
        return true;
    }
}
