using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101PoseStreamObservationTests
{
    private static readonly string Stream = new('a', 32);
    private static W101PoseStreamObservation Observer() => new("run", Stream, new(44, 12, 99, "test", "path", "hash"));
    private static string Packet(long sequence = 1, string? stream = null, string zone = "OldeTown", string? wizard = null) => JsonSerializer.Serialize(new
    {
        kind = "moving-pose-candidate",
        streamId = stream ?? Stream,
        sequence,
        zone,
        wizardKey = wizard ?? new string('a', 64),
        wizardOwnerChecked = true,
        observedUtcTicks = 10000000L,
        sampleAgeMs = 1,
        binding = new { runId = "run", target = new { handle = 44, processId = 12, startedUtcTicks = 99 } },
        sample = new { position = new { x = 1743.090087890625, y = 241.71363830566406, z = 2.200042724609375 }, yawRadians = 1.6863594055175781 }
    });
    [Fact]
    public void ChangedWizardCannotContinueSameProcessStream()
    {
        var o = Observer(); o.Receive(Packet(), 10000000, 100, true);
        o.Receive(Packet(2, wizard: new string('b', 64)), 10100000, 110, true);
        Assert.True(o.Ended); Assert.Equal(1, o.Accepted);
        Assert.Equal("wizard-identity-changed-or-unverified", o.Reason);
    }
    [Fact]
    public void HostPinnedStreamRejectsForeignStream()
    {
        var o = Observer(); o.Receive(Packet(stream: new('b', 32)), 10000000, 100, true);
        Assert.True(o.Ended); Assert.Equal(0, o.Accepted);
    }
    [Fact]
    public void ExcessDeliveryDelayRejectsSample()
    {
        var o = Observer(); o.Receive(Packet(), 13000000, 1000, true);
        Assert.True(o.Ended); Assert.Equal(0, o.Accepted);
    }
    [Fact]
    public void MissingSequenceEndsStream()
    {
        var o = Observer(); o.Receive(Packet(), 10000000, 100, true);
        o.Receive(Packet(3), 10100000, 110, true);
        Assert.True(o.Ended); Assert.Equal(1, o.Accepted);
    }
    [Theory]
    [InlineData("OldeTown")]
    [InlineData("Commons")]
    public void LoadingPreventsBothSameZoneAndCrossZoneRevival(string zone)
    {
        var o = Observer(); o.Receive(Packet(), 10000000, 100, true);
        o.Receive("{\"kind\":\"loading\",\"loading\":true}", 10100000, 110, true);
        o.Receive(Packet(2, zone: zone), 10200000, 120, true);
        Assert.True(o.SawLoading); Assert.True(o.Ended); Assert.Equal(1, o.Accepted);
    }
    [Fact]
    public void EofRequiresNewConsumer()
    {
        var o = Observer(); o.Receive(Packet(), 10000000, 100, true); o.End("worker-ended");
        o.Receive(Packet(2), 10100000, 110, true);
        Assert.Equal(1, o.Accepted); Assert.True(o.Ended);
    }
    [Fact]
    public void OwnershipLossPreventsFurtherConsumption()
    {
        var o = Observer(); o.Receive(Packet(), 10000000, 100, false);
        o.Receive(Packet(), 10100000, 110, true);
        Assert.True(o.Ended); Assert.Equal(0, o.Accepted);
    }
}
