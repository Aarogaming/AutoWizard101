using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class MotionSuggestionsTests
{
    [Fact]
    public void TracksASelectedObjectButAbstainsForStillOrAmbiguousFrames()
    {
        using var pattern = new Bitmap(24, 24);
        var random = new Random(42);
        for (var y = 0; y < pattern.Height; y++)
            for (var x = 0; x < pattern.Width; x++) pattern.SetPixel(x, y, Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)));
        using var first = Frame(pattern, new Point(15, 30));
        using var moved = Frame(pattern, new Point(54, 30));
        var region = new Rectangle(15, 30, 24, 24);
        var prediction = MotionSuggestions.Compare(first, moved, region);
        Assert.Equal("screen-motion-right", prediction.Label);
        Assert.Equal(39, prediction.DeltaX);
        using var diagonal = Frame(pattern, new Point(50, 62));
        Assert.Equal("screen-motion-down-right", MotionSuggestions.Compare(first, diagonal, region).Label);
        Assert.Null(MotionSuggestions.Compare(first, first, region).Label);
        using var ambiguous = Frame(pattern, new Point(54, 30), new Point(85, 30));
        Assert.Null(MotionSuggestions.Compare(first, ambiguous, region).Label);
        using var missing = new Bitmap(first.Width, first.Height);
        Assert.Null(MotionSuggestions.Compare(first, missing, region).Label);
    }

    [Fact]
    public void TracksForwardWithoutOverwritingHumanLabels()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-motion-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var pattern = new Bitmap(24, 24);
            var random = new Random(7);
            for (var y = 0; y < 24; y++) for (var x = 0; x < 24; x++)
                pattern.SetPixel(x, y, Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)));
            using var first = Frame(pattern, new Point(15, 30));
            using var second = Frame(pattern, new Point(45, 30));
            using var third = Frame(pattern, new Point(75, 30));
            var recording = TeachingRecording.Start(root, "Test", "test");
            recording.AddImportedFrame(first, 0);
            recording.AddImportedFrame(second, 200);
            recording.AddImportedFrame(third, 400);
            recording.Finish();
            recording.Label(1, "reviewed");
            var result = MotionSuggestions.Track(recording, 0, new Rectangle(15, 30, 24, 24), CancellationToken.None);
            Assert.Equal(2, result.Compared);
            var suggestion = Assert.Single(result.Suggestions);
            Assert.Equal(2, suggestion.Index);
            Assert.Equal("screen-motion-right", suggestion.State);
            Assert.Equal("reviewed", recording.Frames[1].State);
            Assert.Equal("", recording.Frames[2].State);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ReverseCheckCanResolveAMildDistractorButRejectsAmbiguousOrigins()
    {
        using var pattern = new Bitmap(24, 24);
        var random = new Random(7);
        for (var y = 0; y < 24; y++) for (var x = 0; x < 24; x++)
            pattern.SetPixel(x, y, Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)));
        using var distractor = new Bitmap(pattern);
        for (var y = 0; y < 3; y++) for (var x = 0; x < 8; x++) distractor.SetPixel(x, y, Color.Black);
        using var first = Frame(pattern, new Point(15, 30));
        using var later = Frame(pattern, new Point(54, 30));
        using (var graphics = Graphics.FromImage(later)) graphics.DrawImageUnscaled(distractor, new Point(95, 30));
        var result = MotionSuggestions.Compare(first, later, new Rectangle(15, 30, 24, 24));
        Assert.Equal("screen-motion-right", result.Label);
        Assert.True(result.Reliable);
        Assert.True(result.ReverseChecked);
        Assert.StartsWith("A reverse match", result.Reason);
        using var ambiguousOrigin = Frame(pattern, new Point(15, 30), new Point(85, 30));
        var rejected = MotionSuggestions.Compare(ambiguousOrigin, later, new Rectangle(15, 30, 24, 24));
        Assert.Null(rejected.Label);
        Assert.False(rejected.Reliable);
    }

    private static Bitmap Frame(Bitmap pattern, params Point[] positions)
    {
        var image = new Bitmap(140, 90);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.FromArgb(30, 40, 50));
        foreach (var position in positions) graphics.DrawImageUnscaled(pattern, position);
        return image;
    }
}
