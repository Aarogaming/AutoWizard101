using System.Diagnostics;
using ProjectMaelstrom.Utilities.Circuit;
using ProjectMaelstrom.Utilities.Contracts;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class UniversalBusTests
{
    [Fact]
    public void CircuitFeed_Serializes64BitWorldBitsAndRegistersLosslessly()
    {
        ulong rawBits = (ulong)(WorldBits.LeaseActive | WorldBits.ClientFocused | WorldBits.BazaarOpen | WorldBits.GoldAboveFloor);
        var activeNames = new[] { "LeaseActive", "ClientFocused", "BazaarOpen", "GoldAboveFloor" };
        var confidences = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["bazaar_player_gold_label"] = 0.99f,
            ["native_idle_banner"] = 0.05f
        };

        var snapshot = new CircuitSnapshot(
            SchemaVersion: "circuit.v1",
            TimestampUtcTicks: DateTime.UtcNow.Ticks,
            WorldBitsRaw: rawBits,
            ActiveBitNames: activeNames,
            Registers: new CircuitRegisters(
                Gold: 142500,
                Energy: 80,
                CandidateRowIndex: 3,
                CandidateUnitPrice: 2400,
                CandidateQuantity: 10),
            AnchorConfidences: confidences);

        // Serialize to JSON
        var json = snapshot.ToJson();
        Assert.Contains("\"schema_version\":\"circuit.v1\"", json);
        Assert.Contains("\"world_bits_raw\":", json);
        Assert.Contains("\"gold\":142500", json);
        Assert.Contains("\"candidate_unit_price\":2400", json);
        Assert.Contains("LeaseActive", json);

        // Deserialize from JSON
        var roundtripped = CircuitSnapshot.FromJson(json);
        Assert.Equal(snapshot.SchemaVersion, roundtripped.SchemaVersion);
        Assert.Equal(snapshot.WorldBitsRaw, roundtripped.WorldBitsRaw);
        Assert.Equal(snapshot.Registers.Gold, roundtripped.Registers.Gold);
        Assert.Equal(snapshot.Registers.CandidateRowIndex, roundtripped.Registers.CandidateRowIndex);
        Assert.Equal(snapshot.Registers.CandidateUnitPrice, roundtripped.Registers.CandidateUnitPrice);
        Assert.Equal(4, roundtripped.ActiveBitNames.Count);
        Assert.True(roundtripped.HasBit(WorldBits.LeaseActive));
        Assert.True(roundtripped.HasBit(WorldBits.GoldAboveFloor));
        Assert.False(roundtripped.HasBit(WorldBits.ConfirmModalOpen));
    }

    [Fact]
    public async Task FastPerceptualAdapter_DropsLeaseBit_WhenHumanTouchesInput()
    {
        bool humanTouchingInput = false;

        var backingOcr = new MockBackingOcr();
        var adapter = new FastPerceptualAdapter(
            backingOcrState: backingOcr,
            leaseProbe: () => !humanTouchingInput, // Lease drops when human touches input
            focusProbe: () => true,
            minGoldReserveFloor: 25000);

        // Scan 1: No human input -> Lease bit is High (1)
        var snap1 = await adapter.ReadCircuitSnapshotAsync();
        Assert.True(snap1.HasBit(WorldBits.LeaseActive));
        Assert.Contains("LeaseActive", snap1.ActiveBitNames);

        // Scan 2: Human touches mouse/keyboard -> Lease bit drops Low (0)
        humanTouchingInput = true;
        var snap2 = await adapter.ReadCircuitSnapshotAsync();
        Assert.False(snap2.HasBit(WorldBits.LeaseActive));
        Assert.DoesNotContain("LeaseActive", snap2.ActiveBitNames);
    }

    [Fact]
    public async Task FastPerceptualAdapter_SetsGoldAboveFloorBit_OnlyWhenAboveReserve()
    {
        int observedGold = 50000;
        var backingOcr = new MockBackingOcr(goldGetter: () => observedGold);

        var adapter = new FastPerceptualAdapter(
            backingOcrState: backingOcr,
            leaseProbe: () => true,
            focusProbe: () => true,
            minGoldReserveFloor: 25000);

        // When gold (50000) > floor (25000): GoldAboveFloor is active
        var snap1 = await adapter.ReadCircuitSnapshotAsync();
        Assert.True(snap1.HasBit(WorldBits.GoldAboveFloor));
        Assert.Contains("GoldAboveFloor", snap1.ActiveBitNames);
        Assert.Equal(50000, snap1.Registers.Gold);

        // When gold (15000) <= floor (25000): GoldAboveFloor is inactive
        observedGold = 15000;
        var snap2 = await adapter.ReadCircuitSnapshotAsync();
        Assert.False(snap2.HasBit(WorldBits.GoldAboveFloor));
        Assert.DoesNotContain("GoldAboveFloor", snap2.ActiveBitNames);
        Assert.Equal(15000, snap2.Registers.Gold);
    }

    [Fact]
    public async Task AtomicActuator_RefusesExecution_WhenLeaseBitIsLow()
    {
        bool leaseActive = false; // Human has taken over
        bool clickAttempted = false;

        var mockActuator = new MockActuator(
            hasLease: () => leaseActive,
            onClickAnchor: a =>
            {
                clickAttempted = true;
                return Task.FromResult(true);
            });

        var backingOcr = new MockBackingOcr();

        var receipt = await AtomicAnchorActuator.ActuateAsync(
            actuator: mockActuator,
            world: backingOcr,
            anchorId: "bazaar_buy_confirm_dialog_button",
            receiptAnchorId: "bazaar_item_table",
            expectedOcr: "REFRESHED",
            timeout: TimeSpan.FromSeconds(1));

        // Invariant: Zero click actuation when lease is revoked
        Assert.False(receipt.Verified);
        Assert.Equal("LEASE_NOT_HELD", receipt.ObservedValue);
        Assert.False(clickAttempted);
    }

    private sealed class MockBackingOcr : IWorldState
    {
        private readonly Func<int>? _goldGetter;

        public MockBackingOcr(Func<int>? goldGetter = null)
        {
            _goldGetter = goldGetter;
        }

        public Task<OcrObservation> ReadAnchorTextAsync(string anchorId, CancellationToken ct = default)
        {
            return anchorId switch
            {
                "native_idle_banner" => Task.FromResult(new OcrObservation("", 0.0f, new NormalizedRect(), 0)),
                "bazaar_player_gold_label" => Task.FromResult(new OcrObservation($"{_goldGetter?.Invoke() ?? 50000} Gold", 0.95f, new NormalizedRect(), 0)),
                "bazaar_buy_more_button" => Task.FromResult(new OcrObservation("BUY MORE", 0.95f, new NormalizedRect(), 0)),
                "bazaar_buy_confirm_dialog_button" => Task.FromResult(new OcrObservation("BUY", 0.95f, new NormalizedRect(), 0)),
                _ => Task.FromResult(new OcrObservation("", 0.0f, new NormalizedRect(), 0))
            };
        }

        public Task<IReadOnlyList<BazaarRowObservation>> ReadBazaarRowsAsync(string tableAnchorId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BazaarRowObservation>>(Array.Empty<BazaarRowObservation>());

        public Task<StateReceipt> WaitForReceiptAsync(string anchorId, Func<OcrObservation, bool> predicate, TimeSpan timeout, CancellationToken ct = default) =>
            Task.FromResult(new StateReceipt(true, anchorId, "REFRESHED", 0.95f, TimeSpan.FromMilliseconds(50)));
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
