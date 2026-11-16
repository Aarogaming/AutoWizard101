using System.Text.Json;
using ProjectMaelstrom.Utilities.Contracts;

namespace ProjectMaelstrom.Utilities.Circuit;

/// <summary>
/// Server and dispatch bridge for Layer 1 Circuit Snapshot requests and Layer 3 Atomic Actuation.
/// Exposes the sub-millisecond sensory feed over named-pipe IPC and in-process hosts.
/// </summary>
public static class CircuitServer
{
    private static FastPerceptualAdapter? _activeAdapter;
    private static readonly object _sync = new();

    public static void Initialize(FastPerceptualAdapter adapter)
    {
        lock (_sync)
        {
            _activeAdapter = adapter;
        }
    }

    /// <summary>
    /// Processes a "circuit-snapshot" command and returns a structured response object.
    /// Can be plugged directly into JobDeckHost's command switch.
    /// </summary>
    public static async Task<object> HandleSnapshotRequestAsync(
        IWorldState? backingState = null,
        Func<bool>? leaseProbe = null,
        Func<bool>? focusProbe = null,
        int minGoldFloor = 25000,
        CancellationToken ct = default)
    {
        FastPerceptualAdapter adapter;
        lock (_sync)
        {
            adapter = _activeAdapter ??= new FastPerceptualAdapter(
                backingOcrState: backingState ?? new FallbackOcrState(),
                leaseProbe: leaseProbe ?? (() => false),
                focusProbe: focusProbe ?? (() => false),
                minGoldReserveFloor: minGoldFloor);
        }

        var snapshot = await adapter.ReadCircuitSnapshotAsync(ct).ConfigureAwait(false);

        return new
        {
            ok = true,
            schemaVersion = 1,
            snapshot = snapshot
        };
    }

    private sealed class FallbackOcrState : IWorldState
    {
        public Task<OcrObservation> ReadAnchorTextAsync(string anchorId, CancellationToken ct = default) =>
            Task.FromResult(new OcrObservation("", 0.0f, new NormalizedRect(), 0));

        public Task<IReadOnlyList<BazaarRowObservation>> ReadBazaarRowsAsync(string tableAnchorId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BazaarRowObservation>>(Array.Empty<BazaarRowObservation>());

        public Task<StateReceipt> WaitForReceiptAsync(string anchorId, Func<OcrObservation, bool> predicate, TimeSpan timeout, CancellationToken ct = default) =>
            Task.FromResult(new StateReceipt(false, anchorId, "", 0.0f, TimeSpan.Zero));
    }
}
