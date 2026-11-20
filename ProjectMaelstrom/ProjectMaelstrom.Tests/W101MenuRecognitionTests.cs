using System.Security.Cryptography;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101MenuRecognitionTests
{
    [Fact]
    public void YellowLabelContrastSeparatesGraphicsButtonsWithoutIncreasingOcrPasses()
    {
        // Representative yellow ink and red/purple background. Both become white
        // under the old dark-text mode, erasing the letters before OCR sees them.
        byte[] source = [0, 235, 250, 255, 70, 30, 190, 255, 100, 100, 100, 255, 30, 220, 30, 255];
        var dark = source.ToArray(); PetDanceMenuOcrContrast.Apply(dark, PetDanceTextContrast.DarkText);
        Assert.Equal(dark[0], dark[4]);
        var yellow = source.ToArray(); PetDanceMenuOcrContrast.Apply(yellow, PetDanceTextContrast.YellowText);
        Assert.Equal((byte)0, yellow[0]);
        Assert.Equal((byte)255, yellow[4]); Assert.Equal((byte)255, yellow[8]); Assert.Equal((byte)255, yellow[12]);
        Assert.All(Enumerable.Range(0, 4), index => Assert.Equal((byte)255, yellow[index * 4 + 3]));
    }

    [Fact]
    public void IdleGlyphAcceptsActualHoverWhiteWithoutAuthorizingColorsAlone()
    {
        Assert.True(PetDanceMenuOcrContrast.IsIdleGlyphInk(Color.FromArgb(255, 255, 255)));
        Assert.True(PetDanceMenuOcrContrast.IsIdleGlyphInk(Color.FromArgb(255, 255, 0)));
        Assert.False(PetDanceMenuOcrContrast.IsIdleGlyphInk(Color.FromArgb(181, 169, 1)));
        Assert.False(PetDanceMenuOcrContrast.IsIdleGlyphInk(Color.FromArgb(156, 48, 74)));
        using var background = new Bitmap(80, 30);
        using (var graphics = Graphics.FromImage(background)) graphics.Clear(Color.White);
        Assert.Null(PetDanceMenuOcrContrast.FindIdleGlyph(background, new(0, 0, 80, 30)));
        using (var graphics = Graphics.FromImage(background)) graphics.Clear(Color.Purple);
        var packed = Convert.FromBase64String(PetDanceMenuOcrContrast.IdleGlyph);
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 37; x++)
                if ((packed[y * 5 + x / 8] & (128 >> (x % 8))) != 0) background.SetPixel(20 + x, 7 + y, Color.White);
        for (var y = 0; y < 30; y++) { background.SetPixel(5, y, Color.White); background.SetPixel(70, y, Color.White); }
        var diagnosticMask = new byte[80 * 30];
        for (var y = 0; y < 30; y++)
            for (var x = 0; x < 80; x++)
                diagnosticMask[y * 80 + x] = x is 5 or 70 ? (byte)0 : PetDanceMenuOcrContrast.IsIdleGlyphInk(background.GetPixel(x, y)) ? (byte)1 : (byte)0;
        Assert.Equal(new Rectangle(20, 7, 37, 16), PetDanceMenuOcrContrast.MatchIdleGlyph(diagnosticMask, 80, 30));
        var withRails = diagnosticMask.ToArray();
        for (var y = 0; y < 30; y++) { withRails[y * 80 + 5] = 1; withRails[y * 80 + 70] = 1; }
        PetDanceMenuOcrContrast.RemoveFullHeightRails(withRails, 80, 30);
        Assert.Equal(diagnosticMask, withRails);
        Assert.Equal(new Rectangle(20, 7, 37, 16), PetDanceMenuOcrContrast.FindIdleGlyph(background, new(0, 0, 80, 30)));
        // A separate cursor-like fragment is not silently discarded as decoration.
        background.SetPixel(65, 10, Color.White);
        Assert.Null(PetDanceMenuOcrContrast.FindIdleGlyph(background, new(0, 0, 80, 30)));
    }

    [Fact]
    public void CalibratedIdleGlyphMatchesScaledPixelsAndRejectsCorruption()
    {
        var packed = Convert.FromBase64String(PetDanceMenuOcrContrast.IdleGlyph);
        var mask = new byte[74 * 32];
        for (var y = 0; y < 32; y++)
            for (var x = 0; x < 74; x++)
                mask[y * 74 + x] = (packed[y / 2 * 5 + x / 2 / 8] & (128 >> (x / 2 % 8))) != 0 ? (byte)1 : (byte)0;
        Assert.Equal(new Rectangle(0, 0, 74, 32), PetDanceMenuOcrContrast.MatchIdleGlyph(mask, 74, 32));
        Assert.Null(PetDanceMenuOcrContrast.MatchIdleGlyph(new byte[74 * 32], 74, 32));
        Assert.Null(PetDanceMenuOcrContrast.MatchIdleGlyph(Enumerable.Repeat((byte)1, 74 * 32).ToArray(), 74, 32));
        for (var y = 0; y < 32; y++) Array.Reverse(mask, y * 74, 74);
        Assert.Null(PetDanceMenuOcrContrast.MatchIdleGlyph(mask, 74, 32));
        Assert.Null(PetDanceMenuOcrContrast.MatchIdleGlyph([], int.MaxValue, int.MaxValue));
    }

    [Fact]
    public void MissingIdleRescueRequiresThreeAlignedReadLabelsAndNeverSuppliesControlEvidence()
    {
        var text = new PetDanceMenuText(new(1282, 759), [new("GRAPHICS", new(585, 118, 89, 18)),
            new("QUIT", new(412, 602, 44, 16)), new("HELP", new(674, 602, 44, 16)), new("0K", new(813, 602, 27, 16))]);
        var region = PetDanceMenuOcrContrast.MissingIdleRegion(text);
        Assert.NotNull(region);
        Assert.True(region.Value.Contains(565, 610));
        Assert.Equal(PetDanceScene.Unknown, PetDancePostGameClassifier.Classify(text).Scene);
        Assert.Null(PetDanceMenuOcrContrast.MissingIdleRegion(text with { Words = text.Words.Take(3).ToArray() }));
        Assert.Null(PetDanceMenuOcrContrast.MissingIdleRegion(text with { Words = [.. text.Words, new("IDLE", new(545, 602, 40, 16))] }));
        Assert.Null(PetDanceMenuOcrContrast.MissingIdleRegion(text with { Words = [.. text.Words.Take(3), new("OK", new(813, 680, 27, 16))] }));
    }

    [Fact]
    public void SavedRegularMenuGlyphColorsRemainInkWhileGoldButtonBordersDisappear()
    {
        // Samples from module-frame-05: yellow label core, two gold border tones,
        // and purple interior. Border removal leaves separate words for OCR.
        byte[] pixels = [0, 255, 255, 255, 1, 137, 148, 255, 1, 141, 148, 255, 66, 36, 132, 255];
        PetDanceMenuOcrContrast.Apply(pixels, PetDanceTextContrast.YellowText);
        Assert.Equal((byte)0, pixels[0]);
        Assert.All(new[] { 4, 8, 12 }, index => Assert.Equal((byte)255, pixels[index]));
    }

    [Fact]
    public void YellowContrastSelectionNeedsCenteredGraphicsHeadingAndCannotRecognizeMenuAlone()
    {
        var size = new Size(1282, 759);
        var heading = new PetDanceMenuText(size, [new("GRAPHICS", new(585, 118, 89, 18))]);
        Assert.True(PetDanceMenuOcrContrast.HasGraphicsHeading(heading));
        Assert.Equal(PetDanceScene.Unknown, PetDancePostGameClassifier.Classify(heading).Scene);
        Assert.False(PetDanceMenuOcrContrast.HasGraphicsHeading(heading with
        { Words = [new("GRAPHICS", new(20, 600, 89, 18))] }));
        Assert.False(PetDanceMenuOcrContrast.HasGraphicsHeading(heading with
        { Words = [new("GRAPHIC", new(585, 118, 89, 18))] }));
    }

    [Fact]
    public void ContrastKeepsOriginalColorModeAndRejectsInvalidRasters()
    {
        byte[] pixels = [12, 34, 56, 78];
        PetDanceMenuOcrContrast.Apply(pixels, PetDanceTextContrast.Color);
        Assert.Equal(new byte[] { 12, 34, 56, 78 }, pixels);
        Assert.Throws<InvalidDataException>(() => PetDanceMenuOcrContrast.Apply(new byte[3], PetDanceTextContrast.YellowText));
        Assert.Throws<InvalidDataException>(() => PetDanceMenuOcrContrast.Apply(pixels, (PetDanceTextContrast)999));
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(1282, 759)]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    public void BroadOcrBandsIncludeRegularMenuAnchorsWithoutExtraRasterBudget(int width, int height)
    {
        var size = new Size(width, height);
        var header = PetDanceMenuOcrRegions.Header(size);
        var controls = PetDanceMenuOcrRegions.LowerControls(size);
        // Geometry coverage based on the observed Regular-UI layout, not a claim
        // that synthetic points establish OCR accuracy on these resolutions.
        Assert.True(header.Contains(new Point((int)(width * .5), (int)(height * .165))));
        Assert.True(header.Contains(new Point((int)(width * .5), (int)(height * .08))));
        Assert.True(controls.Contains(new Point((int)(width * .5), (int)(height * .794))));
        Assert.True(controls.Contains(new Point((int)(width * .5), (int)(height * .9))));
        Assert.True(new Rectangle(Point.Empty, size).Contains(header));
        Assert.True(new Rectangle(Point.Empty, size).Contains(controls));
        foreach (var region in new[] { header, controls })
        {
            var raster = PetDanceOcrRaster.Create(region.Size, 3, 4096);
            Assert.True((long)raster.Size.Width * raster.Size.Height <= PetDanceOcrRaster.MaximumPixels);
        }
    }

    [Fact]
    public void OcrBandsRemainNonemptyForSmallImagesAndRejectInvalidGeometry()
    {
        Assert.Equal(new Rectangle(0, 0, 1, 1), PetDanceMenuOcrRegions.Header(new(1, 1)));
        Assert.Equal(new Rectangle(0, 0, 1, 1), PetDanceMenuOcrRegions.LowerControls(new(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PetDanceMenuOcrRegions.Header(Size.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => PetDanceMenuOcrRegions.LowerControls(new(8192, 8192)));
    }

    private static string Folder => Path.Combine(AppContext.BaseDirectory, "TestData", "Modules", "W101Menus");
    private static readonly RoutineBinding Binding = new("saved-window", "frame-1282x760", "offline.visit.one");
    private sealed record Case(byte[] Bytes, string Hash, string Scene, string[] Controls, PetDanceMenuText Text);
    private static Case ReadCase(string id)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Folder, "recognition-cases.json")));
        Assert.False(document.RootElement.GetProperty("capture_times_available").GetBoolean());
        var item = document.RootElement.GetProperty("cases").EnumerateArray().Single(value => value.GetProperty("id").GetString() == id);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("captured_ms").ValueKind);
        var bytes = File.ReadAllBytes(Path.Combine(Folder, item.GetProperty("image").GetString()!));
        var hash = item.GetProperty("source_image_sha256").GetString()!;
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        return new(bytes, hash, item.GetProperty("expected_scene").GetString()!,
            item.GetProperty("expected_control_ids").EnumerateArray().Select(value => value.GetString()!).ToArray(),
            item.GetProperty("text").Deserialize<PetDanceMenuText>(OperationModules.Json)!);
    }
    // The capture clock was not recorded in these fixtures. This is explicitly a
    // synthetic test clock; it is never claimed as original live timing evidence.
    private static ModuleVisualFrame Frame(Case sample) => new(sample.Hash, 100, sample.Text.ImageSize, Binding, "saved-ocr");

    [Theory]
    [InlineData("reward")]
    [InlineData("snack")]
    [InlineData("loading")]
    [InlineData("outside")]
    [InlineData("graphics-menu")]
    [InlineData("native-idle")]
    public void SavedActualOcrProducesBoundMenuScenesAndExactControls(string id)
    {
        var sample = ReadCase(id); var result = W101MenuRecognition.Classify(Frame(sample), sample.Text);
        ModuleRecognition.Validate(result);
        Assert.Equal(sample.Scene, result.Scene);
        Assert.Equal(sample.Controls, result.Controls.Select(control => control.ControlId).ToArray());
        Assert.Equal(sample.Hash, result.Frame.ImageSha256);
        Assert.Equal("saved-ocr", result.Frame.TextOrigin);
        Assert.All(result.Controls, control => Assert.True(ModuleRecognition.ValidBounds(control.Bounds, sample.Text.ImageSize)));
        if (id is "outside" or "loading") Assert.Empty(result.VisualFacts);
        else Assert.Equal(["wizard101.ui"], result.VisualFacts);
    }

    [Fact]
    public void DanceInstructionsUseObservedCancelWithoutInventingSelectedArenaOrPlayPermission()
    {
        var text = Instructions();
        var frame = new ModuleVisualFrame(new('a', 64), 100, text.ImageSize, Binding, "manual-annotation");
        var result = W101MenuRecognition.Classify(frame, text);
        Assert.Equal("SigilMenu.dance", result.Scene);
        Assert.Equal("sigil.dance.cancel", Assert.Single(result.Controls).ControlId);
        Assert.Equal(new RectangleF(463, 574, 45, 12), result.Controls[0].Bounds);
        Assert.Equal("manual-annotation", result.Frame.TextOrigin);
        var ambiguous = W101MenuRecognition.Classify(frame, text with { Words = [.. text.Words, new("CANCEL", new(600, 574, 45, 12))] });
        Assert.Equal("SigilMenu.dance", ambiguous.Scene);
        Assert.Empty(ambiguous.Controls);
        var unrelated = W101MenuRecognition.Classify(frame, text with { Words = text.Words.Where(word => word.Text != "DANCE").ToArray() });
        Assert.Equal("Unknown", unrelated.Scene);
        Assert.Empty(unrelated.Controls);
    }

    [Fact]
    public void NativeIdleKeepsPriorityOverUnderlyingDanceMenu()
    {
        var sample = ReadCase("native-idle");
        var text = sample.Text with { Words = [.. sample.Text.Words, .. Instructions().Words] };
        var result = W101MenuRecognition.Classify(Frame(sample), text);
        Assert.Equal("NativeIdle", result.Scene);
        Assert.Empty(result.Controls);
    }

    [Fact]
    public async Task EncodedImageSnapshotCannotBeReplacedDuringReadingAndReaderOriginIsTruthful()
    {
        var sample = ReadCase("graphics-menu");
        var result = await W101MenuRecognition.ReadPngAsync(sample.Bytes, Frame(sample), CancellationToken.None, (image, token) =>
        {
            Assert.Equal(sample.Text.ImageSize, image.Size);
            Array.Fill(sample.Bytes, (byte)0);
            return Task.FromResult(sample.Text);
        });
        Assert.Equal("MenuObserved", result.Scene);
        Assert.Equal(sample.Hash, result.Frame.ImageSha256);
        Assert.Equal("injected-test-ocr", result.Frame.TextOrigin);
    }

    [Fact]
    public async Task HashGeometryAndMalformedPngRejectionsHappenBeforeOcr()
    {
        var sample = ReadCase("graphics-menu"); var calls = 0;
        Task<PetDanceMenuText> Reader(Bitmap image, CancellationToken token) { calls++; return Task.FromResult(sample.Text); }
        await Assert.ThrowsAsync<InvalidDataException>(() => W101MenuRecognition.ReadPngAsync(sample.Bytes,
            Frame(sample) with { ImageSha256 = new('0', 64) }, CancellationToken.None, Reader));
        await Assert.ThrowsAsync<InvalidDataException>(() => W101MenuRecognition.ReadPngAsync(sample.Bytes,
            Frame(sample) with { Geometry = new(1280, 720) }, CancellationToken.None, Reader));
        var malformed = new byte[33];
        var badFrame = Frame(sample) with { ImageSha256 = Convert.ToHexString(SHA256.HashData(malformed)) };
        await Assert.ThrowsAsync<InvalidDataException>(() => W101MenuRecognition.ReadPngAsync(malformed, badFrame, CancellationToken.None, Reader));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CancelledReadDoesNotDisposePixelsStillBorrowedByTheReader()
    {
        var sample = ReadCase("graphics-menu"); using var cancellation = new CancellationTokenSource();
        var pending = new TaskCompletionSource<PetDanceMenuText>(); Bitmap? borrowed = null;
        var task = W101MenuRecognition.ReadPngAsync(sample.Bytes, Frame(sample), cancellation.Token, (image, token) =>
        {
            borrowed = image; cancellation.Cancel(); return pending.Task;
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.NotNull(borrowed);
        Assert.Equal(sample.Text.ImageSize, borrowed.Size);
        pending.SetResult(sample.Text);
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try { _ = borrowed.GetPixel(0, 0); }
            catch (ArgumentException) { return; }
            await Task.Delay(10);
        }
        Assert.Throws<ArgumentException>(() => borrowed.GetPixel(0, 0));
    }

    [Fact]
    public async Task PreCancelledReadNeverCallsTheReaderAndMismatchedTextCannotBecomeEvidence()
    {
        var sample = ReadCase("graphics-menu"); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => W101MenuRecognition.ReadPngAsync(sample.Bytes,
            Frame(sample), cancellation.Token, (image, token) => { calls++; return Task.FromResult(sample.Text); }));
        Assert.Equal(0, calls);
        await Assert.ThrowsAsync<InvalidDataException>(() => W101MenuRecognition.ReadPngAsync(sample.Bytes, Frame(sample),
            CancellationToken.None, (image, token) => Task.FromResult(sample.Text with { ImageSize = new(800, 600) })));
    }

    private static PetDanceMenuText Instructions() => new(new(1282, 760),
    [
        new("DANCE", new(591, 211, 50, 15)), new("GAME", new(646, 212, 45, 14)),
        new("OBJECTIVE", new(437, 343, 66, 11)), new("SELECT", new(604, 473, 36, 12)),
        new("LEVEL", new(643, 473, 32, 11)), new("PLAY!", new(778, 574, 42, 12)),
        new("CANCEL", new(463, 574, 45, 12))
    ]);
}
