using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class GameAudioRecordingTests
{
    private static ProcessAudioPacket Packet(int frames = 441, ulong position = 0, ulong qpc = 10_000_000, uint flags = 0) =>
        new(new byte[frames * 4], frames, position, qpc, flags);

    [Fact]
    public void SourceClockSurvivesGapsWithoutInventedSamples()
    {
        var timeline = new GameAudioPacketTimeline(4);
        var first = timeline.Append(Packet());
        var afterGap = timeline.Append(Packet(position: 1323, qpc: 10_300_000, flags: 1));
        Assert.Equal(1000, first.qpc_ms);
        Assert.Equal(1030, afterGap.qpc_ms);
        Assert.Equal(441, afterGap.frame_offset);
        Assert.Equal((ulong)1323, afterGap.device_position);
        Assert.True(afterGap.discontinuity);
        Assert.Equal(882, timeline.Frames);
    }

    [Fact]
    public void SilenceAndUnreliableClocksStayExplicit()
    {
        var timeline = new GameAudioPacketTimeline(4);
        var item = timeline.Append(Packet(qpc: 0, flags: 6));
        Assert.True(item.silent);
        Assert.True(item.timestamp_error);
        Assert.Equal((ulong)0, item.qpc_position_100ns);
        Assert.Throws<InvalidDataException>(() => new GameAudioPacketTimeline(4).Append(Packet(qpc: 0)));
    }

    [Fact]
    public void BackwardsOrOverlappingPacketsCannotLookContinuous()
    {
        var timeline = new GameAudioPacketTimeline(4);
        timeline.Append(Packet());
        Assert.Throws<InvalidDataException>(() => timeline.Append(Packet(position: 441, qpc: 9_999_999)));
        Assert.Throws<InvalidDataException>(() => timeline.Append(Packet(position: 441, qpc: 10_010_000)));
        Assert.Single(timeline.Packets);
    }

    [Fact]
    public void VirtualSourceZeroDevicePositionsRemainRawAndUnqualified()
    {
        var timeline = new GameAudioPacketTimeline(4);
        timeline.Append(Packet());
        timeline.Append(Packet(position: 0, qpc: 10_100_000));
        Assert.False(timeline.DevicePositionQualified);
        Assert.Equal((ulong)0, timeline.Packets[1].device_position);
        Assert.Equal(1010, timeline.Packets[1].qpc_ms);
        Assert.Equal(882, timeline.Frames);
    }

    [Fact]
    public void MalformedAndUnboundedPacketsAreRejected()
    {
        var timeline = new GameAudioPacketTimeline(4);
        Assert.Throws<InvalidDataException>(() => timeline.Append(Packet() with { PcmBytes = [0, 0] }));
        Assert.Throws<InvalidDataException>(() => timeline.Append(Packet(frames: 0)));
        Assert.Throws<InvalidDataException>(() => timeline.Append(Packet(flags: 8)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameAudioPacketTimeline(0));
        for (var i = 0; i < GameAudioPacketTimeline.MaximumPackets; i++)
            timeline.Append(Packet(1, (ulong)i, 10_000_000UL + (ulong)i));
        Assert.Throws<InvalidDataException>(() => timeline.Append(Packet(1, (ulong)GameAudioPacketTimeline.MaximumPackets, 10_030_000)));
    }
}
