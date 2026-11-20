using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceEntryImageReviewTests
{
    private static string Folder => Path.Combine(AppContext.BaseDirectory, "TestData", "PetDance");
    private static Bitmap Ref(string group, string name) => new(Path.Combine(Folder, group, name + ".png"));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static PetDanceMenuText Text(Size size) => new(size, [
        new("DANCE", new(591, 211, 50, 15)), new("GAME", new(646, 212, 45, 14)),
        new("OBJECTIVE", new(440, 343, 65, 12)), new("SELECT", new(602, 475, 44, 10)),
        new("LEVEL", new(650, 475, 36, 10)), new("PLAY", new(778, 574, 42, 12))]);

    private static Bitmap Frame(bool selected, bool hoveredPlay = true)
    {
        var image = new Bitmap(1282, 760);
        using var icon = Ref("Selection", selected ? "selected-away" : "unselected");
        using var play = Ref(hoveredPlay ? "Review" : "Entry", hoveredPlay ? "play-enabled-hover" : "play-disabled");
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.DarkGray);
        graphics.DrawImageUnscaled(icon, 452, 485);
        graphics.DrawImageUnscaled(play, 754, 568);
        return image;
    }

    private static PetDanceEntryImageResult Analyze(Bitmap image, bool pointerAway = true)
    {
        using var unselected = Ref("Selection", "unselected"); using var selected = Ref("Selection", "selected-away");
        using var disabled = Ref("Entry", "play-disabled"); using var enabled = Ref("Entry", "play-enabled");
        using var hover = Ref("Review", "play-enabled-hover");
        return PetDanceEntryImageReview.Analyze(image, new string('a', 64), 100, Text(image.Size), true, pointerAway,
            new(unselected, selected), new(disabled, enabled, hover));
    }

    [Fact]
    public void ActualHoverReferenceHasPinnedProvenanceAndRequiresItsOwnAppearance()
    {
        using var provenance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Folder, "Review", "provenance.json")));
        Assert.Equal(provenance.RootElement.GetProperty("sha256").GetString(),
            Hash(File.ReadAllBytes(Path.Combine(Folder, "Review", "play-enabled-hover.png"))));
        Assert.False(provenance.RootElement.GetProperty("input_video_alignment_verified").GetBoolean());
        using var disabled = Ref("Entry", "play-disabled"); using var enabled = Ref("Entry", "play-enabled");
        using var hover = Ref("Review", "play-enabled-hover");
        Assert.Equal(PetDancePlayState.Enabled, new PetDancePlayButtonVision(disabled, enabled, hover)
            .Classify(hover, new(24, 6, 42, 12)).State);
        Assert.Throws<ArgumentException>(() => new PetDancePlayButtonVision(disabled, enabled, disabled));
    }

    [Fact]
    public void MeasuredIconAndHoverPlayWithExplicitContextProduceReadyEntry()
    {
        using var frame = Frame(true);
        var result = Analyze(frame);
        Assert.Equal(PetDanceLevelSelectionState.SelectedAppearance, result.Selection.State);
        Assert.True(result.Selection.SelectionVerified);
        Assert.Equal(PetDanceScene.InstructionsReady, result.Entry.Scene);
        Assert.Equal(PetDanceArena.WizardCity, result.Entry.SelectedArena);
        // This fixture pastes actual crops onto a synthetic canvas; it does not
        // establish OCR accuracy, current geometry, authorization or live success.
        var visit = new PetDanceVisit(new("synthetic", frame.Size, "test", PetDanceArena.WizardCity, Enabled: true));
        var plan = visit.Observe(new(100, new('a', 64), result.Entry.Scene, "synthetic", frame.Size, "test", result.Entry,
            Fresh: true, TargetReady: true, ContextVerified: true, InputOwned: true));
        Assert.False(plan.IsProposal);
        Assert.Equal("entry-cost-not-approved", plan.Reason);
    }

    [Fact]
    public void AppearanceDoesNotInventPointerEvidenceOrReadyState()
    {
        using var frame = Frame(true);
        var result = Analyze(frame, pointerAway: false);
        Assert.Equal(PetDanceLevelSelectionState.SelectedAppearance, result.Selection.State);
        Assert.False(result.Selection.SelectionVerified);
        Assert.Equal(PetDanceScene.InstructionsUnselected, result.Entry.Scene);
    }

    [Fact]
    public void DisabledPlayAndUnselectedIconRemainUnselected()
    {
        using var frame = Frame(false, hoveredPlay: false);
        var result = Analyze(frame);
        Assert.Equal(PetDanceLevelSelectionState.Unselected, result.Selection.State);
        Assert.Equal(PetDancePlayState.Disabled, result.Play!.State);
        Assert.Equal(PetDanceScene.InstructionsUnselected, result.Entry.Scene);
    }

    [Fact]
    public void LoadingWithoutInstructionTextCannotRetainSelection()
    {
        using var frame = Frame(true);
        using var unselected = Ref("Selection", "unselected"); using var selected = Ref("Selection", "selected-away");
        using var disabled = Ref("Entry", "play-disabled"); using var enabled = Ref("Entry", "play-enabled");
        var result = PetDanceEntryImageReview.Analyze(frame, new('a', 64), 100, new(frame.Size, []), true, true,
            new(unselected, selected), new(disabled, enabled));
        Assert.Equal(PetDanceScene.Unknown, result.Entry.Scene);
        Assert.False(result.Selection.SelectionVerified);
    }

    [Fact]
    public async Task ReviewUsesSameVerifiedBytesForDecodeDespiteSourceReplacement()
    {
        using var fixture = new Files();
        var data = fixture.Manifest with { Frames = [fixture.Manifest.Frames[0], fixture.Manifest.Frames[0] with { TimeMs = 200 }] };
        fixture.Save(data);
        var calls = 0;
        Assert.True(await PetDanceEntryImageReview.RunAsync(fixture.Input, fixture.Output, (image, token) =>
        {
            calls++;
            File.WriteAllText(Path.Combine(fixture.Directory, "frame.png"), "replaced after verification");
            return Task.FromResult(Text(image.Size));
        }));
        Assert.Equal(2, calls);
        using var report = JsonDocument.Parse(File.ReadAllBytes(fixture.Output));
        Assert.Equal(1, report.RootElement.GetProperty("unique_image_count").GetInt32());
        Assert.Equal("supplied-test-reader", report.RootElement.GetProperty("ocr_origin").GetString());
        Assert.False(report.RootElement.GetProperty("live_qualified").GetBoolean());
        Assert.False(report.RootElement.GetProperty("runtime_enabled").GetBoolean());
        Assert.Equal(0, report.RootElement.GetProperty("desktop_dispatch_count").GetInt32());
        Assert.True(report.RootElement.GetProperty("reviews")[1].GetProperty("repeated_image").GetBoolean());
        await Assert.ThrowsAsync<InvalidDataException>(() => PetDanceEntryImageReview.RunAsync(fixture.Input,
            Path.Combine(fixture.Directory, "new-report.json"), (image, token) => Task.FromResult(Text(image.Size))));
    }

    [Theory]
    [InlineData("../frame.png", false)]
    [InlineData("frame.png", true)]
    public async Task PathEscapesAndWrongHashesCannotBecomeEvidence(string name, bool wrongHash)
    {
        using var fixture = new Files();
        var frame = fixture.Manifest.Frames[0];
        fixture.Save(fixture.Manifest with { Frames = [frame with { Image = new(name, wrongHash ? new('0', 64) : frame.Image.Sha256) }] });
        await Assert.ThrowsAsync<InvalidDataException>(() => PetDanceEntryImageReview.RunAsync(fixture.Input,
            fixture.Output, (image, token) => Task.FromResult(Text(image.Size))));
        Assert.False(File.Exists(fixture.Output));
    }

    [Fact]
    public async Task ExistingReportsAndRepeatedTimesAreRejected()
    {
        using var fixture = new Files();
        File.WriteAllText(fixture.Output, "keep");
        await Assert.ThrowsAsync<IOException>(() => PetDanceEntryImageReview.RunAsync(fixture.Input, fixture.Output,
            (image, token) => Task.FromResult(Text(image.Size))));
        Assert.Equal("keep", File.ReadAllText(fixture.Output));
        fixture.Save(fixture.Manifest with { Frames = [fixture.Manifest.Frames[0], fixture.Manifest.Frames[0]] });
        await Assert.ThrowsAsync<InvalidDataException>(() => PetDanceEntryImageReview.RunAsync(fixture.Input,
            Path.Combine(fixture.Directory, "time-report.json"), (image, token) => Task.FromResult(Text(image.Size))));
    }

    [Fact]
    public void DecoderRejectsUnboundedAndNonPngContentBeforeRasterAllocation()
    {
        Assert.Throws<InvalidDataException>(() => PetDanceEntryImageReview.Decode(new byte[100]));
        var bytes = File.ReadAllBytes(Path.Combine(Folder, "Review", "play-enabled-hover.png"));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), 65535);
        Assert.Throws<InvalidDataException>(() => PetDanceEntryImageReview.Decode(bytes));
    }

    [Fact]
    public async Task CancelledReaderKeepsItsBorrowedBitmapUntilItActuallyStops()
    {
        var bytes = File.ReadAllBytes(Path.Combine(Folder, "Review", "play-enabled-hover.png"));
        using var cancellation = new CancellationTokenSource();
        var pending = new TaskCompletionSource<PetDanceMenuText>();
        Bitmap? borrowed = null;
        var review = PetDanceEntryImageReview.ReadSnapshotAsync(bytes, (image, token) =>
        {
            borrowed = image;
            cancellation.Cancel();
            return pending.Task; // Deliberately ignores cancellation.
        }, cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => review);
        Assert.NotNull(borrowed);
        Assert.Equal(90, borrowed.Width);
        var text = new PetDanceMenuText(borrowed.Size, []);
        pending.SetResult(text);
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try { _ = borrowed.GetPixel(0, 0); }
            catch (ArgumentException) { return; }
            await Task.Delay(10);
        }
        Assert.Throws<ArgumentException>(() => borrowed.GetPixel(0, 0));
    }

    [Fact]
    public async Task ExplicitTextAnnotationsAvoidOcrAndRetainTheirEvidenceKind()
    {
        using var fixture = new Files();
        fixture.Save(fixture.Manifest with
        {
            Frames = [fixture.Manifest.Frames[0] with
            { Text = Text(new(1282, 760)), TextOrigin = "manual-annotation" }]
        });
        Assert.True(await PetDanceEntryImageReview.RunAsync(fixture.Input, fixture.Output,
            (image, token) => throw new InvalidOperationException("Supplied annotations must not start OCR.")));
        using var report = JsonDocument.Parse(File.ReadAllBytes(fixture.Output));
        Assert.Equal("manual-annotation", report.RootElement.GetProperty("reviews")[0].GetProperty("text_origin").GetString());
        Assert.Equal("supplied-text-see-per-frame-origin", report.RootElement.GetProperty("ocr_origin").GetString());
    }

    private sealed class Files : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "maelstrom-entry-review-" + Guid.NewGuid().ToString("N"));
        public string Input => Path.Combine(Directory, "manifest.json");
        public string Output => Path.Combine(Directory, "report.json");
        public PetDanceEntryReviewFixture Manifest { get; }
        public Files()
        {
            System.IO.Directory.CreateDirectory(Directory);
            PetDanceReviewImage Copy(string group, string file, string dest)
            {
                var bytes = File.ReadAllBytes(Path.Combine(Folder, group, file + ".png"));
                File.WriteAllBytes(Path.Combine(Directory, dest), bytes);
                return new(dest, Hash(bytes));
            }
            var refs = new PetDanceReviewReferences(Copy("Selection", "unselected", "unselected.png"),
                Copy("Selection", "selected-away", "selected.png"), Copy("Entry", "play-disabled", "disabled.png"),
                Copy("Entry", "play-enabled", "enabled.png"), Copy("Review", "play-enabled-hover", "hover.png"));
            using var frame = Frame(true); frame.Save(Path.Combine(Directory, "frame.png"), ImageFormat.Png);
            var hash = Hash(File.ReadAllBytes(Path.Combine(Directory, "frame.png")));
            Manifest = new(1, "synthetic-test", "synthetic-offsets", new('b', 64), refs,
                [new(new("frame.png", hash), 100, true, true, "SelectedAppearance")]);
            Save(Manifest);
        }
        public void Save(PetDanceEntryReviewFixture data) => File.WriteAllBytes(Input, JsonSerializer.SerializeToUtf8Bytes(data, PetDanceVisitReplay.Json));
        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }
}
