namespace ProjectMaelstrom.Utilities.Contracts;

/// <summary>
/// A verified state or UI transition receipt confirming an observed change after an action.
/// </summary>
public readonly record struct StateReceipt(
    bool Verified,
    string AnchorId,
    string ObservedValue,
    float Confidence,
    TimeSpan Elapsed);
