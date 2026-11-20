using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class VisionScreeningTests
{
    [Fact]
    public async Task RequestLimitStopsBeforeFurtherModelWork()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-prescreen-limit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = MakeRecording(root);
            recording.SelectRegion(2, new Rectangle(2, 2, 12, 12));
            var calls = 0;
            var result = await VisionScreening.RunAsync(recording, (_, _) => { calls++; return Task.FromResult("screen"); }, _ => { }, null, CancellationToken.None, maxModelRequests: 1, cooldownMilliseconds: 0);
            Assert.Equal(1, calls);
            Assert.Equal(2, result.Completed);
            Assert.Equal(4, result.Total);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task ExactRepeatsReuseInferenceButDifferentCropsDoNot()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-prescreen-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = MakeRecording(root);
            recording.SelectRegion(2, new Rectangle(2, 2, 12, 12));
            recording.Label(3, "reviewed");
            var calls = new List<int>();
            var suggestions = new List<LabelSuggestion>();
            var result = await VisionScreening.RunAsync(recording, (index, _) => { calls.Add(index); return Task.FromResult("screen-" + index); }, suggestions.Add, null, CancellationToken.None);
            Assert.Equal(new[] { 0, 2 }, calls);
            Assert.Equal(3, result.Completed);
            Assert.Equal(2, result.ModelRequests);
            Assert.Equal(1, result.Reused);
            Assert.Equal("screen-0", suggestions[1].State);
            Assert.Equal("Local AI · same image", suggestions[1].Source);
            Assert.All(recording.Frames.Take(3), frame => Assert.Empty(frame.State));
            Assert.Equal("reviewed", recording.Frames[3].State);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task IndependentChecksAndChangedDuplicateFilesAreRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-prescreen-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = MakeRecording(root);
            var second = Path.Combine(recording.DirectoryPath, recording.Frames[1].File);
            File.WriteAllBytes(second, new byte[] { 1, 2, 3 });
            await Assert.ThrowsAsync<InvalidDataException>(() => VisionScreening.RunAsync(recording, (_, _) => Task.FromResult("screen"), _ => { }, null, CancellationToken.None));
            new GameplaySamplePlan("check", "Independent", "hash", "pack").Save(recording.DirectoryPath);
            await Assert.ThrowsAsync<InvalidOperationException>(() => VisionScreening.RunAsync(recording, (_, _) => Task.FromResult("screen"), _ => { }, null, CancellationToken.None));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CancellationKeepsCompletedSuggestionsOutsideTheRecording()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-prescreen-cancel-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = MakeRecording(root);
            using var cancel = new CancellationTokenSource();
            var suggestions = new List<LabelSuggestion>();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => VisionScreening.RunAsync(recording, (_, _) => Task.FromResult("screen"), suggestion => { suggestions.Add(suggestion); cancel.Cancel(); }, null, cancel.Token));
            Assert.Single(suggestions);
            Assert.All(recording.Frames, frame => Assert.Empty(frame.State));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task UnusableResponsesAreSkippedAndDoNotAbortLaterFrames()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-prescreen-unusable-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = MakeRecording(root);
            recording.SelectRegion(2, new Rectangle(2, 2, 12, 12));
            var suggestions = new List<LabelSuggestion>();
            var result = await VisionScreening.RunAsync(recording,
                (index, _) => index == 0 ? Task.FromException<string>(new UnusableVisionSuggestionException("bad")) : Task.FromResult("playing"),
                suggestions.Add, null, CancellationToken.None);
            Assert.Equal(4, result.Completed);
            Assert.Equal(2, result.ModelRequests);
            Assert.Equal(2, result.Reused);
            Assert.Equal(3, result.Rejected);
            Assert.Equal("playing", Assert.Single(suggestions).State);
            Assert.All(recording.Frames, frame => Assert.Empty(frame.State));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static TeachingRecording MakeRecording(string root)
    {
        var recording = TeachingRecording.Start(root, "Test", "test");
        using var image = new Bitmap(32, 24);
        using (var graphics = Graphics.FromImage(image)) graphics.Clear(Color.CornflowerBlue);
        for (var i = 0; i < 4; i++) recording.AddImportedFrame(image, i * 200);
        recording.Finish();
        return recording;
    }
}
