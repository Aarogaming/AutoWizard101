using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101RunningNoticeTests
{
    [Theory]
    [InlineData(0, 0, 1280, 720, 96)]
    [InlineData(2560, 0, 2560, 1440, 144)]
    [InlineData(-1920, -200, 1920, 1080, 192)]
    [InlineData(-3840, 300, 3840, 2160, 288)]
    [InlineData(10, 20, 50, 10, 96)]
    public void CaptionStaysAtBottomCenterOfItsClient(int x, int y, int width, int height, uint dpi)
    {
        var client = new Rectangle(x, y, width, height);
        var bounds = W101RunningNotice.CaptionBounds(client, dpi);
        Assert.True(client.Contains(bounds));
        Assert.InRange(Math.Abs((bounds.Left + bounds.Right) - (client.Left + client.Right)), 0, 1);
        Assert.InRange(client.Bottom - bounds.Bottom, 0, (int)Math.Ceiling(2 * dpi / 96d));
    }

    [Fact]
    public void MissingClientHasNoCaption() => Assert.Equal(Rectangle.Empty, W101RunningNotice.CaptionBounds(Rectangle.Empty, 96));

    [Theory]
    [InlineData(true, false, 10, 10, true)]
    [InlineData(false, false, 10, 10, false)]
    [InlineData(true, true, 10, 10, false)]
    [InlineData(true, false, 10, 11, false)]
    [InlineData(true, false, 0, 0, false)]
    public void CaptionNeverAppearsOverAnotherFocusedWindow(bool current, bool minimized, int target, int foreground, bool expected) =>
        Assert.Equal(expected, W101RunningNotice.ShouldShow(current, minimized, target, foreground));

    [Theory]
    [InlineData("Running", "Maelstrom Running")]
    [InlineData("Maelstrom Running", "Maelstrom Running")]
    [InlineData("Paused", "Maelstrom Paused")]
    [InlineData("Stopping", "Maelstrom Stopping")]
    [InlineData("unknown", "Maelstrom Paused")]
    [InlineData(null, "Maelstrom Paused")]
    public void CaptionUsesBoundedRecognizableStatus(string? value, string expected) => Assert.Equal(expected, W101RunningNotice.Caption(value));

    [Theory]
    [InlineData("Running", (int)W101RunningNotice.ControlButton.Play)]
    [InlineData("Paused", (int)W101RunningNotice.ControlButton.Pause)]
    [InlineData("Stopped", (int)W101RunningNotice.ControlButton.Stop)]
    [InlineData("Stopping", (int)W101RunningNotice.ControlButton.Stop)]
    public void ObservedStateChoosesActiveSymbol(string status, int expected) =>
        Assert.Equal((W101RunningNotice.ControlButton)expected, W101RunningNotice.ActiveControl(status));

    [Fact]
    public void OnlyEnabledCircleInteriorsAreInteractive()
    {
        var size = new Size(102, 28);
        foreach (var button in Enum.GetValues<W101RunningNotice.ControlButton>())
        {
            var bounds = W101RunningNotice.ControlBounds(size, button);
            var center = Point.Round(new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));
            Assert.Equal(button, W101RunningNotice.HitControl(size, center, true, true, true));
            Assert.Null(W101RunningNotice.HitControl(size, center, false, false, false));
            Assert.Null(W101RunningNotice.HitControl(size, Point.Round(bounds.Location), true, true, true));
        }
        Assert.Null(W101RunningNotice.HitControl(size, new(-1, 14), true, true, true));
        Assert.Null(W101RunningNotice.HitControl(size, new(34, 14), true, true, true));
        Assert.Null(W101RunningNotice.HitControl(size, new(102, 14), true, true, true));
    }

    [Theory]
    [InlineData("Running", (int)W101RunningNotice.ControlButton.Play)]
    [InlineData("Paused", (int)W101RunningNotice.ControlButton.Pause)]
    [InlineData("Stopped", (int)W101RunningNotice.ControlButton.Stop)]
    public void OnlyTheActiveGlyphIsColoredWithoutAGoldRing(string status, int active)
    {
        using var bitmap = new Bitmap(306, 84);
        using (var graphics = Graphics.FromImage(bitmap)) W101RunningNotice.DrawControls(graphics, bitmap.Size, status, true, true, true);
        var colors = new[] { Color.LimeGreen, Color.Yellow, Color.Red };
        foreach (var button in Enum.GetValues<W101RunningNotice.ControlButton>())
        {
            var region = Rectangle.Ceiling(W101RunningNotice.ControlBounds(bitmap.Size, button));
            Assert.Equal((int)button == active, Count(bitmap, colors[(int)button], region) > 100);
            Assert.Equal(0, Count(bitmap, Color.Gold, region));
        }
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++) Assert.Equal(255, bitmap.GetPixel(x, y).A);
        var preview = Environment.GetEnvironmentVariable("MAELSTROM_OVERLAY_PREVIEW_DIR");
        if (!string.IsNullOrWhiteSpace(preview))
        {
            Directory.CreateDirectory(preview);
            bitmap.Save(Path.Combine(preview, status.ToLowerInvariant() + ".png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    [Fact]
    public void ObservedStateRemainsVisibleEvenWhenItsButtonIsNotClickable()
    {
        using var bitmap = new Bitmap(306, 84);
        using (var graphics = Graphics.FromImage(bitmap)) W101RunningNotice.DrawControls(graphics, bitmap.Size, "Running", false, false, true);
        var all = new Rectangle(Point.Empty, bitmap.Size);
        Assert.True(Count(bitmap, Color.LimeGreen, all) > 100);
        Assert.Equal(0, Count(bitmap, Color.Yellow, all));
        Assert.Equal(0, Count(bitmap, Color.Red, all));
        Assert.Equal(0, Count(bitmap, Color.Gold, all));
    }

    [Theory]
    [InlineData(102, 28)]
    [InlineData(306, 84)]
    public void WindowShapeExcludesBackgroundAndGaps(int width, int height)
    {
        var size = new Size(width, height);
        using var region = W101RunningNotice.ControlRegion(size);
        Assert.False(region.IsVisible(0, 0));
        Assert.False(region.IsVisible(width / 3f, height / 2f));
        Assert.False(region.IsVisible(width * 2 / 3f, height / 2f));
        foreach (var button in Enum.GetValues<W101RunningNotice.ControlButton>())
        {
            var bounds = W101RunningNotice.ControlBounds(size, button);
            Assert.True(region.IsVisible(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));
            Assert.False(region.IsVisible(bounds.Left, bounds.Top));
        }
    }

    private static int Count(Bitmap bitmap, Color color, Rectangle region)
    {
        var count = 0;
        for (var y = Math.Max(0, region.Top); y < Math.Min(bitmap.Height, region.Bottom); y++)
            for (var x = Math.Max(0, region.Left); x < Math.Min(bitmap.Width, region.Right); x++)
                if (bitmap.GetPixel(x, y).ToArgb() == color.ToArgb()) count++;
        return count;
    }
}
