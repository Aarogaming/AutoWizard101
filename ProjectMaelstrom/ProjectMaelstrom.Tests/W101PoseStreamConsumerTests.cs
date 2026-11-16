using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101PoseStreamConsumerTests
{
    private static readonly string Stream = new('a', 32);
    private static W101PoseStreamConsumer Consumer() => new("run", Stream, new(44, 12, 99, "test", "path", "hash"), "zone", "epoch", 50, 0);
    private static string Packet(long sequence = 1, string? zone = null, string? stream = null) => JsonSerializer.Serialize(new
    {
        kind = "moving-pose-candidate",
        zone = zone ?? "zone",
        streamId = stream ?? Stream,
        sequence,
        observedUtcTicks = 10000000,
        sampleAgeMs = 1,
        binding = new { runId = "run", target = new { handle = 44, processId = 12, startedUtcTicks = 99 } },
        sample = new { position = new { x = 1, y = 2, z = 3 }, yawRadians = 0 }
    });
    [Fact]
    public void ValidPacketThenExpiryClearsPose()
    {
        var c = Consumer(); Assert.True(c.Accept(Packet(), 10000000, 100, true));
        Assert.NotNull(c.ReadCandidate(110, true)); Assert.Null(c.ReadCandidate(151, true)); Assert.True(c.Ended);
    }
    [Theory]
    [InlineData("moving-pose-ended")]
    [InlineData("moving-pose-invalidated")]
    public void TerminalEventCannotRevive(string kind)
    {
        var c = Consumer(); Assert.True(c.Accept(Packet(), 10000000, 100, true));
        Assert.False(c.Accept(JsonSerializer.Serialize(new { kind }), 10000000, 101, true));
        Assert.Null(c.ReadCandidate(102, true)); Assert.False(c.Accept(Packet(2), 10000000, 103, true));
    }
    [Fact]
    public void SequenceGapClearsLastPose()
    {
        var c = Consumer(); c.Accept(Packet(), 10000000, 100, true);
        Assert.False(c.Accept(Packet(3), 10000000, 110, true)); Assert.Null(c.ReadCandidate(111, true));
    }
    [Fact] public void ChangedZoneFails() => Assert.False(Consumer().Accept(Packet(zone: "other"), 10000000, 100, true));
    [Fact] public void ChangedStreamFails() => Assert.False(Consumer().Accept(Packet(stream: new('b', 32)), 10000000, 100, true));
    [Fact]
    public void OwnerLossClearsPose()
    {
        var c = Consumer(); c.Accept(Packet(), 10000000, 100, true); Assert.Null(c.ReadCandidate(101, false)); Assert.True(c.Ended);
    }
    [Fact] public void MalformedEventFails() => Assert.False(Consumer().Accept("{}", 10000000, 100, true));
}
