using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceArrowShapeMatcherTests
{
    private static readonly string[] Labels = ["Neutral", "Up", "Down", "Left", "Right"];
    private static Dictionary<string, Bitmap> References() => Labels.ToDictionary(label => label, label => Draw(label));
    private static PetDanceArrowShapeMatcher Create(IReadOnlyDictionary<string, Bitmap> references)
        => new(new Rectangle(0, 0, 64, 64), [31, 31, 25], references);

    // Independently rasterize the vector shape at the requested size. This is not
    // a resized copy of the template, so scale tests exercise different edge pixels.
    private static Bitmap Draw(string label, int size = 64, float shiftX = 0, float shiftY = 0)
    {
        var image = new Bitmap(size, size, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.FromArgb(47, 37, 28));
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(size / 64f, size / 64f);
        graphics.TranslateTransform(shiftX, shiftY);
        graphics.FillEllipse(Brushes.Black, 3, 3, 57, 57);
        graphics.FillEllipse(Brushes.Goldenrod, 5, 5, 53, 53);
        using var interior = new SolidBrush(label == "Neutral" ? Color.FromArgb(110, 105, 87) : Color.FromArgb(56, 148, 48));
        graphics.FillEllipse(interior, 7, 7, 49, 49);
        if (label != "Neutral")
        {
            graphics.TranslateTransform(31.5f, 31.5f);
            graphics.RotateTransform(label switch { "Down" => 180, "Left" => -90, "Right" => 90, _ => 0 });
            PointF[] arrow = [new(0, -19), new(14, -4), new(6, -4), new(6, 18), new(-6, 18), new(-6, -4), new(-14, -4)];
            graphics.FillPolygon(Brushes.Yellow, arrow);
            using var outline = new Pen(Color.FromArgb(25, 70, 22), 2);
            graphics.DrawPolygon(outline, arrow);
        }
        return image;
    }

    private static Bitmap Blend(Bitmap first, Bitmap second, double proportion)
    {
        var result = new Bitmap(first.Width, first.Height, PixelFormat.Format24bppRgb);
        for (var y = 0; y < first.Height; y++) for (var x = 0; x < first.Width; x++)
        {
            var a = first.GetPixel(x, y); var b = second.GetPixel(x, y);
            int Channel(int start, int end) => (int)Math.Round(start * (1 - proportion) + end * proportion);
            result.SetPixel(x, y, Color.FromArgb(Channel(a.R, b.R), Channel(a.G, b.G), Channel(a.B, b.B)));
        }
        return result;
    }

    [Theory]
    [InlineData("Up", 32)]
    [InlineData("Up", 96)]
    [InlineData("Up", 160)]
    [InlineData("Down", 32)]
    [InlineData("Down", 96)]
    [InlineData("Down", 160)]
    [InlineData("Left", 32)]
    [InlineData("Left", 96)]
    [InlineData("Left", 160)]
    [InlineData("Right", 32)]
    [InlineData("Right", 96)]
    [InlineData("Right", 160)]
    public void IndependentlyRasterizedIconScalesKeepTheirDirection(string direction, int size)
    {
        var references = References();
        try
        {
            var matcher = Create(references);
            using var image = Draw(direction, size);
            var result = matcher.MatchIcon(image);
            Assert.True(Enum.Parse<DanceCue>(direction) == result.Cue, $"{direction} at {size}px: {result}");
            Assert.Equal(DanceCue.Unknown, result.FadeOf);
            Assert.InRange(result.ShapeScore, .9, 1);
            Assert.True(result.Margin >= .08);
        }
        finally { foreach (var reference in references.Values) reference.Dispose(); }
    }

    [Theory]
    [InlineData("Up", 1, 0)]
    [InlineData("Down", 0, 1)]
    [InlineData("Left", -1, 0)]
    [InlineData("Right", 0, -1)]
    public void SmallIconAlignmentAndAntialiasDifferencesDoNotNeedExactPixels(string direction, int dx, int dy)
    {
        var references = References();
        try
        {
            var matcher = Create(references);
            using var shifted = Draw(direction, 64, dx, dy);
            var result = matcher.Match(shifted);
            Assert.True(Enum.Parse<DanceCue>(direction) == result.Cue, $"{direction} shift {dx},{dy}: {result}");
        }
        finally { foreach (var reference in references.Values) reference.Dispose(); }
    }

    [Theory]
    [InlineData("Up", .25)]
    [InlineData("Down", .45)]
    [InlineData("Left", .60)]
    [InlineData("Right", .35)]
    public void ASoftKnownFadeRemainsFadeOfItsDirection(string direction, double opacity)
    {
        var references = References();
        try
        {
            var matcher = Create(references);
            using var faded = Blend(references["Neutral"], references[direction], opacity);
            var result = matcher.Match(faded);
            Assert.True(DanceCue.Fading == result.Cue, $"{direction} opacity {opacity}: {result}");
            Assert.Equal(Enum.Parse<DanceCue>(direction), result.FadeOf);
            Assert.InRange(result.Opacity, opacity - .025, opacity + .025);
        }
        finally { foreach (var reference in references.Values) reference.Dispose(); }
    }

    [Fact]
    public void NeutralAndItsSmallRenderingNoiseDoNotInventAnArrow()
    {
        var references = References();
        try
        {
            var matcher = Create(references);
            Assert.Equal(DanceCue.Neutral, matcher.Match(references["Neutral"]).Cue);
            using var noisy = (Bitmap)references["Neutral"].Clone();
            for (var y = 0; y < noisy.Height; y++) for (var x = 0; x < noisy.Width; x++)
            {
                var pixel = noisy.GetPixel(x, y); var delta = (x + y) % 2 == 0 ? 2 : -2;
                noisy.SetPixel(x, y, Color.FromArgb(Math.Clamp(pixel.R + delta, 0, 255), Math.Clamp(pixel.G + delta, 0, 255), Math.Clamp(pixel.B + delta, 0, 255)));
            }
            Assert.Equal(DanceCue.Neutral, matcher.Match(noisy).Cue);
        }
        finally { foreach (var reference in references.Values) reference.Dispose(); }
    }

    [Fact]
    public void AmbiguousCompositeAndUnrelatedPatternAreUnknown()
    {
        var references = References();
        try
        {
            var matcher = Create(references);
            using var composite = Blend(references["Up"], references["Down"], .5);
            Assert.Equal(DanceCue.Unknown, matcher.Match(composite).Cue);
            using var unrelated = new Bitmap(64, 64);
            using (var graphics = Graphics.FromImage(unrelated))
            {
                graphics.Clear(Color.Magenta);
                using var pen = new Pen(Color.Cyan, 7);
                graphics.DrawLine(pen, 8, 8, 56, 56); graphics.DrawLine(pen, 56, 8, 8, 56);
            }
            Assert.Equal(DanceCue.Unknown, matcher.Match(unrelated).Cue);
        }
        finally { foreach (var reference in references.Values) reference.Dispose(); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(.01)]
    public void CommonBackgroundWithOnlyTinyDirectionalDetailCannotBecomeAFullArrow(double directionalFraction)
    {
        var references = References();
        try
        {
            var matcher = Create(references);
            using var common = new Bitmap(64, 64, PixelFormat.Format24bppRgb);
            for (var y = 0; y < common.Height; y++) for (var x = 0; x < common.Width; x++)
            {
                var pixels = Labels.Skip(1).Select(label => references[label].GetPixel(x, y)).ToArray();
                common.SetPixel(x, y, Color.FromArgb((int)Math.Round(pixels.Average(pixel => pixel.R)),
                    (int)Math.Round(pixels.Average(pixel => pixel.G)), (int)Math.Round(pixels.Average(pixel => pixel.B))));
            }
            using var almostCommon = Blend(common, references["Up"], directionalFraction);
            var result = matcher.Match(almostCommon);
            Assert.True(result.Cue == DanceCue.Unknown, $"Only {directionalFraction:P0} directional detail: {result}");
            Assert.InRange(result.Opacity, 0, .12);
        }
        finally { foreach (var reference in references.Values) reference.Dispose(); }
    }

    [Fact]
    public void ReferencesAreCopiedAndRecognitionDoesNotAccumulatePreviousFrameState()
    {
        var references = References();
        var matcher = Create(references);
        foreach (var reference in references.Values) reference.Dispose();
        using var up = Draw("Up"); using var down = Draw("Down"); using var blank = Draw("Neutral");
        var before = matcher.Match(up);
        var downResult = matcher.Match(down);
        Assert.True(DanceCue.Down == downResult.Cue, $"Exact Down reference: {downResult}");
        Assert.Equal(DanceCue.Neutral, matcher.Match(blank).Cue);
        Assert.Equal(before, matcher.Match(up));
    }

    [Fact]
    public void MissingOrIndistinctReferencesAndUnboundedPatchesAreRejected()
    {
        var references = References();
        try
        {
            Assert.Throws<ArgumentException>(() => new PetDanceArrowShapeMatcher(new(0, 0, 64, 64), [31, 31, 32], references));
            Assert.Throws<ArgumentException>(() => Create(new Dictionary<string, Bitmap>()));
            Assert.Throws<ArgumentException>(() => Create(Labels.ToDictionary(label => label, _ => references["Neutral"])));
            var duplicatedDirection = new Dictionary<string, Bitmap>(references) { ["Down"] = references["Up"] };
            Assert.Throws<ArgumentException>(() => Create(duplicatedDirection));
            var matcher = Create(references);
            using var screen = new Bitmap(640, 480);
            Assert.Throws<ArgumentException>(() => matcher.MatchIcon(screen));
            Assert.Throws<ArgumentException>(() => matcher.Match(screen));
            using var stretched = new Bitmap(100, 64);
            Assert.Throws<ArgumentException>(() => matcher.MatchIcon(stretched));
        }
        finally { foreach (var reference in references.Values) reference.Dispose(); }
    }
}
