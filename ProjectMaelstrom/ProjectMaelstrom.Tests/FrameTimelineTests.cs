using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class FrameTimelineTests
{
    [Fact]
    public void ReportsMissingSlotsAndRejectsOutOfOrderTimes()
    {
        var timeline = new FrameTimeline(10);
        timeline.Add(100);
        timeline.Add(200);
        timeline.Add(500);
        Assert.Equal(3, timeline.Frames);
        Assert.Equal(2, timeline.EstimatedMissing);
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Add(499));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameTimeline(0));
    }
}
