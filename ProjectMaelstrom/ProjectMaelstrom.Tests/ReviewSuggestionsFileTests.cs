using System.Drawing;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class ReviewSuggestionsFileTests
{
    [Fact]
    public void SuggestionsRequireMatchingFramesAndNeverSetLabels()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-suggestions-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = MakeRecording(root);
            var path = Path.Combine(recording.DirectoryPath, ReviewSuggestionsFile.FileName);
            var saved = new SavedReviewSuggestions(1, "Assistant review", new() { new(0, recording.Frames[0].Sha256, "menu"), new(1, recording.Frames[1].Sha256, "playing") });
            File.WriteAllText(path, JsonSerializer.Serialize(saved));
            var suggestions = ReviewSuggestionsFile.Load(recording);
            Assert.Equal(2, suggestions.Count);
            Assert.Equal("Assistant review", suggestions[0].Source);
            Assert.All(recording.Frames, frame => Assert.Empty(frame.State));
            recording.Label(0, "reviewed-menu");
            Assert.Single(ReviewSuggestionsFile.Load(recording));
            File.WriteAllText(path, JsonSerializer.Serialize(saved with { Suggestions = new() { new(1, "wrong-hash", "playing") } }));
            Assert.Throws<InvalidDataException>(() => ReviewSuggestionsFile.Load(recording));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void IndependentCheckRecordingsNeverLoadSuggestions()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-suggestions-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = MakeRecording(root);
            new GameplaySamplePlan("check", "Independent review", "pack-hash", "pack").Save(recording.DirectoryPath);
            File.WriteAllText(Path.Combine(recording.DirectoryPath, ReviewSuggestionsFile.FileName), "invalid data");
            Assert.Empty(ReviewSuggestionsFile.Load(recording));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static TeachingRecording MakeRecording(string root)
    {
        var recording = TeachingRecording.Start(root, "Test", "test");
        using var image = new Bitmap(32, 24);
        recording.AddImportedFrame(image, 0);
        recording.AddImportedFrame(image, 200);
        recording.Finish();
        return recording;
    }
}
