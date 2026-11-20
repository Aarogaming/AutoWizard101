using System.Drawing;
using System.Security.Cryptography;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceEntryVisionTests
{
    private static string Folder => Path.Combine(AppContext.BaseDirectory, "TestData", "PetDance", "Entry");
    private const double Time = 1000; // Synthetic test time; saved capture timestamps are unavailable.
    private static readonly RectangleF WizardCityIcon = new(452, 485, 59, 58);

    private static (PetDanceMenuText Text, string Hash) Fixture(string id)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Folder, "entry-ocr.json")));
        var frame = document.RootElement.GetProperty("frames").EnumerateArray().Single(item => item.GetProperty("id").GetString() == id);
        var size = frame.GetProperty("image_size");
        var words = frame.GetProperty("words").EnumerateArray().Select(word =>
        {
            var box = word.GetProperty("bounds");
            return new PetDanceMenuWord(word.GetProperty("text").GetString()!, new(box.GetProperty("x").GetSingle(),
                box.GetProperty("y").GetSingle(), box.GetProperty("width").GetSingle(), box.GetProperty("height").GetSingle()));
        }).ToArray();
        return (new(new(size.GetProperty("width").GetInt32(), size.GetProperty("height").GetInt32()), words),
            frame.GetProperty("image_sha256").GetString()!);
    }

    private static PetDanceEntryEvidence HypotheticalSelection(PetDanceMenuText text, string hash) =>
        new(hash, Time, text.ImageSize, [new(PetDanceArena.WizardCity, WizardCityIcon, true)],
            PetDanceArena.WizardCity, SelectionHighlightVerified: true, PlayEnabledVerified: true);

    [Fact]
    public void RecordedOcrFixtureHasItsOwnHashAndDoesNotClaimCaptureTimesOrSelectionQualification()
    {
        var bytes = File.ReadAllBytes(Path.Combine(Folder, "entry-ocr.json"));
        using var provenance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Folder, "provenance.json")));
        Assert.Equal(provenance.RootElement.GetProperty("fixture_sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        using var fixture = JsonDocument.Parse(bytes);
        Assert.False(fixture.RootElement.GetProperty("capture_times_available").GetBoolean());
        Assert.False(fixture.RootElement.GetProperty("selection_detector_qualified").GetBoolean());
        Assert.Equal(0, fixture.RootElement.GetProperty("dispatch_count").GetInt32());
    }

    [Theory]
    [InlineData("instructions-disabled", "InstructionsUnselected")]
    [InlineData("instructions-hovered", "InstructionsUnselected")]
    [InlineData("loading-artwork", "Unknown")]
    [InlineData("sigil-title-unread", "Unknown")]
    [InlineData("idle-over-sigil", "NativeIdle")]
    public void ActualSavedOcrOnlyClaimsWhatItsWordsEstablish(string id, string expected)
    {
        var (text, hash) = Fixture(id);
        var scene = PetDanceEntryVision.Classify(text, hash, Time);
        Assert.Equal(expected, scene.Scene.ToString());
        Assert.Null(scene.SelectedArena); Assert.False(scene.SelectionVerified); Assert.False(scene.PlayEnabledVerified);
        Assert.Equal(hash, scene.ImageSha256); Assert.Equal(Time, scene.CapturedMs);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void ReadyNeedsIndependentIdentitySelectionAndEnabledProof(bool identity, bool selected, bool enabled)
    {
        var (text, hash) = Fixture("instructions-hovered");
        var evidence = HypotheticalSelection(text, hash) with
        {
            LevelControls = [new(PetDanceArena.WizardCity, WizardCityIcon, identity)],
            SelectionHighlightVerified = selected,
            PlayEnabledVerified = enabled
        };
        var scene = PetDanceEntryVision.Classify(text, hash, Time, evidence);
        Assert.Equal(identity && selected && enabled ? PetDanceScene.InstructionsReady : PetDanceScene.InstructionsUnselected, scene.Scene);
        if (scene.Scene == PetDanceScene.InstructionsReady)
        {
            Assert.Equal(PetDanceArena.WizardCity, scene.SelectedArena);
            Assert.True(scene.SelectionVerified); Assert.True(scene.PlayEnabledVerified);
            Assert.NotNull(scene.PlayBounds);
        }
    }

    [Fact]
    public void MeasuredHoverLabelCanIdentifyAControlButCannotProveSelection()
    {
        var (text, hash) = Fixture("instructions-hovered");
        var evidence = HypotheticalSelection(text, hash) with { SelectionHighlightVerified = false };
        var scene = PetDanceEntryVision.Classify(text, hash, Time, evidence);
        Assert.Equal(PetDanceScene.InstructionsUnselected, scene.Scene);
        Assert.Single(scene.LevelControls!); Assert.Null(scene.SelectedArena);
    }

    [Fact]
    public void SupplementalEvidenceCannotBeMovedAcrossFramesTimesOrGeometry()
    {
        var (text, hash) = Fixture("instructions-hovered");
        var original = HypotheticalSelection(text, hash);
        foreach (var evidence in new[] { original with { ImageSha256 = new('a', 64) },
            original with { CapturedMs = Time - 1 }, original with { Geometry = new(1280, 720) } })
        {
            var scene = PetDanceEntryVision.Classify(text, hash, Time, evidence);
            Assert.Equal(PetDanceScene.Unknown, scene.Scene);
            Assert.Equal("visual-evidence-frame-mismatch", scene.Reason);
        }
    }

    [Fact]
    public void GenericInteractionPromptNeedsTheMeasuredDanceTitleAndIdleTakesPrecedence()
    {
        foreach (var id in new[] { "sigil-title-unread", "idle-over-sigil" })
        {
            var (text, hash) = Fixture(id);
            var evidence = new PetDanceEntryEvidence(hash, Time, text.ImageSize, VerifiedSigilTitleBounds: new(525, 642, 75, 15));
            Assert.Equal(id == "sigil-title-unread" ? PetDanceScene.DanceSigil : PetDanceScene.NativeIdle,
                PetDanceEntryVision.Classify(text, hash, Time, evidence).Scene);
        }
    }

    [Fact]
    public void BlankOcrNeedsAnIndependentLoadingCueAndPositiveMenusRejectConflictingLoading()
    {
        var (text, hash) = Fixture("loading-artwork");
        Assert.Equal(PetDanceScene.Unknown, PetDanceEntryVision.Classify(text, hash, Time).Scene);
        Assert.Equal(PetDanceScene.Loading, PetDanceEntryVision.Classify(text, hash, Time,
            new(hash, Time, text.ImageSize, LoadingVerified: true)).Scene);
        (text, hash) = Fixture("instructions-hovered");
        Assert.Equal(PetDanceScene.Unknown, PetDanceEntryVision.Classify(text, hash, Time,
            new(hash, Time, text.ImageSize, LoadingVerified: true)).Scene);
    }

    [Fact]
    public void AmbiguousControlsAndMalformedOrOversizeWordsDoNotProduceEntryActions()
    {
        var (text, hash) = Fixture("instructions-hovered");
        var variants = new[]
        {
            text with { Words = [.. text.Words, new("PLAY!", new(950, 574, 42, 12))] },
            text with { Words = [new("DANCE", new(float.NaN, 1, 10, 10))] },
            text with { Words = Enumerable.Repeat(new PetDanceMenuWord("WORD", new(1, 1, 10, 10)), 513).ToArray() }
        };
        foreach (var variant in variants) Assert.Equal(PetDanceScene.Unknown, PetDanceEntryVision.Classify(variant, hash, Time).Scene);
        var evidence = HypotheticalSelection(text, hash);
        foreach (var controls in new PetDanceLevelControl[][]
        {
            [new(PetDanceArena.WizardCity, new(1, 1, 30, 30), true)],
            [new(PetDanceArena.WizardCity, WizardCityIcon, true), new(PetDanceArena.WizardCity, new(600, 485, 59, 58), true)],
            [new(PetDanceArena.WizardCity, WizardCityIcon, true), new(PetDanceArena.Krokotopia, WizardCityIcon, true)]
        }) Assert.Equal(PetDanceScene.Unknown, PetDanceEntryVision.Classify(text, hash, Time, evidence with { LevelControls = controls }).Scene);
    }

    [Theory]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void DeclaredSyntheticCoordinateScalingPreservesInstructionClassification(float scale)
    {
        var (text, hash) = Fixture("instructions-hovered");
        var scaled = text with
        {
            ImageSize = new((int)(text.ImageSize.Width * scale), (int)(text.ImageSize.Height * scale)),
            Words = text.Words.Select(word => word with
            {
                Bounds = new(word.Bounds.X * scale, word.Bounds.Y * scale,
                word.Bounds.Width * scale, word.Bounds.Height * scale)
            }).ToArray()
        };
        Assert.Equal(PetDanceScene.InstructionsUnselected, PetDanceEntryVision.Classify(scaled, hash, Time).Scene);
    }
}
