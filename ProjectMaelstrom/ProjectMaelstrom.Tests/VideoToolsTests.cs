using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class VideoToolsTests
{
    [Fact]
    public void RequiresBothToolsAndPrefersThePrivateCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-video-tools-" + Guid.NewGuid().ToString("N"));
        var packaged = Path.Combine(root, "app", "runtime", "ffmpeg");
        var installed = Path.Combine(root, "installed");
        try
        {
            Directory.CreateDirectory(packaged);
            Directory.CreateDirectory(installed);
            File.WriteAllText(Path.Combine(packaged, "ffmpeg.exe"), "");
            File.WriteAllText(Path.Combine(installed, "ffmpeg.exe"), "");
            File.WriteAllText(Path.Combine(installed, "ffprobe.exe"), "");
            Assert.Equal(Path.Combine(installed, "ffmpeg.exe"), VideoTools.FindFfmpeg(Path.Combine(root, "app"), installed));
            File.WriteAllText(Path.Combine(packaged, "ffprobe.exe"), "");
            Assert.Equal(Path.Combine(packaged, "ffmpeg.exe"), VideoTools.FindFfmpeg(Path.Combine(root, "app"), installed));
            Assert.Null(VideoTools.FindFfmpeg(Path.Combine(root, "absent"), null));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
