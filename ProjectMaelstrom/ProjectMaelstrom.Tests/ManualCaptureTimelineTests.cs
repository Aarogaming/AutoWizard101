using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class ManualCaptureTimelineTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "maelstrom-timeline-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static ManualCaptureSnapshot Snapshot(string reason = "running") =>
        new(ManualCaptureMode.Normal, ManualCaptureState.Running, ManualPauseTrigger.None,
            MayDispatch: true, ExplicitLatch: false, QuietMs: 0, CountdownMs: 0, reason);

    [Fact]
    public void SamplesAreRelativeToOneUtcOrigin()
    {
        const long origin = 639000000000000000;
        var timeline = new ManualCaptureTimeline(_dir, origin);
        Assert.True(timeline.Add(Snapshot(), origin + TimeSpan.TicksPerMillisecond * 1500));
        timeline.Write();
        Assert.Equal(1, timeline.Count);
        var lines = File.ReadAllLines(Path.Combine(_dir, "backend-timeline.jsonl"));
        Assert.Single(lines);
        Assert.Contains("\"RelativeMs\":1500", lines[0]);
        var cross = File.ReadAllText(Path.Combine(_dir, "cross-compare.json"));
        Assert.Contains("639000000000000000", cross);
    }

    [Fact]
    public void TimelineIsBoundedAndRejectsOutOfWindowSamples()
    {
        const long origin = 639000000000000000;
        var timeline = new ManualCaptureTimeline(_dir, origin);
        Assert.False(timeline.Add(Snapshot(), origin - 1));
        Assert.False(timeline.Add(Snapshot(), origin + TimeSpan.TicksPerMinute * 11));
        for (var i = 0; i < ManualCaptureTimeline.MaximumSamples + 5; i++)
            timeline.Add(Snapshot(), origin + TimeSpan.TicksPerMillisecond * i);
        Assert.Equal(ManualCaptureTimeline.MaximumSamples, timeline.Count);
    }

    [Fact]
    public void MissingFrontEndFolderIsRejected()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            new ManualCaptureTimeline(_dir, 0, Path.Combine(_dir, "missing")));
    }
}
