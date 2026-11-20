using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class TransitionSuggestionsTests
{
    [Fact]
    public void TemporalCorrelationRejectsOppositeOrUnchangedTransitions()
    {
        byte[] low = { 0, 20, 30 }, high = { 50, 70, 90 };
        Assert.Equal(1, TransitionSuggestions.DeltaCorrelation(low, high, low, high)!.Value, 8);
        Assert.Equal(-1, TransitionSuggestions.DeltaCorrelation(high, low, low, high)!.Value, 8);
        Assert.Null(TransitionSuggestions.DeltaCorrelation(low, low, low, high));
    }
    [Fact]
    public void SuggestsRepeatedHumanTaughtTransitionWithoutChangingLabels()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-transitions-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var closed = Image(Color.DarkRed);
            using var open = Image(Color.ForestGreen);
            var recording = TeachingRecording.Start(root, "Test", "test");
            recording.AddImportedFrame(closed, 0);
            recording.AddImportedFrame(open, 200);
            recording.AddImportedFrame(closed, 400);
            recording.AddImportedFrame(open, 600);
            recording.AddImportedFrame(open, 800);
            recording.Finish();
            recording.Label(1, "open");
            recording.Label(2, "close");
            var proposals = TransitionSuggestions.Suggest(recording, CancellationToken.None);
            var result = Assert.Single(proposals);
            Assert.Equal(3, result.Index);
            Assert.Equal("open", result.State);
            Assert.Equal("", recording.Frames[3].State);
            Assert.Equal("", recording.Frames[4].State);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MarkedRegionIgnoresBackgroundChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-transition-roi-" + Guid.NewGuid().ToString("N"));
        try
        {
            var rectangle = new Rectangle(8, 8, 32, 24);
            using var first = RegionImage(Color.DarkBlue, Color.DarkRed, rectangle);
            using var second = RegionImage(Color.DarkBlue, Color.ForestGreen, rectangle);
            using var third = RegionImage(Color.Goldenrod, Color.DarkRed, rectangle);
            using var fourth = RegionImage(Color.Goldenrod, Color.ForestGreen, rectangle);
            var recording = TeachingRecording.Start(root, "Test", "test");
            recording.AddImportedFrame(first, 0);
            recording.AddImportedFrame(second, 200);
            recording.AddImportedFrame(third, 400);
            recording.AddImportedFrame(fourth, 600);
            recording.Finish();
            recording.Label(1, "open");
            recording.Label(2, "close");
            recording.SelectRegion(1, rectangle);
            recording.SelectRegion(2, rectangle);
            var proposal = Assert.Single(TransitionSuggestions.Suggest(recording, CancellationToken.None));
            Assert.Equal(3, proposal.Index);
            Assert.Equal("open", proposal.State);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void RejectsConflictingLabelsForTheSameTransition()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-transition-conflict-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var first = Image(Color.DarkRed);
            using var second = Image(Color.ForestGreen);
            var recording = TeachingRecording.Start(root, "Test", "test");
            recording.AddImportedFrame(first, 0);
            recording.AddImportedFrame(second, 200);
            recording.AddImportedFrame(first, 400);
            recording.AddImportedFrame(second, 600);
            recording.Finish();
            recording.Label(1, "open");
            recording.Label(3, "attack");
            Assert.Throws<InvalidDataException>(() => TransitionSuggestions.Suggest(recording, CancellationToken.None));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static Bitmap RegionImage(Color background, Color content, Rectangle rectangle)
    {
        var image = Image(background);
        using var graphics = Graphics.FromImage(image);
        using var brush = new SolidBrush(content);
        graphics.FillRectangle(brush, rectangle);
        return image;
    }

    private static Bitmap Image(Color color)
    {
        var image = new Bitmap(64, 48);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(color);
        return image;
    }
}
