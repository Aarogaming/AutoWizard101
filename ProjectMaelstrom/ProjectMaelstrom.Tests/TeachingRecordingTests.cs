using System.Drawing;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class TeachingRecordingTests
{
    [Fact]
    public void VideoSamplingStaysWithinFrameAndDurationLimits()
    {
        Assert.Equal(180, VideoTeachingImport.MaximumSeconds(1));
        Assert.Equal(120, VideoTeachingImport.MaximumSeconds(5));
        Assert.Equal(60, VideoTeachingImport.MaximumSeconds(10));
        Assert.Equal(10, VideoTeachingImport.MaximumSeconds(60));
        Assert.Throws<ArgumentOutOfRangeException>(() => VideoTeachingImport.MaximumSeconds(61));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(86401)]
    public async Task InvalidVideoStartIsRejectedBeforeCreatingARecording(int startSeconds)
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-video-start-" + Guid.NewGuid().ToString("N"));
        var video = Path.GetTempFileName();
        try
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                VideoTeachingImport.RunAsync(root, "Video", video, "unused", CancellationToken.None, 5, startSeconds));
            Assert.False(Directory.Exists(root));
        }
        finally { File.Delete(video); }
    }

    [Fact]
    public void ImportedFramesKeepTheirApproximateVideoTimes()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-import-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = TeachingRecording.Start(root, "Video", "video");
            using var bitmap = new Bitmap(20, 20);
            Assert.True(recording.AddImportedFrame(bitmap, 0));
            Assert.True(recording.AddImportedFrame(bitmap, 500));
            Assert.Throws<InvalidDataException>(() => recording.AddImportedFrame(bitmap, 499));
            recording.Finish();
            var reopened = TeachingRecording.Open(Path.Combine(recording.DirectoryPath, "recording.json"));
            Assert.Equal(new double[] { 0, 500 }, reopened.Frames.Select(f => f.TimeMs));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void RecordingLabelsAndPackRoundTripPreserveEvidence()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-teach-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = TeachingRecording.Start(root, "Home", "Wizard");
            using var bitmap = new Bitmap(40, 30);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.CornflowerBlue);
            Assert.True(recording.AddFrame(bitmap));
            Assert.Throws<InvalidOperationException>(() => recording.Label(0, "ready"));
            recording.Finish();
            Assert.False(recording.AddFrame(bitmap));
            Assert.Throws<InvalidOperationException>(() => recording.ExportPack(root, "test", "game", "Test"));
            recording.Label(0, "ready");
            recording.SelectRegion(0, new Rectangle(2, 3, 10, 12));
            Assert.Throws<InvalidDataException>(() => recording.SelectRegion(0, new Rectangle(39, 0, 10, 10)));
            var reopened = TeachingRecording.Open(Path.Combine(recording.DirectoryPath, "recording.json"));
            Assert.Equal("ready", reopened.Frames[0].State);
            Assert.Equal(new[] { 2, 3, 10, 12 }, reopened.Frames[0].Region);
            using var frame = reopened.ReadFrame(0);
            Assert.Equal(Color.CornflowerBlue.ToArgb(), frame.GetPixel(10, 10).ToArgb());
            var pack = reopened.ExportPack(root, "test", "game", "Test");
            var original = File.ReadAllBytes(Path.Combine(pack, "pack.json"));
            var next = reopened.ExportPack(root, "test", "game", "Test again");
            Assert.NotEqual(pack, next);
            Assert.Equal(original, File.ReadAllBytes(Path.Combine(pack, "pack.json")));
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(pack, "examples", "events.jsonl")));
            Assert.Equal("ready", document.RootElement.GetProperty("payload").GetProperty("state").GetString());
            Assert.Equal("human", document.RootElement.GetProperty("payload").GetProperty("label_source").GetString());
            Assert.Equal(10, document.RootElement.GetProperty("payload").GetProperty("region")[2].GetInt32());
            Assert.Throws<InvalidDataException>(() => reopened.ExportPack(root, "../escape", "game", "Bad"));
            File.WriteAllText(Path.Combine(recording.DirectoryPath, recording.Frames[0].File), "changed");
            Assert.Throws<InvalidDataException>(() => reopened.ReadFrame(0));
            Assert.Throws<InvalidDataException>(() => reopened.ExportPack(root, "test", "game", "Bad"));
            Assert.Equal(2, Directory.GetDirectories(Path.Combine(root, "game-packs", "test")).Length);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
