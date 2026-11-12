namespace ProjectMaelstrom.Utilities;

internal sealed record ModuleVisualFrame(string ImageSha256, double CapturedMs, Size Geometry,
    RoutineBinding Binding, string TextOrigin);
internal sealed record ModuleVisualControl(string ControlId, RectangleF Bounds);
internal sealed record ModuleVisualRecognition(ModuleVisualFrame Frame, string Scene,
    ModuleVisualControl[] Controls, string[] VisualFacts, string Reason);

/// <summary>Converts image-bound recognition into shared module observations. It
/// grants neither input ownership nor action-outcome receipts. Capture time and
/// evidence provenance remain explicit even when freshness rejects the image.</summary>
internal static class ModuleRecognition
{
    internal const double MaximumAgeMs = 300;

    public static void ValidateFrame(ModuleVisualFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.ImageSha256 is not { Length: 64 } || !frame.ImageSha256.All(char.IsAsciiHexDigit) ||
            !double.IsFinite(frame.CapturedMs) || frame.CapturedMs < 0 || !ValidGeometry(frame.Geometry) ||
            frame.Binding is null || frame.TextOrigin is not ("local-windows-ocr" or "saved-ocr" or "manual-annotation" or "injected-test-ocr" or "selected-window-capture"))
            throw new InvalidDataException("Recognition needs a valid image hash, clock, geometry, binding and explicit text origin.");
        OperationModules.CheckToken(frame.Binding.TargetId, 256);
        OperationModules.CheckToken(frame.Binding.GeometryId, 256);
        OperationModules.CheckToken(frame.Binding.ContextId, 1024);
    }

    public static void Validate(ModuleVisualRecognition recognition)
    {
        ArgumentNullException.ThrowIfNull(recognition);
        ValidateFrame(recognition.Frame);
        OperationModules.CheckId(recognition.Scene);
        if (string.IsNullOrWhiteSpace(recognition.Reason) || recognition.Reason.Length > 512 ||
            recognition.Controls is null || recognition.Controls.Length > 32 ||
            recognition.Controls.Any(control => control is null || !ValidBounds(control.Bounds, recognition.Frame.Geometry)))
            throw new InvalidDataException("Recognition needs a bounded reason and finite image-local controls.");
        OperationModules.CheckNames(recognition.Controls.Select(control => control.ControlId).ToArray(), 32);
        OperationModules.CheckNames(recognition.VisualFacts, 32);
        if (recognition.Frame.TextOrigin == "selected-window-capture" &&
            (recognition.Scene != "Unknown" || recognition.Controls.Length != 0 || recognition.VisualFacts.Length != 0))
            throw new InvalidDataException("Uninterpreted capture cannot grant visual facts or controls.");
        if ((recognition.Scene is "Unknown" or "Loading" or "OutsideCandidate") && recognition.Controls.Length != 0)
            throw new InvalidDataException("An uncertain scene cannot provide actionable controls.");
    }

    public static ModuleObservation AsObservation(ModuleVisualRecognition recognition, RoutineBinding expectedBinding,
        Size expectedGeometry, double observedMs, bool targetReady, bool contextVerified, bool inputOwned,
        bool userActive = false, bool cancelled = false, string[]? externalFacts = null)
    {
        ArgumentNullException.ThrowIfNull(expectedBinding);
        Validate(recognition);
        if (!ValidGeometry(expectedGeometry)) throw new InvalidDataException("Expected image geometry is invalid.");
        var external = externalFacts?.ToArray() ?? [];
        OperationModules.CheckNames(external, 64);
        var frame = recognition.Frame;
        var fresh = double.IsFinite(observedMs) && observedMs >= 0 && frame.CapturedMs <= observedMs &&
            observedMs - frame.CapturedMs <= MaximumAgeMs && frame.Binding == expectedBinding && frame.Geometry == expectedGeometry;
        var usable = fresh && targetReady && contextVerified && inputOwned && !userActive && !cancelled;
        var facts = usable ? recognition.VisualFacts.Concat(external).Distinct(StringComparer.Ordinal).ToArray() : [];
        var controls = usable ? recognition.Controls.Select(control => control.ControlId).ToArray() : [];
        OperationModules.CheckNames(facts, 64);
        return new(new(frame.CapturedMs, recognition.Scene, frame.Binding, Fresh: fresh,
            TargetReady: targetReady, ContextVerified: contextVerified && frame.Binding == expectedBinding && frame.Geometry == expectedGeometry,
            InputOwned: inputOwned, ControlVerified: controls.Length > 0, Cancelled: cancelled, UserActive: userActive), facts, controls);
    }

    internal static bool ValidGeometry(Size geometry) => geometry.Width is > 0 and <= 8192 &&
        geometry.Height is > 0 and <= 8192 && (long)geometry.Width * geometry.Height <= 16777216;

    internal static bool ValidBounds(RectangleF bounds, Size geometry) =>
        float.IsFinite(bounds.X) && float.IsFinite(bounds.Y) && float.IsFinite(bounds.Width) && float.IsFinite(bounds.Height) &&
        bounds.X >= 0 && bounds.Y >= 0 && bounds.Width > 0 && bounds.Height > 0 &&
        bounds.Right <= geometry.Width && bounds.Bottom <= geometry.Height;
}
