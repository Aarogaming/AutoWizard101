using Xunit;
using ProjectMaelstrom.Utilities.Contracts;
using ProjectMaelstrom.Utilities.Cards;

namespace ProjectMaelstrom.Tests;

public sealed class BazaarSniperCardTests
{
    private sealed class MockWorldState : IWorldState
    {
        public string GoldText = "150000";
        public float GoldConfidence = 0.95f;
        public List<BazaarRowObservation> Rows = new();
        public bool TableRefreshSuccess = true;
        public bool BuyButtonVisible = true;
        public bool ConfirmButtonVisible = true;

        public Task<OcrObservation> ReadAnchorTextAsync(string anchorId, CancellationToken ct = default)
        {
            var text = anchorId == "bazaar_player_gold_label" ? GoldText : "sample text";
            return Task.FromResult(new OcrObservation(text, GoldConfidence, default, 1000));
        }

        public Task<IReadOnlyList<BazaarRowObservation>> ReadBazaarRowsAsync(string tableAnchorId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BazaarRowObservation>>(Rows);

        public Task<StateReceipt> WaitForReceiptAsync(
            string anchorId,
            Func<OcrObservation, bool> predicate,
            TimeSpan timeout,
            CancellationToken ct = default)
        {
            string observedText;
            float confidence = 0.95f;

            switch (anchorId)
            {
                case "bazaar_item_table":
                    observedText = TableRefreshSuccess ? "Table Rows Loaded" : "Timeout";
                    confidence = TableRefreshSuccess ? 0.95f : 0.40f;
                    break;
                case "bazaar_buy_more_button":
                    observedText = BuyButtonVisible ? "BUY MORE" : "";
                    break;
                case "bazaar_buy_confirm_dialog_button":
                    observedText = ConfirmButtonVisible ? "BUY" : "";
                    break;
                case "bazaar_player_gold_label":
                    observedText = GoldText;
                    confidence = GoldConfidence;
                    break;
                default:
                    observedText = "OK";
                    break;
            }

            var obs = new OcrObservation(observedText, confidence, default, 1000);
            bool verified = predicate(obs);
            return Task.FromResult(new StateReceipt(verified, anchorId, observedText, confidence, TimeSpan.FromMilliseconds(50)));
        }
    }

    private sealed class MockActuator : IActuator
    {
        public bool HasActiveLease { get; set; } = true;
        public List<string> ClickedAnchors = new();
        public List<NormalizedRect> ClickedRects = new();

        public Task<bool> ClickAnchorAsync(string anchorId, CancellationToken ct = default)
        {
            ClickedAnchors.Add(anchorId);
            return Task.FromResult(true);
        }

