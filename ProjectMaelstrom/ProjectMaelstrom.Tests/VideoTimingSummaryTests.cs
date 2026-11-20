using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class VideoTimingSummaryTests
{
    [Fact]
    public void ReportsActualCadenceAndTailGapsWithoutInventingDrops()
    {
        var summary = VideoTimingSummary.Create(new double[] { 100, 110, 130, 230 });
        Assert.Equal(4, summary.Frames);
        Assert.Equal(130, summary.DurationMs);
        Assert.Equal(20, summary.GapP50Ms);
        Assert.Equal(100, summary.GapP95Ms);
        Assert.Equal(100, summary.GapP99Ms);
        Assert.Equal(100, summary.MaximumGapMs);
        Assert.Null(VideoTimingSummary.Create(new double[] { 1 }).DeliveredFps);
        Assert.Throws<InvalidDataException>(() => VideoTimingSummary.Create(new double[] { 3, 2 }));
    }

    [Fact]
    public void ReportsPlaybackTimingErrorAgainstCaptureRatherThanWallClockOrigin()
    {
        var error = VideoTimingError.Create(new double[] { 1000, 1033, 1066, 1100 }, new double[] { 0, 31, 50, 91 });
        Assert.NotNull(error);
        Assert.Equal(2, error.MedianMs);
        Assert.Equal(16, error.P95Ms);
        Assert.Equal(16, error.MaximumMs);
        Assert.Throws<InvalidDataException>(() => VideoTimingError.Create(new double[] { 1 }, Array.Empty<double>()));
    }

    [Fact]
    public void ReportsMissingStaticTailSeparatelyFromPlaybackTimestampError()
    {
        var staticCoverage = VideoCoverageSummary.Create(new double[] { 1000 }, 11000, .016);
        Assert.NotNull(staticCoverage);
        Assert.Equal(10000, staticCoverage.ObservedDurationMs);
        Assert.Equal(9984, staticCoverage.UnrepresentedTailMs);
        var fullCoverage = VideoCoverageSummary.Create(new double[] { 1000, 2990 }, 3000, 2);
        Assert.Equal(0, fullCoverage!.UnrepresentedTailMs);
        var boundary = VideoCoverageSummary.Create(new double[] { 1000, 3001.5 }, 3000, 2.02);
        Assert.Equal(1.5, boundary!.LastFrameAfterStopMs);
        Assert.Equal(2001.5, boundary.ObservedDurationMs);
        Assert.Null(VideoCoverageSummary.Create(Array.Empty<double>(), 1000, 0));
        Assert.Throws<InvalidDataException>(() => VideoCoverageSummary.Create(new double[] { 1000, 2000 }, 1500, 1));
    }

    [Fact]
    public void TimingDistributionsPreserveSignedClockDifferences()
    {
        var result = TimingDistribution.Create(new double[] { -2, 1, 3, 4 });
        Assert.Equal(4, result!.Samples);
        Assert.Equal(1, result.MedianMs);
        Assert.Equal(4, result.P95Ms);
        Assert.Equal(4, result.MaximumMs);
        Assert.Null(TimingDistribution.Create(Array.Empty<double>()));
        Assert.Throws<InvalidDataException>(() => TimingDistribution.Create(new[] { double.NaN }));
    }
}
