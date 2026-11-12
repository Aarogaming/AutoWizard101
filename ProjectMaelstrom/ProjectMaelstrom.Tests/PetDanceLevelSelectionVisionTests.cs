using System.Security.Cryptography;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceLevelSelectionVisionTests
{
    private static string Folder => Path.Combine(AppContext.BaseDirectory, "TestData", "PetDance", "Selection");
    private static readonly string Hash = new('a', 64);
    private static Bitmap Crop(string state) => new(Path.Combine(Folder, state + ".png"));
    private static PetDanceLevelSelectionVision Matcher()
    {
        using var unselected = Crop("unselected"); using var selected = Crop("selected-away");
        return new(unselected, selected);
    }
    private static PetDanceLevelSelectionBinding Binding() => new(Hash, 100,
        PetDanceLevelSelectionVision.QualifiedGeometry,
        new(PetDanceArena.WizardCity, PetDanceLevelSelectionVision.QualifiedControlBounds, true),
        InstructionsVerified: true, PointerAwayVerified: true, PlayEnabledVerified: true);

    // The observed icon pixels are real; the background is explicitly synthetic
    // to test only the bounded matcher, not full-frame scene recognition.
    private static Bitmap Canvas(string state)
    {
        using var crop = Crop(state);
        var image = new Bitmap(PetDanceLevelSelectionVision.QualifiedGeometry.Width,
            PetDanceLevelSelectionVision.QualifiedGeometry.Height);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.Black);
        graphics.DrawImageUnscaled(crop, PetDanceLevelSelectionVision.QualifiedControlBounds.Location);
        return image;
    }

    [Fact]
    public void SavedCropsHaveExactProvenanceAndDiscloseNoIndependentHoldout()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Folder, "manifest.json")));
        var qualification = manifest.RootElement.GetProperty("qualification");
        Assert.False(qualification.GetProperty("live_qualified").GetBoolean());
        Assert.False(qualification.GetProperty("independent_session_holdout").GetBoolean());
        Assert.False(qualification.GetProperty("hover_only_unselected_negative_available").GetBoolean());
        var references = manifest.RootElement.GetProperty("references").EnumerateArray().ToArray();
        Assert.Equal(3, references.Length);
        foreach (var reference in references)
        {
            var path = Path.Combine(Folder, reference.GetProperty("file").GetString()!);
            Assert.Equal(reference.GetProperty("sha256").GetString(),
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
            using var image = new Bitmap(path);
            Assert.Equal(PetDanceLevelSelectionVision.QualifiedControlBounds.Size, image.Size);
            Assert.Equal(64, reference.GetProperty("source_sha256").GetString()!.Length);
        }
    }

    [Theory]
    [InlineData("unselected", "Unselected", false)]
    [InlineData("selected-away", "SelectedAppearance", true)]
    [InlineData("selected-hover", "Unknown", false)]
    public void ActualSavedIconPixelsRetainTheirDistinctAppearance(string sample, string expected, bool verified)
    {
        using var image = Canvas(sample);
        var result = Matcher().Classify(image, Hash, 100, Binding());
        Assert.Equal(expected, result.State.ToString());
        Assert.Equal(verified, result.SelectionVerified);
        Assert.Equal(Hash, result.ImageSha256);
        Assert.Equal(100, result.CapturedMs);
        Assert.Equal(image.Size, result.Geometry);
        Assert.InRange(result.Region!.Value.Width * result.Region.Value.Height, 1,
            PetDanceLevelSelectionVision.MaximumCropPixels);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void MeasuredSelectedAppearanceDoesNotReplaceIndependentPointerAndPlayEvidence(bool pointerAway, bool enabledPlay)
    {
        using var image = Canvas("selected-away");
        var binding = Binding() with { PointerAwayVerified = pointerAway, PlayEnabledVerified = enabledPlay };
        var result = Matcher().Classify(image, Hash, 100, binding);
        Assert.Equal(PetDanceLevelSelectionState.SelectedAppearance, result.State);
        Assert.False(result.SelectionVerified);
    }

    [Fact]
    public void IndependentArenaIdentityAndInstructionContextAreRequired()
    {
        using var image = Canvas("selected-away"); var matcher = Matcher(); var binding = Binding();
        foreach (var arena in Enum.GetValues<PetDanceArena>().Where(a => a != PetDanceArena.WizardCity))
            Assert.Equal(PetDanceLevelSelectionState.Unknown, matcher.Classify(image, Hash, 100,
                binding with { Control = binding.Control with { Arena = arena } }).State);
        Assert.Equal(PetDanceLevelSelectionState.Unknown, matcher.Classify(image, Hash, 100,
            binding with { Control = binding.Control with { IdentityVerified = false } }).State);
        Assert.Equal(PetDanceLevelSelectionState.Unknown, matcher.Classify(image, Hash, 100,
            binding with { InstructionsVerified = false }).State);
    }

    [Fact]
    public void StaleOrDifferentFrameBindingCannotQualifyCurrentPixels()
    {
        using var image = Canvas("selected-away"); var matcher = Matcher(); var binding = Binding();
        foreach (var changed in new[]
        {
            binding with { CapturedMs = 99 }, binding with { CapturedMs = double.NaN },
            binding with { ImageSha256 = new('b', 64) }, binding with { Geometry = new(1282, 759) }
        })
        {
            var result = matcher.Classify(image, Hash, 100, changed);
            Assert.Equal(PetDanceLevelSelectionState.Unknown, result.State);
            Assert.False(result.SelectionVerified);
            Assert.Equal("selection-frame-binding-mismatch", result.Reason);
        }
        Assert.Equal(PetDanceLevelSelectionState.Unknown, matcher.Classify(image, "bad-hash", 100, binding).State);
        Assert.Equal(PetDanceLevelSelectionState.Unknown, matcher.Classify(image, Hash, double.NaN, binding).State);
        Assert.Equal(PetDanceLevelSelectionState.Unknown, matcher.Classify(image, Hash, -1, binding).State);
    }

    [Theory]
    [InlineData(1282, 759)]
    [InlineData(1920, 1080)]
    [InlineData(2564, 1520)]
    public void DifferentBordersAndScalesRemainUnqualified(int width, int height)
    {
        using var image = new Bitmap(width, height);
        var result = Matcher().Classify(image, Hash, 100, Binding() with { Geometry = image.Size });
        Assert.Equal(PetDanceLevelSelectionState.Unknown, result.State);
        Assert.Equal("selection-layout-unqualified", result.Reason);
    }

    [Fact]
    public void ChangedControlRegistrationIsNotSilentlyRebased()
    {
        using var image = Canvas("selected-away"); var binding = Binding();
        foreach (var bounds in new[] { new RectangleF(453, 485, 59, 55), new RectangleF(452, 485, 118, 110),
            new RectangleF(float.NaN, 485, 59, 55), new RectangleF(-1, 485, 59, 55) })
            Assert.Equal(PetDanceLevelSelectionState.Unknown, Matcher().Classify(image, Hash, 100,
                binding with { Control = binding.Control with { Bounds = bounds } }).State);
    }

    [Fact]
    public void OcclusionTransparencyAndAmbiguousBlendsCannotQualifySelection()
    {
        var matcher = Matcher(); var region = PetDanceLevelSelectionVision.QualifiedControlBounds;
        using var image = Canvas("selected-away");
        using (var graphics = Graphics.FromImage(image)) graphics.FillRectangle(Brushes.Black,
            region.X + 10, region.Y + 10, region.Width - 20, region.Height - 20);
        Assert.Equal(PetDanceLevelSelectionState.Unknown, matcher.Classify(image, Hash, 100, Binding()).State);
        using var transparent = Canvas("selected-away");
        transparent.SetPixel(region.X + 1, region.Y + 1, Color.Transparent);
        Assert.Equal("selection-region-transparent", matcher.Classify(transparent, Hash, 100, Binding()).Reason);
        using var blend = Canvas("unselected"); using var selected = Crop("selected-away");
        for (var y = 0; y < region.Height; y++)
            for (var x = 0; x < region.Width; x++)
            {
                var a = blend.GetPixel(region.X + x, region.Y + y); var b = selected.GetPixel(x, y);
                blend.SetPixel(region.X + x, region.Y + y, Color.FromArgb((a.R + b.R) / 2, (a.G + b.G) / 2, (a.B + b.B) / 2));
            }
        Assert.Equal(PetDanceLevelSelectionState.Unknown, matcher.Classify(blend, Hash, 100, Binding()).State);
    }

    [Fact]
    public void ReferenceConstructorRejectsAmbiguityWrongSizeAndTransparency()
    {
        using var selected = Crop("selected-away"); using var unselected = Crop("unselected");
        Assert.Throws<ArgumentException>(() => new PetDanceLevelSelectionVision(selected, selected));
        using var wrongSize = new Bitmap(118, 110);
        Assert.Throws<ArgumentException>(() => new PetDanceLevelSelectionVision(wrongSize, selected));
        // The saved PNG is RGB. Editing it in place discards alpha, so construct
        // an explicit ARGB negative control before checking the opacity guard.
        using var transparent = new Bitmap(unselected.Width, unselected.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(transparent)) graphics.DrawImageUnscaled(unselected, 0, 0);
        transparent.SetPixel(0, 0, Color.Transparent);
        Assert.Equal(0, transparent.GetPixel(0, 0).A);
        Assert.Throws<ArgumentException>(() => new PetDanceLevelSelectionVision(transparent, selected));
    }
}
