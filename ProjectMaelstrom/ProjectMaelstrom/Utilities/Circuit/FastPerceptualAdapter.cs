using ProjectMaelstrom.Utilities.Contracts;

namespace ProjectMaelstrom.Utilities.Circuit;

public interface ICircuitFeed
{
    Task<CircuitSnapshot> ReadCircuitSnapshotAsync(CancellationToken ct = default);
}

/// <summary>
/// Layer 1 concrete perceptual adapter implementing IWorldState and ICircuitFeed.
/// Evaluates declarative UI layout anchors (ui_anchors.v1.json) into a 64-bit WorldBits status word.
/// </summary>
public sealed class FastPerceptualAdapter : IWorldState, ICircuitFeed
{
    private readonly IWorldState _backingOcrState;
    private readonly Func<bool> _leaseProbe;
    private readonly Func<bool> _focusProbe;
    private readonly int _minGoldReserveFloor;

    public FastPerceptualAdapter(
        IWorldState backingOcrState,
        Func<bool> leaseProbe,
        Func<bool> focusProbe,
        int minGoldReserveFloor = 25000)
    {
        _backingOcrState = backingOcrState;
        _leaseProbe = leaseProbe;
        _focusProbe = focusProbe;
        _minGoldReserveFloor = minGoldReserveFloor;
    }

    public async Task<CircuitSnapshot> ReadCircuitSnapshotAsync(CancellationToken ct = default)
    {
        WorldBits bits = WorldBits.None;

        // Layer 4 Hardware Interlocks
        if (_leaseProbe()) bits |= WorldBits.LeaseActive;
        if (_focusProbe()) bits |= WorldBits.ClientFocused;

        var confidences = new Dictionary<string, float>(StringComparer.Ordinal);

        // Evaluate Idle Banner
        var idleObs = await _backingOcrState.ReadAnchorTextAsync("native_idle_banner", ct).ConfigureAwait(false);
        confidences["native_idle_banner"] = idleObs.Confidence;
        if (idleObs.Confidence >= 0.88f && idleObs.RawText.Contains("IDLE", StringComparison.OrdinalIgnoreCase))
        {
            bits |= WorldBits.NativeIdleBanner;
        }

        // Evaluate Bazaar Gold Register
        var goldObs = await _backingOcrState.ReadAnchorTextAsync("bazaar_player_gold_label", ct).ConfigureAwait(false);
        confidences["bazaar_player_gold_label"] = goldObs.Confidence;
        int parsedGold = TryParseDigits(goldObs.RawText, out int g) ? g : -1;
        if (parsedGold >= 0 && goldObs.Confidence >= 0.88f)
        {
            bits |= WorldBits.BazaarOpen;
            if (parsedGold > _minGoldReserveFloor)
            {
                bits |= WorldBits.GoldAboveFloor;
            }
        }

        // Evaluate Buy More & Confirm Modal Buttons
        var buyMoreObs = await _backingOcrState.ReadAnchorTextAsync("bazaar_buy_more_button", ct).ConfigureAwait(false);
        confidences["bazaar_buy_more_button"] = buyMoreObs.Confidence;
        if (buyMoreObs.Confidence >= 0.88f && buyMoreObs.RawText.Contains("BUY", StringComparison.OrdinalIgnoreCase))
        {
            bits |= WorldBits.BuyMoreButtonActive;
        }

        var confirmObs = await _backingOcrState.ReadAnchorTextAsync("bazaar_buy_confirm_dialog_button", ct).ConfigureAwait(false);
        confidences["bazaar_buy_confirm_dialog_button"] = confirmObs.Confidence;
        if (confirmObs.Confidence >= 0.88f && confirmObs.RawText.Contains("BUY", StringComparison.OrdinalIgnoreCase))
        {
            bits |= WorldBits.ConfirmModalOpen;
        }

        var activeNames = Enum.GetValues<WorldBits>()
            .Where(flag => flag != WorldBits.None && (bits & flag) == flag)
            .Select(flag => flag.ToString())
            .ToArray();

        return new CircuitSnapshot(
            SchemaVersion: "circuit.v1",
            TimestampUtcTicks: DateTime.UtcNow.Ticks,
            WorldBitsRaw: (ulong)bits,
            ActiveBitNames: activeNames,
            Registers: new CircuitRegisters(
                Gold: parsedGold,
                Energy: 0,
                CandidateRowIndex: -1,
                CandidateUnitPrice: 0,
                CandidateQuantity: 0),
            AnchorConfidences: confidences);
    }

    public Task<OcrObservation> ReadAnchorTextAsync(string anchorId, CancellationToken ct = default) =>
        _backingOcrState.ReadAnchorTextAsync(anchorId, ct);

    public Task<IReadOnlyList<BazaarRowObservation>> ReadBazaarRowsAsync(string tableAnchorId, CancellationToken ct = default) =>
        _backingOcrState.ReadBazaarRowsAsync(tableAnchorId, ct);

    public Task<StateReceipt> WaitForReceiptAsync(
        string anchorId,
        Func<OcrObservation, bool> predicate,
        TimeSpan timeout,
        CancellationToken ct = default) =>
        _backingOcrState.WaitForReceiptAsync(anchorId, predicate, timeout, ct);

    private static bool TryParseDigits(string raw, out int value)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out value);
    }
}
