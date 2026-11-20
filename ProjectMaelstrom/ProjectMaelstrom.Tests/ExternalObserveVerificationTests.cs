using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class ExternalObserveVerificationTests
{
    [Fact]
    public void ReviewedFramesCreateALearnablePack()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-reviewed-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = TeachingRecording.Start(root, "Test", "test");
            using var first = Image(Color.Red);
            using var second = Image(Color.Blue);
            recording.AddImportedFrame(first, 0);
            recording.AddImportedFrame(second, 200);
            recording.Finish();
            new GameplaySamplePlan("teaching", "Review states", null, null).Save(recording.DirectoryPath);
            var pack = ExternalObserveVerification.ExportReviewed(Path.Combine(recording.DirectoryPath, "recording.json"), root, 0, "menu", 1, "playing");
            Assert.Equal(2, VisualStateRecognizer.Learn(Path.Combine(pack, "pack.json")).StateCount);
            Assert.Throws<InvalidOperationException>(() => ExternalObserveVerification.ExportReviewed(Path.Combine(recording.DirectoryPath, "recording.json"), root, 0, "menu", 1, "playing"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void IndependentChecksAreNeverExportedAsTraining()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-independent-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = TeachingRecording.Start(root, "Test", "test");
            using var image = Image(Color.Red);
            recording.AddImportedFrame(image, 0);
            recording.AddImportedFrame(image, 200);
            recording.Finish();
            new GameplaySamplePlan("check", "Review states", "hash", "pack").Save(recording.DirectoryPath);
            Assert.Throws<InvalidOperationException>(() => ExternalObserveVerification.ExportReviewed(Path.Combine(recording.DirectoryPath, "recording.json"), root, 0, "menu", 1, "playing"));
            Assert.All(recording.Frames, frame => Assert.Empty(frame.State));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static Bitmap Image(Color color)
    {
        var image = new Bitmap(32, 24);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(color);
        return image;
    }
}
