using ProjectMaelstrom.Utilities.Contracts;

namespace ProjectMaelstrom.Utilities.Cards;

/// <summary>
/// Operational states for the Bazaar Reagent / Treasure Card Sniper finite state machine.
/// </summary>
public enum SniperState
{
    VerifyShopOpen,
    RefreshListing,
    ScanRows,
    SelectTargetRow,
    OpenBuyModal,
    ConfirmPurchase,
    VerifyGoldReceipt,
    RecoveryAbort
}

/// <summary>
/// Policy settings governing target reagents, max allowable prices, and financial safety floors.
/// </summary>
public sealed record SniperPolicy(
    IReadOnlyDictionary<string, int> TargetMaxUnitPrices, // e.g., {"Titanium": 500, "Sunstone": 1200}
    int MinGoldReserveFloor,                              // Stop sniping if gold drops below this floor
    TimeSpan RefreshCooldown,
    float MinOcrConfidence = 0.88f);

/// <summary>
/// Receipt-gated finite state machine that snipes reagents and treasure cards in the Bazaar.
/// Decoupled from volatile screen coordinates via IWorldState and IActuator.
/// </summary>
public sealed class BazaarSniperCard
{
    private readonly IWorldState _world;
    private readonly IActuator _actuator;
    private readonly SniperPolicy _policy;

    public SniperState CurrentState { get; private set; } = SniperState.VerifyShopOpen;
    public string? LastAbortReason { get; private set; }
    public BazaarRowObservation? LastSelectedCandidate => _selectedCandidate;
    public int GoldBeforePurchase => _goldBeforePurchase;

    private BazaarRowObservation? _selectedCandidate;
    private int _goldBeforePurchase;

