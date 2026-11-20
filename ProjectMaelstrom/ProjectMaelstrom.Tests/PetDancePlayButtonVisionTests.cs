using System.Drawing;
using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDancePlayButtonVisionTests
{
    private static string Folder => Path.Combine(AppContext.BaseDirectory, "TestData", "PetDance", "Entry");
    private static readonly RectangleF LocalText = new(24, 6, 42, 12);
    private static Bitmap Image(string state) => new(Path.Combine(Folder, "play-" + state + ".png"));
    private static PetDancePlayButtonVision Matcher()
    {
        using var disabled = Image("disabled"); using var enabled = Image("enabled");
        return new(disabled, enabled);
    }

    [Fact]
    public void ReferenceCropsRetainExactSourceAndDerivativeHashes()
    {
        using var provenance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Folder, "provenance.json")));
        var references = provenance.RootElement.GetProperty("play_button_references").EnumerateArray().ToArray();
        Assert.Equal(2, references.Length);
        foreach (var reference in references)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Folder, reference.GetProperty("file").GetString()!));
            Assert.Equal(reference.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            Assert.Equal(64, reference.GetProperty("source_sha256").GetString()!.Length);
            Assert.Contains("not a holdout", reference.GetProperty("label_basis").GetString());
        }
    }

    [Theory]
    [InlineData("disabled", "Disabled")]
    [InlineData("enabled", "Enabled")]
    public void ActualPairedSavedPixelsDistinguishEnabledFromDisabled(string label, string expected)
    {
        using var image = Image(label);
        var result = Matcher().Classify(image, LocalText);
        Assert.Equal(expected, result.State.ToString());
        Assert.True(result.Similarity >= VisualStateRecognizer.MinimumSimilarity);
        Assert.True(result.Margin >= VisualStateRecognizer.MinimumMargin);
        Assert.Equal(new Rectangle(0, 0, 90, 25), result.Region);
    }

    [Theory]
    [InlineData("disabled", "Disabled", 2)]
    [InlineData("enabled", "Enabled", 2)]
    public void DeclaredSyntheticScaleTransformsStayBounded(string label, string expected, int scale)
    {
        using var original = Image(label);
        using var larger = new Bitmap(original.Width * scale, original.Height * scale);
        using (var graphics = Graphics.FromImage(larger))
        {
            // A synthetic game render has an opaque backing. DrawImage onto a
            // transparent bitmap can introduce edge alpha unrelated to UI scale.
            graphics.Clear(Color.Black);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.DrawImage(original, new Rectangle(Point.Empty, larger.Size));
        }
        var text = new RectangleF(LocalText.X * scale, LocalText.Y * scale, LocalText.Width * scale, LocalText.Height * scale);
        var result = Matcher().Classify(larger, text);
        Assert.True(expected == result.State.ToString(), result.ToString());
        Assert.InRange(result.Region!.Value.Width * result.Region.Value.Height, 1, PetDancePlayButtonVision.MaximumCropPixels);
    }

    [Fact]
    public void UnknownSolidPixelsAndTransparencyCannotBecomeAnEnabledControl()
    {
        var matcher = Matcher();
        using var solid = new Bitmap(90, 25);
        foreach (var color in new[] { Color.Black, Color.White, Color.Yellow, Color.Gray })
        {
            using (var graphics = Graphics.FromImage(solid)) graphics.Clear(color);
            Assert.Equal(PetDancePlayState.Unknown, matcher.Classify(solid, LocalText).State);
        }
        using (var graphics = Graphics.FromImage(solid)) graphics.Clear(Color.Transparent);
        Assert.Equal("play-region-transparent", matcher.Classify(solid, LocalText).Reason);
    }

    [Fact]
    public void AmbiguousReferencesAndUnqualifiedRegionsAreRejected()
    {
        using var enabled = Image("enabled");
        Assert.Throws<ArgumentException>(() => new PetDancePlayButtonVision(enabled, enabled));
        var matcher = Matcher();
        foreach (var bounds in new[] { new RectangleF(0, 0, 42, 12), new RectangleF(float.NaN, 1, 42, 12),
            new RectangleF(24, 6, 1000, 250), new RectangleF(24, 6, 4, 4), new RectangleF(24, 6, 12, 42) })
            Assert.Equal(PetDancePlayState.Unknown, matcher.Classify(enabled, bounds).State);
    }
}
