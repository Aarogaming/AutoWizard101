namespace ProjectMaelstrom.Utilities;

internal enum PetDanceLevelSelectionState { Unknown, Unselected, SelectedAppearance }

/// <summary>Independent context for the exact image being classified. PointerAwayVerified
/// means a current visual pointer/hover witness, not absence of a captured mouse cursor.
/// The caller checks the encoded image hash before decoding and supplying this binding.</summary>
internal sealed record PetDanceLevelSelectionBinding(string ImageSha256, double CapturedMs,
    Size Geometry, PetDanceLevelControl Control, bool InstructionsVerified = false,
    bool PointerAwayVerified = false, bool PlayEnabledVerified = false);

internal sealed record PetDanceLevelSelectionResult(PetDanceLevelSelectionState State,
    string ImageSha256, double CapturedMs, Size Geometry, PetDanceArena? Arena,
    Rectangle? Region, double Similarity, double Margin, bool SelectionVerified, string Reason);

/// <summary>Bounded saved-layout Wizard City selection matcher. It compares an observed
/// icon with unselected and selected-after-pointer-leaves reference crops. Other arenas,
/// layouts and scales remain unknown. A matched appearance is separate from qualification
/// for an entry proposal; this component never performs OCR, capture or input.</summary>
internal sealed class PetDanceLevelSelectionVision
{
    public static Size QualifiedGeometry => new(1282, 760);
    public static Rectangle QualifiedControlBounds => new(452, 485, 59, 55);
    internal const int MaximumCropPixels = 4096;
    private readonly byte[] _unselected, _selected;

    public PetDanceLevelSelectionVision(Bitmap unselectedReference, Bitmap selectedAwayReference)
    {
        ArgumentNullException.ThrowIfNull(unselectedReference);
        ArgumentNullException.ThrowIfNull(selectedAwayReference);
        if (unselectedReference.Size != QualifiedControlBounds.Size ||
            selectedAwayReference.Size != QualifiedControlBounds.Size ||
            !Opaque(unselectedReference) || !Opaque(selectedAwayReference))
            throw new ArgumentException("Use pinned, opaque Wizard City icon crops at the qualified native size.");
        _unselected = VisualStateRecognizer.Features(unselectedReference);
        _selected = VisualStateRecognizer.Features(selectedAwayReference);
        if (1 - VisualStateRecognizer.Similarity(_unselected, _selected) < VisualStateRecognizer.MinimumMargin * 2)
            throw new ArgumentException("Selected and unselected references are not distinguishable.");
    }

    public PetDanceLevelSelectionResult Classify(Bitmap image, string imageSha256, double capturedMs,
        PetDanceLevelSelectionBinding binding)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(binding);
        PetDanceLevelSelectionResult Unknown(string reason, Rectangle? region = null, double score = 0, double margin = 0) =>
            new(PetDanceLevelSelectionState.Unknown, imageSha256, capturedMs, image.Size, null, region, score, margin, false, reason);
        if (!PetDanceEntryVision.IsImageHash(imageSha256) || !double.IsFinite(capturedMs) || capturedMs < 0)
            return Unknown("selection-frame-identity-invalid");
        if (!string.Equals(binding.ImageSha256, imageSha256, StringComparison.OrdinalIgnoreCase) ||
            binding.CapturedMs != capturedMs || binding.Geometry != image.Size)
            return Unknown("selection-frame-binding-mismatch");
        if (image.Size != QualifiedGeometry) return Unknown("selection-layout-unqualified");
        if (binding.Control is null || !binding.Control.IdentityVerified || binding.Control.Arena != PetDanceArena.WizardCity)
            return Unknown("selection-arena-identity-unqualified");
        if (!binding.InstructionsVerified) return Unknown("selection-instructions-unverified");
        if (binding.Control.Bounds != (RectangleF)QualifiedControlBounds)
            return Unknown("selection-control-registration-unqualified");

        var region = QualifiedControlBounds;
        using var crop = image.Clone(region, image.PixelFormat);
        if (!Opaque(crop)) return Unknown("selection-region-transparent", region);
        var features = VisualStateRecognizer.Features(crop);
        var unselected = VisualStateRecognizer.Similarity(features, _unselected);
        var selected = VisualStateRecognizer.Similarity(features, _selected);
        var best = Math.Max(unselected, selected); var margin = Math.Abs(selected - unselected);
        if (best < VisualStateRecognizer.MinimumSimilarity)
            return Unknown("selection-appearance-not-matched", region, best, margin);
        if (margin < VisualStateRecognizer.MinimumMargin)
            return Unknown("selection-appearance-ambiguous", region, best, margin);
        if (unselected > selected)
            return new(PetDanceLevelSelectionState.Unselected, imageSha256, capturedMs, image.Size,
                PetDanceArena.WizardCity, region, best, margin, false, "qualified-unselected-reference-match");

        var verified = binding.PointerAwayVerified && binding.PlayEnabledVerified;
        var reason = !binding.PointerAwayVerified ? "selected-appearance-pointer-away-unverified" :
            !binding.PlayEnabledVerified ? "selected-appearance-enabled-play-unverified" :
            "selected-appearance-qualified-in-saved-layout";
        return new(PetDanceLevelSelectionState.SelectedAppearance, imageSha256, capturedMs, image.Size,
            PetDanceArena.WizardCity, region, best, margin, verified, reason);
    }

    private static bool Opaque(Bitmap crop)
    {
        if ((long)crop.Width * crop.Height > MaximumCropPixels) return false;
        for (var y = 0; y < crop.Height; y++)
            for (var x = 0; x < crop.Width; x++)
                if (crop.GetPixel(x, y).A < 250) return false;
        return true;
    }
}
