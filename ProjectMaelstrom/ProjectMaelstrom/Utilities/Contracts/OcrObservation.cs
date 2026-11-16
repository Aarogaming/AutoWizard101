namespace ProjectMaelstrom.Utilities.Contracts;

/// <summary>
/// An OCR reading from an anchor region of interest with text, confidence score, bounds, and capture timestamp.
/// </summary>
public readonly record struct OcrObservation(
    string RawText,
    float Confidence,
    NormalizedRect Bounds,
    long TimestampMs);
