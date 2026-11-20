using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceOcrRasterTests
{
    [Theory]
    [InlineData(1282, 760, 1)]
    [InlineData(641, 152, 3)]
    [InlineData(427, 84, 3)]
    public void ExistingQualifiedMenuSamplingIsUnchanged(int width, int height, int scale)
    {
        var raster = PetDanceOcrRaster.Create(new(width, height), scale, 2600);
        Assert.Equal(new Size(width * scale, height * scale), raster.Size);
        Assert.Equal((double)scale, raster.ScaleX);
        Assert.Equal((double)scale, raster.ScaleY);
    }

    [Theory]
    [InlineData(1920, 1080, 1)]
    [InlineData(2560, 1440, 1)]
    [InlineData(3840, 2160, 1)]
    [InlineData(1920, 432, 3)]
    [InlineData(7680, 4320, 1)]
    public void NativeSizesStayWithinOcrAndWorkBudgetsAndKeepAnInverse(int width, int height, int scale)
    {
        var raster = PetDanceOcrRaster.Create(new(width, height), scale, 2600);
        Assert.InRange(raster.Size.Width, 1, 2600);
        Assert.InRange(raster.Size.Height, 1, 2600);
        Assert.True((long)raster.Size.Width * raster.Size.Height <= PetDanceOcrRaster.MaximumPixels);
        Assert.Equal(width * .37, width * .37 * raster.ScaleX / raster.ScaleX, 6);
        Assert.Equal(height * .81, height * .81 * raster.ScaleY / raster.ScaleY, 6);
    }

    [Fact]
    public void InvalidGeometryCannotCreateAnImage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PetDanceOcrRaster.Create(Size.Empty, 1, 2600));
        Assert.Throws<ArgumentOutOfRangeException>(() => PetDanceOcrRaster.Create(new(100, 100), 5, 2600));
        Assert.Throws<ArgumentOutOfRangeException>(() => PetDanceOcrRaster.Create(new(100, 100), 1, 0));
    }
}
