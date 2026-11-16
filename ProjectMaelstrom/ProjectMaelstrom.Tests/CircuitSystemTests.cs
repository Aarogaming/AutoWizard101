using System.Diagnostics;
using ProjectMaelstrom.Utilities.Cards;
using ProjectMaelstrom.Utilities.Circuit;
using ProjectMaelstrom.Utilities.Contracts;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class CircuitSystemTests
{
    [Fact]
    public void AnchorRegistry_LoadsDefaultAnchors_AllRequiredAnchorsPresent()
    {
        var registry = AnchorRegistry.LoadDefault();

        Assert.True(registry.TryGetAnchor("bazaar_refresh_button", out var refresh));
        Assert.True(refresh.Bounds.W > 0);

        Assert.True(registry.TryGetAnchor("bazaar_item_table", out var table));
        Assert.Equal(10, table.RowCount);
        Assert.True(table.RowStrideY > 0);
        Assert.Contains("name", table.Columns.Keys);
        Assert.Contains("quantity", table.Columns.Keys);
        Assert.Contains("price", table.Columns.Keys);

        Assert.True(registry.TryGetAnchor("native_idle_banner", out var idle));
        Assert.Equal("YOU ARE IDLE", idle.ExpectedOcr);
    }

    [Fact]
    public async Task FastPerceptualAdapter_WaitForReceipt_CompletesOnPredicateMatch()
    {
        int callCount = 0;
        var mockOcr = new MockBackingState(
            onReadAnchor: id =>
            {
                callCount++;
                string text = callCount >= 3 ? "BUY" : "WAITING";
                return new OcrObservation(text, 0.95f, new NormalizedRect(), 0);
            });

        var adapter = new FastPerceptualAdapter(
            backingOcrState: mockOcr,
            leaseProbe: () => true,
            focusProbe: () => true);

        var receipt = await adapter.WaitForReceiptAsync(
            "bazaar_buy_confirm_dialog_button",
            obs => obs.RawText == "BUY",
            TimeSpan.FromSeconds(1));

        Assert.True(receipt.Verified);
        Assert.Equal("BUY", receipt.ObservedValue);
        Assert.True(callCount >= 3);
    }

    [Fact]
    public async Task EndToEnd_Layer1Adapter_Drives_Layer2BazaarSniperCard()
    {
        // Wire FastPerceptualAdapter as IWorldState directly into BazaarSniperCard
        bool leaseHeld = true;
        var clickedAnchors = new List<string>();
        int gold = 100000;

        var actuator = new MockActuator(
            hasLease: () => leaseHeld,
            onClickAnchor: a =>
            {
                clickedAnchors.Add(a);
                if (a == "bazaar_buy_confirm_dialog_button")
                {
                    gold -= 2400; // Deduct purchase
                }
                return Task.FromResult(true);
            });

        var mockOcr = new MockBackingState(
            onReadAnchor: id => id switch
            {
                "bazaar_refresh_button" => new OcrObservation("REFRESH", 0.95f, new NormalizedRect(), 0),
                "bazaar_item_table" => new OcrObservation("TABLE_LOADED", 0.95f, new NormalizedRect(), 0),
                "bazaar_buy_more_button" => new OcrObservation("BUY MORE", 0.95f, new NormalizedRect(), 0),
                "bazaar_buy_confirm_dialog_button" => new OcrObservation("BUY", 0.95f, new NormalizedRect(), 0),
                "bazaar_player_gold_label" => new OcrObservation($"{gold} Gold", 0.99f, new NormalizedRect(), 0),
                _ => new OcrObservation("", 0.0f, new NormalizedRect(), 0)
            },
            onReadRows: _ => new List<BazaarRowObservation>
            {
                new(0, "Sunstone", 5, 2400, new NormalizedRect(0.26, 0.275, 0.42, 0.044), 0.99f)
            });

        var adapter = new FastPerceptualAdapter(
            backingOcrState: mockOcr,
            leaseProbe: () => leaseHeld,
            focusProbe: () => true,
            minGoldReserveFloor: 10000);

        var policy = new SniperPolicy(
            TargetMaxUnitPrices: new Dictionary<string, int> { ["Sunstone"] = 3000 },
            MinGoldReserveFloor: 10000,
            RefreshCooldown: TimeSpan.FromMilliseconds(10));

        var card = new BazaarSniperCard(
            world: adapter,
            actuator: actuator,
            policy: policy);

        // Step 1: VerifyShopOpen
        var res1 = await card.StepAsync();
        Assert.Equal(SniperState.RefreshListing, res1);

        // Step 2: RefreshListing
        var res2 = await card.StepAsync();
        Assert.Equal(SniperState.ScanRows, res2);

        // Step 3: ScanRows
        var res3 = await card.StepAsync();
        Assert.Equal(SniperState.SelectTargetRow, res3);

        // Step 4: SelectTargetRow
        var res4 = await card.StepAsync();
        Assert.Equal(SniperState.OpenBuyModal, res4);

        // Step 5: OpenBuyModal
        var res5 = await card.StepAsync();
        Assert.Equal(SniperState.ConfirmPurchase, res5);

        // Step 6: ConfirmPurchase
        var res6 = await card.StepAsync();
        Assert.Equal(SniperState.VerifyGoldReceipt, res6);

        // Step 7: VerifyGoldReceipt -> completes purchase and loops to VerifyShopOpen
        var res7 = await card.StepAsync();
        Assert.Equal(SniperState.VerifyShopOpen, res7);
        Assert.Equal(97600, gold);
    }

    private sealed class MockBackingState : IWorldState
    {
        private readonly Func<string, OcrObservation>? _onReadAnchor;
        private readonly Func<string, IReadOnlyList<BazaarRowObservation>>? _onReadRows;

        public MockBackingState(
            Func<string, OcrObservation>? onReadAnchor = null,
            Func<string, IReadOnlyList<BazaarRowObservation>>? onReadRows = null)
        {
            _onReadAnchor = onReadAnchor;
            _onReadRows = onReadRows;
        }

        public Task<OcrObservation> ReadAnchorTextAsync(string anchorId, CancellationToken ct = default) =>
            Task.FromResult(_onReadAnchor?.Invoke(anchorId) ?? new OcrObservation("", 0.0f, new NormalizedRect(), 0));

        public Task<IReadOnlyList<BazaarRowObservation>> ReadBazaarRowsAsync(string tableAnchorId, CancellationToken ct = default) =>
            Task.FromResult(_onReadRows?.Invoke(tableAnchorId) ?? Array.Empty<BazaarRowObservation>());

        public async Task<StateReceipt> WaitForReceiptAsync(string anchorId, Func<OcrObservation, bool> predicate, TimeSpan timeout, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                var obs = await ReadAnchorTextAsync(anchorId, ct).ConfigureAwait(false);
                if (predicate(obs)) return new StateReceipt(true, anchorId, obs.RawText, obs.Confidence, sw.Elapsed);
                await Task.Delay(10, ct).ConfigureAwait(false);
            }
            var finalObs = await ReadAnchorTextAsync(anchorId, ct).ConfigureAwait(false);
            return new StateReceipt(false, anchorId, finalObs.RawText, finalObs.Confidence, sw.Elapsed);
        }
    }

    private sealed class MockActuator : IActuator
    {
        private readonly Func<bool> _hasLease;
        private readonly Func<string, Task<bool>> _onClickAnchor;

        public MockActuator(Func<bool> hasLease, Func<string, Task<bool>> onClickAnchor)
        {
            _hasLease = hasLease;
            _onClickAnchor = onClickAnchor;
        }

        public bool HasActiveLease => _hasLease();

        public Task<bool> ClickAnchorAsync(string anchorId, CancellationToken ct = default) =>
            _onClickAnchor(anchorId);

        public Task<bool> ClickNormalizedRectAsync(NormalizedRect rect, CancellationToken ct = default) =>
            Task.FromResult(true);
    }
}
