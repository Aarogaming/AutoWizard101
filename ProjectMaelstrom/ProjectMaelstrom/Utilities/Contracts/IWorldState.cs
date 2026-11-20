namespace ProjectMaelstrom.Utilities.Contracts;

/// <summary>
/// Abstract perception interface providing decoupled reads and receipt gating over the game client.
/// </summary>
public interface IWorldState
{
    /// <summary>Reads OCR text from a declarative anchor defined in ui_anchors.v1.json.</summary>
    Task<OcrObservation> ReadAnchorTextAsync(string anchorId, CancellationToken ct = default);

    /// <summary>Reads structured table rows inside a repeating list anchor.</summary>
    Task<IReadOnlyList<BazaarRowObservation>> ReadBazaarRowsAsync(string tableAnchorId, CancellationToken ct = default);

    /// <summary>Polls an anchor until the predicate matches or timeout expires.</summary>
    Task<StateReceipt> WaitForReceiptAsync(
        string anchorId,
        Func<OcrObservation, bool> predicate,
        TimeSpan timeout,
        CancellationToken ct = default);
}
