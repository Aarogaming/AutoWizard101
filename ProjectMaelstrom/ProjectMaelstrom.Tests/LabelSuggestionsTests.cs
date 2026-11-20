using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class LabelSuggestionsTests
{
    [Fact]
    public async Task ProposalsDoNotModifyLabelsAndChecksCannotUseSuggestions()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-suggest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = TeachingRecording.Start(root, "test", "test");
            using var frame = new Bitmap(40, 30);
            using (var g = Graphics.FromImage(frame)) g.Clear(Color.Red);
            recording.AddFrame(frame);
            await Task.Delay(1050);
            using (var g = Graphics.FromImage(frame)) g.Clear(Color.Blue);
            recording.AddFrame(frame);
            await Task.Delay(1050);
            using (var g = Graphics.FromImage(frame)) g.Clear(Color.Red);
            recording.AddFrame(frame);
            recording.Finish();
            recording.Label(0, "red");
            recording.Label(1, "blue");
            var suggestions = LabelSuggestions.Suggest(recording);
            Assert.Equal("red", Assert.Single(suggestions).State);
            Assert.Equal("", recording.Frames[2].State);
            new GameplaySamplePlan("check", "Test", "pack", "Pack").Save(recording.DirectoryPath);
            Assert.Throws<InvalidOperationException>(() => LabelSuggestions.Suggest(recording));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
