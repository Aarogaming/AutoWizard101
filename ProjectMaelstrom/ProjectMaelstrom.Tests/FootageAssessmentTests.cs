using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class FootageAssessmentTests
{
    [Fact]
    public void SparseRepeatedVideoIsNeverTreatedAsVerifiedActions()
    {
        var frames = Enumerable.Range(0, 6).Select(i => new TeachingFrame($"frame-{i:D4}.png", i * 1000, 1280, 720, "identical")).ToArray();
        var quality = FootageAssessment.Assess(frames);
        Assert.False(quality.ActionTrainingEligible); Assert.Equal(1000, quality.MedianSampleGapMs);
        Assert.Contains(quality.Findings, text => text.Contains("repeated"));
        Assert.Contains(quality.Findings, text => text.Contains("fast actions"));
        Assert.Contains(quality.Findings, text => text.Contains("label relevant"));
    }
    [Fact]
    public void DenseLabeledSamplesStillNeedInputAndOutcomeEvidence()
    {
        var frames = Enumerable.Range(0, 30).Select(i => new TeachingFrame($"frame-{i:D4}.png", i * 1000.0 / 30, 480, 720, i.ToString(), "playing")).ToArray();
        var quality = FootageAssessment.Assess(frames);
        Assert.Equal(30, quality.LabeledFrames); Assert.Equal(30, quality.UniqueImages);
        Assert.False(quality.ActionTrainingEligible);
        Assert.DoesNotContain(quality.Findings, text => text.Contains("fast actions"));
    }
    [Fact]
    public void ImportHasBoundedThreadsResolutionAndFrameCount()
    {
        var args = VideoTeachingImport.Arguments("C:/game footage.mp4", "C:/samples", 30, 12);
        Assert.Equal(2, args.Count(a => a == "-threads"));
        Assert.Contains("-filter_threads", args); Assert.Contains("600", args);
        Assert.Contains(args, a => a.Contains("min(1280,iw)") && a.Contains("min(720,ih)"));
        Assert.Equal("20", args[Array.IndexOf(args, "-t") + 1]);
        Assert.Equal("C:/game footage.mp4", args[Array.IndexOf(args, "-i") + 1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => VideoTeachingImport.Arguments("x", "y", 61, 0));
    }
    [Fact]
    public void InvalidTimelineAndChangingDimensionsAreFlagged()
    {
        TeachingFrame[] frames = [new("frame-0000.png", 100, 480, 720, "a"), new("frame-0001.png", 100, 100, 100, "b")];
        var quality = FootageAssessment.Assess(frames);
        Assert.False(quality.ConsistentDimensions);
        Assert.Contains(quality.Findings, text => text.Contains("non-increasing"));
        Assert.Contains(quality.Findings, text => text.Contains("Small images"));
    }
}
