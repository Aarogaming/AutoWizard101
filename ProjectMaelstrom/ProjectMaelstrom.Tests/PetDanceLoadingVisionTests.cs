using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetDanceLoadingVisionTests
{
    [Fact]
    public void OnePixelDifferenceRequiresDeclaredMappingAndPreservesPixelsWhenSelected()
    {
        using var frame = new Bitmap(128, 132, PixelFormat.Format24bppRgb);
        using (var draw = Graphics.FromImage(frame))
        {
            draw.Clear(Color.Black);
            using var cue = PanelFixture.Frame("Up");
            draw.DrawImageUnscaled(cue, 0, 0);
            // Thin rows make the regression sensitive to unintended resampling.
            for (var y = 0; y < 130; y += 2) draw.DrawLine(Pens.White, 110, y, 127, y);
        }
        using var shorter = frame.Clone(new Rectangle(0, 0, 128, 131), PixelFormat.Format24bppRgb);
        using var exactFixture = new PanelFixture(canonicalHeight: 132);
        using var exact = new PetDancePanelVision(exactFixture.ProfilePath);
        Assert.Throws<InvalidDataException>(() => exact.Extract(shorter));
        using var mappedFixture = new PanelFixture(canonicalHeight: 132,
            captureGeometry: new(128, 131, "preserve-pixels", "synthetic unchanged-pixel regression"));
        using var mapped = new PetDancePanelVision(mappedFixture.ProfilePath);
        using var expected = exact.Extract(frame);
        using var actual = mapped.Extract(shorter);
        for (var y = 0; y < expected.Height; y++)
            for (var x = 0; x < expected.Width; x++)
                Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
        Assert.Equal(DancePhase.Watch, mapped.Read(actual, 0).Phase);
        using var wrongWidth = new Bitmap(127, 131);
        Assert.Throws<InvalidDataException>(() => mapped.Extract(wrongWidth));
    }

    [Fact]
    public void DeclaredScaleMappingIsStillAvailableButNeverChosenImplicitly()
    {
        using var fixture = new PanelFixture(canonicalHeight: 132,
            captureGeometry: new(128, 131, "scale-one-pixel-height", "synthetic scale declaration"));
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var shorter = new Bitmap(128, 131);
        using var panel = vision.Extract(shorter);
        Assert.Equal(vision.PanelSize, panel.Size);
        Assert.Equal("scale-one-pixel-height", vision.CaptureGeometry!.Transform);
    }

    [Theory]
    [InlineData(127, 131, "preserve-pixels", "evidence")]
    [InlineData(128, 130, "preserve-pixels", "evidence")]
    [InlineData(128, 131, "auto-best-match", "evidence")]
    [InlineData(128, 131, "preserve-pixels", "")]
    public void InvalidCaptureMappingCannotEnterAProfile(int width, int height, string transform, string evidence)
    {
        Assert.Throws<InvalidDataException>(() => PetDancePanelVision.ValidateCaptureGeometry(new Size(128, 132),
            new(width, height, transform, evidence), new Rectangle(0, 0, 128, 130)));
    }

    [Fact]
    public void PixelPreservingMappingCannotCropBeyondSourceBounds()
    {
        Assert.Throws<InvalidDataException>(() => PetDancePanelVision.ValidateCaptureGeometry(new Size(128, 132),
            new(128, 131, "preserve-pixels", "evidence"), new Rectangle(0, 0, 128, 132)));
    }

    [Fact]
    public void CalibratedLoadingHasNoDirectionalCueOrFade()
    {
        using var fixture = new PanelFixture();
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var loading = PanelFixture.Frame("Loading");

        foreach (var time in new[] { 0, 50, 100 })
        {
            var observation = vision.Read(loading, time);
            Assert.Equal(DancePhase.Loading, observation.Phase);
            Assert.Equal(DanceCue.Unknown, observation.Cue);
            Assert.Equal(DanceCue.Unknown, observation.FadeOf);
        }
    }

    [Fact]
    public void DifferentSceneOutsideCueAndPhaseRegionsIsNotLoading()
    {
        using var fixture = new PanelFixture();
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var scene = PanelFixture.Frame("Loading");
        using (var draw = Graphics.FromImage(scene))
            draw.FillRectangle(Brushes.Yellow, 80, 70, 40, 50);

        // Cue, phase and feedback regions still match Loading. The rest of the
        // panel must also agree before an unknown scene can become a pause.
        Assert.Equal(DancePhase.Unknown, vision.Read(scene, 0).Phase);
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(9, false)]
    public void LoadingToleranceAcceptsSmallNoiseButRejectsLargerChanges(int change, bool matches)
    {
        using var fixture = new PanelFixture();
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var scene = PanelFixture.Frame("Loading");
        using (var draw = Graphics.FromImage(scene))
            draw.Clear(Color.FromArgb(128 + change, change, 128 + change));

        Assert.Equal(matches ? DancePhase.Loading : DancePhase.Unknown, vision.Read(scene, 0).Phase);
    }

    [Theory]
    [InlineData("Watch")]
    [InlineData("Go")]
    public void LoadingReferencePreservesKnownGameplayPhases(string label)
    {
        using var fixture = new PanelFixture();
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var frame = PanelFixture.Frame(label);

        var observation = vision.Read(frame, 0);
        Assert.Equal(Enum.Parse<DancePhase>(label), observation.Phase);
        Assert.Equal(DanceCue.Neutral, observation.Cue);
    }

    [Theory]
    [InlineData("Up")]
    [InlineData("Down")]
    [InlineData("Left")]
    [InlineData("Right")]
    public void LoadingReferencePreservesClearDirectionalCues(string label)
    {
        using var fixture = new PanelFixture();
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var frame = PanelFixture.Frame(label);

        var observation = vision.Read(frame, 0);
        Assert.Equal(DancePhase.Watch, observation.Phase);
        Assert.Equal(Enum.Parse<DanceCue>(label), observation.Cue);
        Assert.Equal(DanceCue.Unknown, observation.FadeOf);
    }

    [Fact]
    public void KnownPhaseTakesPriorityEvenWhenLoadingReferenceAlsoMatches()
    {
        using var fixture = new PanelFixture(loadingTemplate: "Watch");
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var watch = PanelFixture.Frame("Watch");

        // Deliberately ambiguous calibration: the full image matches both
        // references, but a recognized gameplay phase must never pause.
        Assert.Equal(DancePhase.Watch, vision.Read(watch, 0).Phase);
    }

    [Theory]
    [InlineData("Success", "Failure")]
    [InlineData("Failure", "Success")]
    public void LoadingClearsLatchedOutcomeAndRequiresFreshResultEvidence(string first, string second)
    {
        using var fixture = new PanelFixture();
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var before = PanelFixture.Frame(first);
        using var loading = PanelFixture.Frame("Loading");
        using var resolving = PanelFixture.Frame("Resolving");
        using var after = PanelFixture.Frame(second);

        Assert.Equal(DancePhase.Resolving, vision.Read(before, 0).Phase);
        Assert.Equal(Enum.Parse<DancePhase>(first), vision.Read(before, 60).Phase);
        Assert.Equal(DancePhase.Loading, vision.Read(loading, 90).Phase);
        Assert.Equal(DancePhase.Resolving, vision.Read(resolving, 120).Phase);
        Assert.Equal(DancePhase.Resolving, vision.Read(after, 150).Phase);
        Assert.Equal(Enum.Parse<DancePhase>(second), vision.Read(after, 210).Phase);
    }

    [Fact]
    public void LegacyTenReferenceProfileTreatsLoadingAsUnknownAndStillReadsGameplay()
    {
        using var fixture = new PanelFixture(includeLoading: false);
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var loading = PanelFixture.Frame("Loading");
        using var watch = PanelFixture.Frame("Watch");
        using var go = PanelFixture.Frame("Go");

        Assert.Equal(DancePhase.Unknown, vision.Read(loading, 0).Phase);
        Assert.Equal(DancePhase.Watch, vision.Read(watch, 30).Phase);
        Assert.Equal(DancePhase.Go, vision.Read(go, 60).Phase);
    }

    /// <summary>Entirely synthetic calibration: no installed assets, capture or input.</summary>
    private sealed class PanelFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-loading-vision-" + Guid.NewGuid().ToString("N"));
        public string ProfilePath { get; }

        public PanelFixture(bool includeLoading = true, string loadingTemplate = "Loading", int canonicalHeight = 130,
            DancePanelCaptureGeometry? captureGeometry = null)
        {
            Directory.CreateDirectory(_root);
            ProfilePath = Path.Combine(_root, "profile.json");
            try
            {
                string[] labels = ["Neutral", "Up", "Down", "Left", "Right", "Watch", "Go", "Resolving", "Success", "Failure"];
                if (includeLoading) labels = [.. labels, "Loading"];
                var references = new List<DancePanelReference>();
                foreach (var label in labels)
                {
                    using var bitmap = Frame(label == "Loading" ? loadingTemplate : label);
                    using var encoded = new MemoryStream();
                    bitmap.Save(encoded, ImageFormat.Png);
                    var bytes = encoded.ToArray();
                    var file = label + ".png";
                    File.WriteAllBytes(Path.Combine(_root, file), bytes);
                    references.Add(new(label, file, Convert.ToHexString(SHA256.HashData(bytes)), "synthetic test fixture"));
                }
                var profile = new DancePanelProfile(1, 128, canonicalHeight,
                    [0, 0, 128, 30], [0, 30, 128, 100], [0, 66, 64, 64],
                    [20, 35, 80, 20], [10, 0, 100, 25], [31, 30, 19],
                    references.ToArray(), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("synthetic pet dance video identity"))), captureGeometry);
                File.WriteAllText(ProfilePath, JsonSerializer.Serialize(profile));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static Bitmap Frame(string label)
        {
            var bitmap = new Bitmap(128, 130, PixelFormat.Format24bppRgb);
            try
            {
                using var draw = Graphics.FromImage(bitmap);
                draw.Clear(label == "Loading" ? Color.FromArgb(128, 0, 128) : Color.Black);
                if (label == "Loading") return bitmap;

                var phase = label switch
                {
                    "Go" => Brushes.Lime,
                    "Resolving" or "Success" or "Failure" => Brushes.Blue,
                    _ => Brushes.Gold
                };
                draw.FillRectangle(phase, 20, 35, 80, 20);
                if (label is "Success" or "Failure")
                    draw.FillRectangle(label == "Success" ? Brushes.Lime : Brushes.Red, 25, 6, 60, 10);

                // Distinct bright quadrants lie inside the circular cue mask.
                // They stand in for four visually separable arrow templates.
                var quadrant = label switch
                {
                    "Up" => new Rectangle(14, 80, 17, 16),
                    "Down" => new Rectangle(31, 96, 17, 18),
                    "Left" => new Rectangle(14, 96, 17, 18),
                    "Right" => new Rectangle(31, 80, 17, 16),
                    _ => Rectangle.Empty
                };
                if (!quadrant.IsEmpty) draw.FillRectangle(Brushes.White, quadrant);
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
