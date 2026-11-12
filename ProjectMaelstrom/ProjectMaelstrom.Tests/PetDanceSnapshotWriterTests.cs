using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetDanceSnapshotWriterTests
{
    [Fact]
    public async Task QueuedSnapshotOwnsItsImageAndStopsAfterCompletion()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-audio-snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var writer = new PetDanceSnapshotWriter();
            using (var original = new Bitmap(4, 4))
            {
                original.SetPixel(0, 0, Color.Red);
                Assert.True(writer.TrySave(original, Path.Combine(root, "frame.png")));
            }
            await writer.CompleteAsync();
            using var saved = new Bitmap(Path.Combine(root, "frame.png"));
            Assert.Equal(Color.Red.ToArgb(), saved.GetPixel(0, 0).ToArgb());
            Assert.Equal(1, writer.Saved);
            Assert.Null(writer.Error);
            Assert.False(writer.TrySave(saved, Path.Combine(root, "late.png")));
            Assert.False(File.Exists(Path.Combine(root, "late.png")));
        }
        finally { Directory.Delete(root, true); }
    }
}
