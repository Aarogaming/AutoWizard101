using System.Diagnostics;
using ProjectMaelstrom.Utilities.Contracts;

namespace ProjectMaelstrom.Utilities.Circuit;

/// <summary>
/// Layer 3 Atomic Actuator Transaction.
/// Executes a single bounded anchor click, gated strictly by Layer 4 GameInputLease,
/// and polls Layer 1 for post-condition verification.
/// </summary>
public static class AtomicAnchorActuator
{
    public static async Task<StateReceipt> ActuateAsync(
        IActuator actuator,
        IWorldState world,
        string anchorId,
        string receiptAnchorId,
        string expectedOcr,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Layer 4 Deadman switch verification
        if (!actuator.HasActiveLease)
        {
            return new StateReceipt(
                Verified: false,
                AnchorId: anchorId,
                ObservedValue: "LEASE_NOT_HELD",
                Confidence: 0.0f,
                Elapsed: sw.Elapsed);
        }

        // Layer 3 Bounded Win32 Actuation
        bool clicked = await actuator.ClickAnchorAsync(anchorId, ct).ConfigureAwait(false);
        if (!clicked)
        {
            return new StateReceipt(
                Verified: false,
                AnchorId: anchorId,
                ObservedValue: "CLICK_REFUSED_OR_LEASE_LOST",
                Confidence: 0.0f,
                Elapsed: sw.Elapsed);
        }

        // Layer 1 Post-condition verification receipt polling
        var receipt = await world.WaitForReceiptAsync(
            receiptAnchorId,
            obs => !string.IsNullOrEmpty(expectedOcr) && obs.RawText.Contains(expectedOcr, StringComparison.OrdinalIgnoreCase),
            timeout,
            ct).ConfigureAwait(false);

        return receipt;
    }
}
