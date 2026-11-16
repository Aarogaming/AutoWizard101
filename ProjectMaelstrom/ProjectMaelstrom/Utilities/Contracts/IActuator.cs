namespace ProjectMaelstrom.Utilities.Contracts;

/// <summary>
/// Abstract actuation interface providing safe, lease-gated inputs to the game window.
/// </summary>
public interface IActuator
{
    /// <summary>Checks GameInputLease; returns false immediately if user touched mouse/keyboard.</summary>
    bool HasActiveLease { get; }

    /// <summary>Clicks the center of a named declarative anchor if GameInputLease is held.</summary>
    Task<bool> ClickAnchorAsync(string anchorId, CancellationToken ct = default);

    /// <summary>Clicks a specific normalized sub-rect (e.g., a dynamically indexed table row).</summary>
    Task<bool> ClickNormalizedRectAsync(NormalizedRect rect, CancellationToken ct = default);
}