        public Task<bool> ClickNormalizedRectAsync(NormalizedRect rect, CancellationToken ct = default)
        {
            ClickedRects.Add(rect);
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task Sniper_SuccessfulCycle_DebitsGoldAndReturnsToVerifyShop()
    {
        // Arrange
        var world = new MockWorldState();
        var actuator = new MockActuator();
        var policy = new SniperPolicy(
            TargetMaxUnitPrices: new Dictionary<string, int> { ["Titanium"] = 500 },
            MinGoldReserveFloor: 10000,
            RefreshCooldown: TimeSpan.Zero);

        world.Rows.Add(new BazaarRowObservation(0, "Titanium", 5, 450, new NormalizedRect(0.26, 0.275, 0.42, 0.044), 0.95f));

        var card = new BazaarSniperCard(world, actuator, policy);

        // Step 1: VerifyShopOpen -> RefreshListing
        Assert.Equal(SniperState.VerifyShopOpen, card.CurrentState);
        await card.StepAsync();
        Assert.Equal(SniperState.RefreshListing, card.CurrentState);
        Assert.Equal(150000, card.GoldBeforePurchase);

        // Step 2: RefreshListing -> ScanRows
        await card.StepAsync();
        Assert.Equal(SniperState.ScanRows, card.CurrentState);
        Assert.Contains("bazaar_refresh_button", actuator.ClickedAnchors);

        // Step 3: ScanRows -> SelectTargetRow
        await card.StepAsync();
        Assert.Equal(SniperState.SelectTargetRow, card.CurrentState);
        Assert.NotNull(card.LastSelectedCandidate);
        Assert.Equal("Titanium", card.LastSelectedCandidate.Value.ItemName);

        // Step 4: SelectTargetRow -> OpenBuyModal
        await card.StepAsync();
        Assert.Equal(SniperState.OpenBuyModal, card.CurrentState);
        Assert.Single(actuator.ClickedRects);

        // Step 5: OpenBuyModal -> ConfirmPurchase
        await card.StepAsync();
        Assert.Equal(SniperState.ConfirmPurchase, card.CurrentState);
        Assert.Contains("bazaar_buy_more_button", actuator.ClickedAnchors);

        // Step 6: ConfirmPurchase -> VerifyGoldReceipt
        await card.StepAsync();
        Assert.Equal(SniperState.VerifyGoldReceipt, card.CurrentState);
        Assert.Contains("bazaar_buy_confirm_dialog_button", actuator.ClickedAnchors);

        // Simulate 2,250 gold debit on game client
        world.GoldText = "147750";

        // Step 7: VerifyGoldReceipt -> VerifyShopOpen (Complete loop)
        await card.StepAsync();
        Assert.Equal(SniperState.VerifyShopOpen, card.CurrentState);
    }

    [Fact]
    public async Task Sniper_HumanTakeover_PausesInPlaceWithoutStepping()
    {
        var world = new MockWorldState();
        var actuator = new MockActuator { HasActiveLease = false }; // Human actively using input
        var policy = new SniperPolicy(
            TargetMaxUnitPrices: new Dictionary<string, int> { ["Titanium"] = 500 },
            MinGoldReserveFloor: 10000,
            RefreshCooldown: TimeSpan.Zero);

        var card = new BazaarSniperCard(world, actuator, policy);

        var state = await card.StepAsync();

        Assert.Equal(SniperState.VerifyShopOpen, state);
        Assert.Empty(actuator.ClickedAnchors);
    }

    [Fact]
    public async Task Sniper_GoldFloorReached_AbortsToRecovery()
    {
        var world = new MockWorldState { GoldText = "8500" }; // Below 10,000 floor
        var actuator = new MockActuator();
        var policy = new SniperPolicy(
            TargetMaxUnitPrices: new Dictionary<string, int> { ["Titanium"] = 500 },
            MinGoldReserveFloor: 10000,
            RefreshCooldown: TimeSpan.Zero);

        var card = new BazaarSniperCard(world, actuator, policy);
        var state = await card.StepAsync();

        Assert.Equal(SniperState.RecoveryAbort, state);
        Assert.Contains("floor reached", card.LastAbortReason);
    }

    [Fact]
    public async Task Sniper_GoldUnreadable_AbortsToRecovery()
    {
        var world = new MockWorldState { GoldText = "???" };
        var actuator = new MockActuator();
        var policy = new SniperPolicy(
            TargetMaxUnitPrices: new Dictionary<string, int> { ["Titanium"] = 500 },
            MinGoldReserveFloor: 10000,
            RefreshCooldown: TimeSpan.Zero);

        var card = new BazaarSniperCard(world, actuator, policy);
        var state = await card.StepAsync();

        Assert.Equal(SniperState.RecoveryAbort, state);
        Assert.Contains("unreadable", card.LastAbortReason);
    }

    [Fact]
    public async Task Sniper_NoMatchingRow_LoopsToVerifyShop()
    {
        var world = new MockWorldState();
        var actuator = new MockActuator();
        var policy = new SniperPolicy(
            TargetMaxUnitPrices: new Dictionary<string, int> { ["Titanium"] = 500 },
            MinGoldReserveFloor: 10000,
            RefreshCooldown: TimeSpan.Zero);

        // Add row with price exceeding maximum threshold
        world.Rows.Add(new BazaarRowObservation(0, "Titanium", 5, 800, default, 0.95f));

        var card = new BazaarSniperCard(world, actuator, policy);

        await card.StepAsync(); // VerifyShopOpen -> RefreshListing
        await card.StepAsync(); // RefreshListing -> ScanRows
        await card.StepAsync(); // ScanRows -> VerifyShopOpen (no match, loops)

        Assert.Equal(SniperState.VerifyShopOpen, card.CurrentState);
        Assert.Null(card.LastSelectedCandidate);
    }

    [Fact]
    public async Task Sniper_ItemSnipedBeforeModal_RevertsToRefreshListing()
    {
        var world = new MockWorldState { BuyButtonVisible = false }; // Modal did not activate (item gone)
        var actuator = new MockActuator();
        var policy = new SniperPolicy(
            TargetMaxUnitPrices: new Dictionary<string, int> { ["Titanium"] = 500 },
            MinGoldReserveFloor: 10000,
            RefreshCooldown: TimeSpan.Zero);

        world.Rows.Add(new BazaarRowObservation(0, "Titanium", 5, 450, default, 0.95f));

        var card = new BazaarSniperCard(world, actuator, policy);

        await card.StepAsync(); // VerifyShopOpen -> RefreshListing
        await card.StepAsync(); // RefreshListing -> ScanRows
        await card.StepAsync(); // ScanRows -> SelectTargetRow
        await card.StepAsync(); // SelectTargetRow -> RefreshListing (Buy button failed to appear)

        Assert.Equal(SniperState.RefreshListing, card.CurrentState);
    }

    [Fact]
    public async Task Sniper_ItemSnipedBeforeConfirm_NoGoldDebit_RevertsToRefreshListing()
    {
        var world = new MockWorldState(); // GoldText remains 150000 (never debited)
        var actuator = new MockActuator();
        var policy = new SniperPolicy(
            TargetMaxUnitPrices: new Dictionary<string, int> { ["Titanium"] = 500 },
            MinGoldReserveFloor: 10000,
            RefreshCooldown: TimeSpan.Zero);

        world.Rows.Add(new BazaarRowObservation(0, "Titanium", 5, 450, default, 0.95f));

        var card = new BazaarSniperCard(world, actuator, policy);

        await card.StepAsync(); // VerifyShopOpen -> RefreshListing
        await card.StepAsync(); // RefreshListing -> ScanRows
        await card.StepAsync(); // ScanRows -> SelectTargetRow
        await card.StepAsync(); // SelectTargetRow -> OpenBuyModal
        await card.StepAsync(); // OpenBuyModal -> ConfirmPurchase
        await card.StepAsync(); // ConfirmPurchase -> VerifyGoldReceipt

        // In VerifyGoldReceipt, gold did NOT decrease:
        await card.StepAsync(); // VerifyGoldReceipt -> RefreshListing (clean reset without phantom debit)

        Assert.Equal(SniperState.RefreshListing, card.CurrentState);
    }

    [Fact]
    public async Task Sniper_TableRefreshTimeout_AbortsToRecovery()
    {
        var world = new MockWorldState { TableRefreshSuccess = false };
        var actuator = new MockActuator();
        var policy = new SniperPolicy(
            TargetMaxUnitPrices: new Dictionary<string, int> { ["Titanium"] = 500 },
            MinGoldReserveFloor: 10000,
            RefreshCooldown: TimeSpan.Zero);

        var card = new BazaarSniperCard(world, actuator, policy);

        await card.StepAsync(); // VerifyShopOpen -> RefreshListing
        await card.StepAsync(); // RefreshListing -> RecoveryAbort (receipt timed out)

        Assert.Equal(SniperState.RecoveryAbort, card.CurrentState);
        Assert.Contains("failed to refresh", card.LastAbortReason);
    }

    [Theory]
    [InlineData("150,000", 150000)]
    [InlineData("150.000 Gold", 150000)]
    [InlineData("  50,420 ", 50420)]
    [InlineData("$999", 999)]
    [InlineData("0", 0)]
    public void TryParseGold_HandlesCommasAndSymbols(string raw, int expected)
    {
        bool success = BazaarSniperCard.TryParseGold(raw, out int actual);
        Assert.True(success);
        Assert.Equal(expected, actual);
    }
}