    public BazaarSniperCard(IWorldState world, IActuator actuator, SniperPolicy policy)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <summary>
    /// Executes one discrete step of the finite state machine.
    /// Returns immediately without state mutation if human input takeover is detected.
    /// </summary>
    public async Task<SniperState> StepAsync(CancellationToken ct = default)
    {
        // 1. Pre-Condition: Instant human takeover check (zero-latency pause)
        if (!_actuator.HasActiveLease)
        {
            return CurrentState; // Pause in place while human holds input lease
        }

        switch (CurrentState)
        {
            case SniperState.VerifyShopOpen:
                var goldObs = await _world.ReadAnchorTextAsync("bazaar_player_gold_label", ct).ConfigureAwait(false);
                if (!TryParseGold(goldObs.RawText, out int currentGold) || goldObs.Confidence < _policy.MinOcrConfidence)
                {
                    return TransitionToRecovery("Bazaar gold label unreadable or shop not open.");
                }
                if (currentGold <= _policy.MinGoldReserveFloor)
                {
                    return TransitionToRecovery($"Gold reserve floor reached ({currentGold} <= {_policy.MinGoldReserveFloor}).");
                }
                _goldBeforePurchase = currentGold;
                CurrentState = SniperState.RefreshListing;
                break;

            case SniperState.RefreshListing:
                if (!await _actuator.ClickAnchorAsync("bazaar_refresh_button", ct).ConfigureAwait(false))
                    return CurrentState;

                // Post-Condition Receipt: Wait for table rows to repopulate with high-confidence OCR
                var tableReceipt = await _world.WaitForReceiptAsync(
                    "bazaar_item_table",
                    obs => obs.Confidence >= _policy.MinOcrConfidence,
                    TimeSpan.FromSeconds(2.5),
                    ct).ConfigureAwait(false);

                CurrentState = tableReceipt.Verified
                    ? SniperState.ScanRows
                    : TransitionToRecovery("Bazaar table failed to refresh within 2.5s.");
                break;

            case SniperState.ScanRows:
                var rows = await _world.ReadBazaarRowsAsync("bazaar_item_table", ct).ConfigureAwait(false);
                _selectedCandidate = rows
                    .Where(r => r.Confidence >= _policy.MinOcrConfidence && r.Quantity > 0)
                    .Where(r => _policy.TargetMaxUnitPrices.TryGetValue(r.ItemName, out int maxPrice)
                                && r.UnitPriceGold <= maxPrice)
                    .OrderBy(r => r.RowIndex)
                    .Select(r => (BazaarRowObservation?)r)
                    .FirstOrDefault();

                if (_selectedCandidate is null)
                {
                    if (_policy.RefreshCooldown > TimeSpan.Zero)
                    {
                        await Task.Delay(_policy.RefreshCooldown, ct).ConfigureAwait(false);
                    }
                    CurrentState = SniperState.VerifyShopOpen;
                }
                else
                {
                    CurrentState = SniperState.SelectTargetRow;
                }
                break;

            case SniperState.SelectTargetRow:
                if (_selectedCandidate is null)
                {
                    CurrentState = SniperState.ScanRows;
                    break;
                }

                await _actuator.ClickNormalizedRectAsync(_selectedCandidate.Value.RowBounds, ct).ConfigureAwait(false);

                // Receipt: Verify "BUY MORE" button becomes active/readable
                var buyBtnReceipt = await _world.WaitForReceiptAsync(
                    "bazaar_buy_more_button",
                    obs => obs.RawText.Contains("BUY", StringComparison.OrdinalIgnoreCase),
                    TimeSpan.FromSeconds(1.5),
                    ct).ConfigureAwait(false);

                CurrentState = buyBtnReceipt.Verified
                    ? SniperState.OpenBuyModal
                    : SniperState.RefreshListing; // Contested item: sniped by someone else, refresh safely
                break;

            case SniperState.OpenBuyModal:
                await _actuator.ClickAnchorAsync("bazaar_buy_more_button", ct).ConfigureAwait(false);

                var modalReceipt = await _world.WaitForReceiptAsync(
                    "bazaar_buy_confirm_dialog_button",
                    obs => obs.RawText.Contains("BUY", StringComparison.OrdinalIgnoreCase),
                    TimeSpan.FromSeconds(1.5),
                    ct).ConfigureAwait(false);

                CurrentState = modalReceipt.Verified
                    ? SniperState.ConfirmPurchase
                    : SniperState.RefreshListing;
                break;

            case SniperState.ConfirmPurchase:
                await _actuator.ClickAnchorAsync("bazaar_buy_confirm_dialog_button", ct).ConfigureAwait(false);
                CurrentState = SniperState.VerifyGoldReceipt;
                break;

            case SniperState.VerifyGoldReceipt:
                // Strict Gold Debit Receipt: Confirm gold actually decreased strictly below baseline
                var goldReceipt = await _world.WaitForReceiptAsync(
                    "bazaar_player_gold_label",
                    obs => TryParseGold(obs.RawText, out int newGold) && newGold < _goldBeforePurchase,
                    TimeSpan.FromSeconds(2.0),
                    ct).ConfigureAwait(false);

                // If gold debited, purchase is verified -> loop back to verify shop & refresh
                // If timed out without gold decrement (e.g. sold out before confirm), refresh safely without recording phantom debit
                CurrentState = goldReceipt.Verified
                    ? SniperState.VerifyShopOpen
                    : SniperState.RefreshListing;
                break;

            case SniperState.RecoveryAbort:
                // Failsafe state: remains in recovery until explicit caller reset
                break;
        }

        return CurrentState;
    }

    private SniperState TransitionToRecovery(string reason)
    {
        LastAbortReason = reason;
        CurrentState = SniperState.RecoveryAbort;
        return CurrentState;
    }

    /// <summary>
    /// Parses digit characters from gold string, ignoring commas, periods, spaces, and currency text.
    /// </summary>
    public static bool TryParseGold(string? raw, out int gold)
    {
        gold = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out gold);
    }
}
