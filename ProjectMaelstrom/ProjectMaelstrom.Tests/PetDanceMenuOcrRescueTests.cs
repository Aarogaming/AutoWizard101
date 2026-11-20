using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceMenuOcrRescueTests
{
    private static readonly Size RecordedGeometry = new(1282, 760);

    // Exact heading bounds from report-current-ocr.json: the actual selected-away
    // frame had these words but no PLAY. Tests exercise routing, not OCR accuracy.
    private static PetDanceMenuText MissingPlay() => new(RecordedGeometry,
    [
        new("DANCE", new(591, 211, 50, 15)), new("GAME", new(646, 212, 45, 14)),
        new("OBJECTIVE", new(437, 343, 66, 11)), new("SELECT", new(604, 473, 36, 12)),
        new("LEVEL", new(643, 473, 32, 11))
    ]);

    [Fact]
    public void SavedMissingPlayObservationGetsOneBroadLowerMenuRegion()
    {
        var region = PetDanceMenuOcrRescue.TryRegion(MissingPlay());
        Assert.Equal(new Rectangle(320, 456, 641, 190), region);
        Assert.True(region!.Value.Contains(new Rectangle(778, 574, 42, 12)));
        Assert.True(region.Value.Contains(new Rectangle(452, 485, 59, 55)));
        Assert.Equal(3, PetDanceMenuOcrRescue.RequestedScale);
    }

    [Theory]
    [InlineData("PLAY")]
    [InlineData("PLAY!")]
    [InlineData("play")]
    public void AlreadyReadPlayAvoidsAdditionalWork(string play)
    {
        var text = MissingPlay();
        Assert.Null(PetDanceMenuOcrRescue.TryRegion(text with
        { Words = [.. text.Words, new(play, new(778, 574, 42, 12))] }));
    }

    [Fact]
    public void MissingWrongOrDisplacedHeadersDoNotTriggerRescue()
    {
        var text = MissingPlay();
        foreach (var term in new[] { "DANCE", "GAME", "SELECT", "LEVEL" })
            Assert.Null(PetDanceMenuOcrRescue.TryRegion(text with
            { Words = text.Words.Where(word => word.Text != term).ToArray() }));
        Assert.Null(PetDanceMenuOcrRescue.TryRegion(new(RecordedGeometry, [])));
        Assert.Null(PetDanceMenuOcrRescue.TryRegion(text with
        {
            Words = text.Words.Select(word =>
            word.Text == "GAME" ? word with { Bounds = new(1000, 212, 45, 14) } : word).ToArray()
        }));
        Assert.Null(PetDanceMenuOcrRescue.TryRegion(text with
        {
            Words = text.Words.Select(word =>
            word.Text == "LEVEL" ? word with { Bounds = new(643, 600, 32, 11) } : word).ToArray()
        }));
    }

    [Fact]
    public void NativeIdleOverlayKeepsPriorityOverRetainedMenuText()
    {
        var text = MissingPlay();
        Assert.Null(PetDanceMenuOcrRescue.TryRegion(text with
        {
            Words = [.. text.Words,
            new("YOU", new(530, 70, 30, 15)), new("ARE", new(565, 70, 30, 15)),
            new("IDLE", new(600, 70, 35, 15))]
        }));
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1282, 759)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(7680, 4320)]
    public void GridRegionAndRequestedMagnificationRespectExistingRasterBudget(int width, int height)
    {
        var scaleX = (float)width / RecordedGeometry.Width; var scaleY = (float)height / RecordedGeometry.Height;
        var text = MissingPlay() with
        {
            ImageSize = new(width, height),
            Words = MissingPlay().Words.Select(word =>
            word with
            {
                Bounds = new(word.Bounds.X * scaleX, word.Bounds.Y * scaleY,
                word.Bounds.Width * scaleX, word.Bounds.Height * scaleY)
            }).ToArray()
        };
        var region = PetDanceMenuOcrRescue.TryRegion(text);
        Assert.NotNull(region);
        Assert.True(new Rectangle(Point.Empty, text.ImageSize).Contains(region.Value));
        var raster = PetDanceOcrRaster.Create(region.Value.Size, PetDanceMenuOcrRescue.RequestedScale, 2600);
        Assert.True((long)raster.Size.Width * raster.Size.Height <= PetDanceOcrRaster.MaximumPixels);
        Assert.InRange(raster.Size.Width, 1, 2600);
        Assert.InRange(raster.Size.Height, 1, 2600);
        Assert.Equal(region.Value.Width * .37, region.Value.Width * .37 * raster.ScaleX / raster.ScaleX, 6);
    }

    [Fact]
    public void InvalidOrUnboundedWordInputCannotRequestExtraRasterWork()
    {
        var text = MissingPlay();
        Assert.Null(PetDanceMenuOcrRescue.TryRegion(text with { ImageSize = Size.Empty }));
        Assert.Null(PetDanceMenuOcrRescue.TryRegion(text with { Words = Enumerable.Repeat(text.Words[0], 513).ToArray() }));
        Assert.Null(PetDanceMenuOcrRescue.TryRegion(text with
        {
            Words = [.. text.Words,
            new("bad", new(float.NaN, 1, 20, 10))]
        }));
        Assert.Null(PetDanceMenuOcrRescue.TryRegion(text with
        {
            Words = [.. text.Words,
            new(new string('a', 129), new(1, 1, 20, 10))]
        }));
    }
}
