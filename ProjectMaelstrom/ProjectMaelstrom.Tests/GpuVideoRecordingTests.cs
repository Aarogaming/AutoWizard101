using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class GpuVideoRecordingTests
{
    [Fact]
    public async Task RefusesAnExistingRecordingDirectoryWithoutWritingToIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maelstrom-existing-video-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var sentinel = Path.Combine(directory, "keep.txt");
        File.WriteAllText(sentinel, "keep");
        try
        {
            var target = new GameWindow(0, 0, 0, "test", "test");
            await Assert.ThrowsAsync<IOException>(() => GpuVideoRecording.RunAsync(target, directory, TimeSpan.FromSeconds(1), CancellationToken.None, () => { }));
            Assert.Equal("keep", File.ReadAllText(sentinel));
            Assert.False(File.Exists(Path.Combine(directory, "timing.json")));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
