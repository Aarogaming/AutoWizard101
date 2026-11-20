using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetDanceUiLayoutTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7d / 6)]
    [InlineData(4d / 3)]
    [InlineData(1.5)]
    [InlineData(5d / 3)]
    public void SharedLayoutRecognizesPhasesAcrossConfiguredScalesAndPositions(double scale)
    {
        using var fixture = new Fixture();
        var layout = fixture.Layout();
        foreach (var phase in new[] { DancePhase.Watch, DancePhase.Go, DancePhase.Resolving })
            foreach (var shift in new[] { new Point(-12, -4), new Point(15, 5) })
            {
                using var source = fixture.Scene(phase, scale, shift);
                Assert.True(layout.TryExtract(source, out var panel, out var seen));
                using (panel)
                {
                    Assert.NotNull(seen);
                    Assert.True(seen.Qualified);
                    Assert.Equal(phase, seen.Phase);
                    Assert.Equal(scale, seen.Scale);
                    Assert.Equal(new Size(256, 140), panel!.Size);
                    Assert.True(seen.PhaseScore >= .72);
                    Assert.True(seen.PhaseMargin >= .15);
                    Assert.True(seen.RingSupportingSpokes >= 8);
                }
            }
    }

    [Fact]
    public void GoTextOrMaroonPlateWithoutButtonDoesNotQualify()
    {
        using var fixture = new Fixture();
        var layout = fixture.Layout();
        foreach (var phase in new[] { DancePhase.Go, DancePhase.Unknown })
        {
            using var source = fixture.Scene(phase, 5d / 3, Point.Empty, button: false);
            Assert.False(layout.TryExtract(source, out var panel, out var seen));
            Assert.Null(panel);
            Assert.Null(seen);
        }
    }

    [Fact]
    public void RingAndPlateWithoutKnownTextDoNotInventAPhase()
    {
        using var fixture = new Fixture();
        using var source = fixture.Scene(DancePhase.Unknown, 1.5, Point.Empty);
        Assert.True(fixture.Layout().TryExtract(source, out var panel, out var seen));
        using (panel)
        {
            Assert.NotNull(seen);
            Assert.True(seen.Qualified);
            Assert.Equal(DancePhase.Unknown, seen.Phase);
            Assert.Equal("layout-found-phase-unknown", seen.Status);
        }
    }

    [Fact]
    public void MissingLayoutDoesNotReusePreviousGo()
    {
        using var fixture = new Fixture();
        var layout = fixture.Layout();
        using var source = fixture.Scene(DancePhase.Go, 1, Point.Empty);
        Assert.True(layout.TryExtract(source, out var panel, out _));
        panel!.Dispose();
        using var blank = new Bitmap(1282, 759);
        Assert.False(layout.TryExtract(blank, out panel, out var seen));
        Assert.Null(panel);
        Assert.Null(seen);
    }

    [Fact]
    public void TwoCompleteLayoutsAreAmbiguousInsteadOfPickingTheFirst()
    {
        using var fixture = new Fixture();
        using var source = fixture.Scene(DancePhase.Go, 1, Point.Empty);
        // Add a second complete plate and circle above the first, inside the same region.
        using (var copy = (Bitmap)source.Clone())
        using (var graphics = Graphics.FromImage(source))
        {
            using var plate = copy.Clone(new Rectangle(528, 659, 226, 24), PixelFormat.Format24bppRgb);
            using var circle = copy.Clone(new Rectangle(615, 685, 52, 52), PixelFormat.Format24bppRgb);
            graphics.DrawImageUnscaled(plate, 528, 579);
            graphics.DrawImageUnscaled(circle, 615, 605);
        }
        Assert.False(fixture.Layout().TryExtract(source, out var panel, out var seen));
        Assert.Null(panel);
        Assert.NotNull(seen);
        Assert.Equal("ambiguous-layout", seen.Status);
        Assert.False(seen.Qualified);
        Assert.Equal(DancePhase.Unknown, seen.Phase);
    }

    [Fact]
    public void SixthFallbackIsExplicitAndOnlyUsedAfterTwelfthFails()
    {
        using var fixture = new Fixture();
        using var source = fixture.Scene(DancePhase.Go, 1, new Point(0, -160));
        Assert.False(fixture.Layout(fallback: false).TryExtract(source, out var absent, out _));
        Assert.Null(absent);
        Assert.True(fixture.Layout().TryExtract(source, out var panel, out var seen));
        using (panel) Assert.Equal(PetDanceUiLayout.SearchRegion(source.Size, 2), seen!.SearchRegion);
    }

    [Fact]
    public void UnsupportedSizeAndScaleFailWithoutGuessing()
    {
        using var fixture = new Fixture();
        using var wrongSize = new Bitmap(1280, 720);
        Assert.False(fixture.Layout().TryExtract(wrongSize, out _, out _));
        using var unsupportedScale = fixture.Scene(DancePhase.Go, 1.26, Point.Empty);
        Assert.False(fixture.Layout().TryExtract(unsupportedScale, out _, out _));
        Assert.Throws<InvalidDataException>(() => fixture.Layout(scales: []));
        Assert.Throws<InvalidDataException>(() => fixture.Layout(scales: [double.NaN]));
        Assert.Throws<InvalidDataException>(() => fixture.Layout(scales: [1, 1]));
    }

    [Fact]
    public void ColorShapeAgreementToleratesOnePixelButRejectsDifferentColorOrShape()
    {
        const int width = 24, height = 24;
        var first = new byte[width * height]; var shifted = new byte[first.Length];
        var wrongColor = new byte[first.Length]; var unrelated = new byte[first.Length];
        for (var y = 4; y < 12; y++)
            for (var x = 4; x < 12; x++)
            {
                first[y * width + x] = 1;
                shifted[(y + 1) * width + x + 1] = 1;
                wrongColor[y * width + x] = 2;
                unrelated[(y + 10) * width + x + 10] = 1;
            }
        Assert.Equal(1, PetDanceUiLayout.ShapeAgreement(first, shifted, width, height));
        Assert.Equal(0, PetDanceUiLayout.ShapeAgreement(first, wrongColor, width, height));
        Assert.Equal(0, PetDanceUiLayout.ShapeAgreement(first, unrelated, width, height));
        Assert.Throws<ArgumentException>(() => PetDanceUiLayout.ShapeAgreement(first, shifted, 1000, 1000));
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly Color Background = Color.FromArgb(35, 65, 110);
        private static readonly Color Plate = Color.FromArgb(72, 8, 8);
        private readonly Dictionary<string, Bitmap> _references = [];
        private readonly DancePanelProfile _profile = new(1, 1282, 759, [512, 100, 256, 40],
            [512, 647, 256, 100], [98, 74, 64, 64], [13, 51, 232, 28], [38, 0, 180, 40],
            [31, 30, 19], [], "synthetic-test-only");

        public Fixture()
        {
            foreach (var phase in new[] { DancePhase.Watch, DancePhase.Go, DancePhase.Resolving })
                _references.Add(phase.ToString(), Panel(phase, true));
        }

        public PetDanceUiLayout Layout(bool fallback = true, double[]? scales = null) => new(_profile, _references,
            new(scales ?? [1, 7d / 6, 4d / 3, 1.5, 5d / 3], AllowSixthFallback: fallback));

        public Bitmap Scene(DancePhase phase, double scale, Point shift, bool button = true)
        {
            using var panel = Panel(phase, button);
            var image = new Bitmap(1282, 759, PixelFormat.Format24bppRgb);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(Background);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            // Only the bottom HUD is transformed. Scene content and upper feedback stay independent.
            var targetX = 640.5 + shift.X;
            var targetY = 670.5 - 87.75 * (scale - 1) + shift.Y;
            var destination = new RectangleF((float)(targetX + (512 - 640.5) * scale),
                (float)(targetY + (647 - 670.5) * scale), (float)(256 * scale), (float)(100 * scale));
            graphics.DrawImage(panel, destination, new RectangleF(0, 40, 256, 100), GraphicsUnit.Pixel);
            return image;
        }

        private static Bitmap Panel(DancePhase phase, bool button)
        {
            var image = new Bitmap(256, 140, PixelFormat.Format24bppRgb);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(Background);
            using var fill = new SolidBrush(Plate);
            graphics.FillRectangle(fill, 16, 52, 226, 24);
            if (phase == DancePhase.Go)
            {
                graphics.FillRectangle(Brushes.Lime, 101, 57, 7, 14);
                graphics.FillRectangle(Brushes.Lime, 112, 57, 20, 4);
                graphics.FillRectangle(Brushes.Lime, 112, 67, 20, 4);
                graphics.FillRectangle(Brushes.Lime, 128, 57, 4, 14);
            }
            else if (phase == DancePhase.Watch)
            {
                graphics.FillRectangle(Brushes.Yellow, 84, 57, 7, 14);
                graphics.FillRectangle(Brushes.Yellow, 103, 57, 7, 14);
                graphics.FillRectangle(Brushes.Yellow, 122, 57, 7, 14);
                graphics.FillRectangle(Brushes.Yellow, 141, 57, 7, 14);
            }
            else if (phase == DancePhase.Resolving)
            {
                graphics.FillRectangle(Brushes.Yellow, 60, 57, 16, 5);
                graphics.FillRectangle(Brushes.Yellow, 165, 66, 20, 5);
                graphics.FillRectangle(Brushes.Yellow, 190, 57, 7, 14);
            }
            if (button)
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.FillEllipse(Brushes.Wheat, 104, 79, 50, 50);
                graphics.FillEllipse(Brushes.Black, 106, 81, 46, 46);
                graphics.FillEllipse(Brushes.LightGray, 110, 85, 38, 38);
            }
            return image;
        }

        public void Dispose() { foreach (var image in _references.Values) image.Dispose(); }
    }
}
