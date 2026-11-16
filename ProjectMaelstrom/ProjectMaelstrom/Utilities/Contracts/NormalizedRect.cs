namespace ProjectMaelstrom.Utilities.Contracts;

/// <summary>
/// A normalized bounding box with coordinates in [0.0, 1.0] relative to the active client area canvas.
/// </summary>
public readonly record struct NormalizedRect(double X, double Y, double W, double H);
