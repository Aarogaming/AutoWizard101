using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetDanceCueAlignmentTests
{
    [Theory]
    [InlineData("Up")]
    [InlineData("Down")]
    [InlineData("Left")]
    [InlineData("Right")]
    public void SharedRingAlignmentRecognizesEveryTranslationWithinThreePixels(string label)
    {
        using var fixture = new Fixture();
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        double time = 0;
        for (var dy = -3; dy <= 3; dy++)
            for (var dx = -3; dx <= 3; dx++)
            {
                using var panel = Frame(label, dx, dy);
                var seen = vision.Read(panel, time += 20);
                Assert.Equal(Enum.Parse<DanceCue>(label), seen.Cue);
                Assert.Equal(DancePhase.Watch, seen.Phase);
                Assert.Equal(dx, seen.CueOffsetX);
                Assert.Equal(dy, seen.CueOffsetY);
                Assert.True(seen.CueAlignmentQualified);
                Assert.Equal(DanceCue.Unknown, seen.FadeOf);
            }
    }

    [Theory]
    [InlineData(-3, 3)]
    [InlineData(1, -2)]
    [InlineData(3, -3)]
    public void ShiftedNeutralAndFadesCannotBecomeFullDirectionCues(int dx, int dy)
    {
        using var fixture = new Fixture();
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var neutral = Frame("Neutral", dx, dy);
        Assert.Equal(DanceCue.Neutral, vision.Read(neutral, 0).Cue);
        double time = 0;
        foreach (var direction in new[] { "Up", "Down", "Left", "Right" })
        {
            using var fade = Frame(direction, dx, dy, opacity: .35);
            var seen = vision.Read(fade, time += 20);
            Assert.Equal(DanceCue.Fading, seen.Cue);
            Assert.Equal(Enum.Parse<DanceCue>(direction), seen.FadeOf);
            Assert.InRange(seen.Opacity, .34, .36);
        }
    }

    [Fact]
    public void UnrelatedCueImageIsUnknownEvenWhenThePhaseIsRecognized()
    {
        using var fixture = new Fixture();
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var unrelated = Frame("Watch");
        using (var draw = Graphics.FromImage(unrelated)) draw.FillRectangle(Brushes.Magenta, Cue);
        var seen = vision.Read(unrelated, 0);
        Assert.Equal(DancePhase.Watch, seen.Phase);
        Assert.Equal(DanceCue.Unknown, seen.Cue);
        Assert.False(seen.CueAlignmentQualified);
    }

    [Theory]
    [InlineData(-4, 0)]
    [InlineData(4, 0)]
    [InlineData(0, -4)]
    [InlineData(0, 4)]
    public void SearchDoesNotExpandToFollowLargerChanges(int dx, int dy)
    {
        using var fixture = new Fixture();
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var shifted = Frame("Up", dx, dy);
        var seen = vision.Read(shifted, 0);
        Assert.Equal(DanceCue.Unknown, seen.Cue);
        Assert.False(seen.CueAlignmentQualified);
    }

    [Fact]
    public void ArrowCannotChooseADifferentOffsetFromItsStationaryRing()
    {
        using var fixture = new Fixture();
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var scene = Frame("Up", arrowOnlyDx: 3);
        var seen = vision.Read(scene, 0);
        Assert.True(seen.CueAlignmentQualified);
        Assert.Equal(0, seen.CueOffsetX);
        Assert.Equal(0, seen.CueOffsetY);
        Assert.Equal(DanceCue.Unknown, seen.Cue);
    }

    [Fact]
    public void TexturelessLegacyProfilesKeepTheirExactUnshiftedBehavior()
    {
        using var fixture = new Fixture(ring: false);
        using var vision = new PetDancePanelVision(fixture.ProfilePath);
        using var exact = Frame("Left", ring: false);
        var seen = vision.Read(exact, 0);
        Assert.Equal(DanceCue.Left, seen.Cue);
        Assert.False(seen.CueAlignmentQualified);
        Assert.Equal(0, seen.CueOffsetX);
        Assert.Equal(0, seen.CueOffsetY);
    }

    private static readonly Rectangle Cue = new(32, 64, 64, 64);

    // Entirely synthetic textured button and four distinct quadrant glyphs.
    // Translation applies only to the button: phase and feedback stay stationary.
    private static Bitmap Frame(string label, int dx = 0, int dy = 0, double opacity = 1,
        bool ring = true, int arrowOnlyDx = 0)
    {
        var image = new Bitmap(128, 130, PixelFormat.Format24bppRgb);
        using (var draw = Graphics.FromImage(image))
        {
            draw.Clear(Color.FromArgb(20, 24, 28));
            draw.FillRectangle(label switch
            {
                "Go" => Brushes.Lime,
                "Resolving" or "Success" or "Failure" => Brushes.Blue,
                _ => Brushes.Gold
            }, 20, 34, 80, 20);
            if (label is "Success" or "Failure")
                draw.FillRectangle(label == "Success" ? Brushes.Lime : Brushes.Red, 25, 6, 60, 10);
        }
        for (var y = 0; y < Cue.Height; y++)
            for (var x = 0; x < Cue.Width; x++)
            {
                var localX = x - dx - 31; var localY = y - dy - 30;
                var radius = localX * localX + localY * localY;
                var value = 60;
                if (ring && radius is >= 400 and <= 676)
                    value = radius is >= 484 and <= 576 ? 230 : 25;
                var glyphX = localX - arrowOnlyDx;
                var glyph = glyphX * glyphX + localY * localY <= 256 && (label switch
                {
                    "Up" => glyphX < 0 && localY < 0,
                    "Down" => glyphX >= 0 && localY >= 0,
                    "Left" => glyphX < 0 && localY >= 0,
                    "Right" => glyphX >= 0 && localY < 0,
                    _ => false
                });
                if (glyph) value = (int)Math.Round(60 + 190 * opacity);
                image.SetPixel(Cue.X + x, Cue.Y + y, Color.FromArgb(value, value, value));
            }
        return image;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "maelstrom-cue-alignment-" + Guid.NewGuid().ToString("N"));
        public string ProfilePath { get; }

        public Fixture(bool ring = true)
        {
            Directory.CreateDirectory(_directory);
            ProfilePath = Path.Combine(_directory, "profile.json");
            try
            {
                var references = new List<DancePanelReference>();
                foreach (var label in new[] { "Neutral", "Up", "Down", "Left", "Right", "Watch", "Go", "Resolving", "Success", "Failure" })
                {
                    using var image = Frame(label, ring: ring);
                    using var encoded = new MemoryStream();
                    image.Save(encoded, ImageFormat.Png);
                    var bytes = encoded.ToArray();
                    var name = label + ".png";
                    File.WriteAllBytes(Path.Combine(_directory, name), bytes);
                    references.Add(new(label, name, Convert.ToHexString(SHA256.HashData(bytes)), "synthetic alignment fixture"));
                }
                var profile = new DancePanelProfile(1, 128, 130, [0, 0, 128, 30], [0, 30, 128, 100],
                    [Cue.X, Cue.Y, Cue.Width, Cue.Height], [20, 34, 80, 20], [10, 0, 100, 25], [31, 30, 19],
                    references.ToArray(), new string('a', 64));
                File.WriteAllText(ProfilePath, JsonSerializer.Serialize(profile));
            }
            catch { Dispose(); throw; }
        }
        public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
    }
}
