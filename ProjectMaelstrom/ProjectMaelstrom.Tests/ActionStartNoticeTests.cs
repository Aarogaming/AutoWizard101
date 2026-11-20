using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class ActionStartNoticeTests
{
    [Theory]
    [InlineData(0, 0, 1280, 720)]
    [InlineData(2560, 0, 2560, 1440)]
    [InlineData(-1920, -200, 1920, 1080)]
    public void BannerRemainsInsideTargetClientAcrossMonitors(int x, int y, int width, int height)
    {
        var client = new System.Drawing.Rectangle(x, y, width, height);
        var banner = ActionStartNotice.BannerBounds(client);
        Assert.True(client.Contains(banner));
        Assert.Equal(width - 32, banner.Width);
        Assert.Equal(x + 16, banner.Left);
    }
    [Theory]
    [InlineData(0, 5)]
    [InlineData(0.9, 5)]
    [InlineData(4.99, 1)]
    [InlineData(5, 0)]
    [InlineData(6, 0)]
    public void CountdownCannotFinishBeforeFiveSeconds(double elapsed, int remaining) =>
        Assert.Equal(remaining, ActionStartNotice.Remaining(elapsed));

    [Fact]
    public void OldSettingsKeepNoticeSoundAndSoundCanBeDisabled()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "settings.json"), "{\"Version\":1}");
            Assert.True(AppPreferences.Load(root).ActionCountdownSound);
            (AppPreferences.Load(root) with { ActionCountdownSound = false }).Save(root);
            Assert.False(AppPreferences.Load(root).ActionCountdownSound);
        }
        finally { Directory.Delete(root, true); }
    }
}
