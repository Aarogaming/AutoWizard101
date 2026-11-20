using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceMenuWord(string Text, RectangleF Bounds, string RecognitionMethod = "windows-ocr");
internal sealed record PetDanceMenuText(Size ImageSize, IReadOnlyList<PetDanceMenuWord> Words)
{
    public string Text => string.Join(" ", Words.Select(word => word.Text));
}

/// <summary>Caps local OCR work independently of the game's native resolution.
/// The exact raster transform is retained to map recognized words back to pixels.</summary>
internal sealed record PetDanceOcrRaster(Size Size, double ScaleX, double ScaleY)
{
    internal const int MaximumPixels = 1500000;

    public static PetDanceOcrRaster Create(Size source, int requestedScale, int maximumDimension)
    {
        if (source.Width <= 0 || source.Height <= 0 || requestedScale is < 1 or > 4 || maximumDimension <= 0)
            throw new ArgumentOutOfRangeException(nameof(source), "OCR needs a positive region, bounded scale and engine limit.");
        var factor = Math.Min(requestedScale, Math.Min((double)maximumDimension / Math.Max(source.Width, source.Height),
            Math.Sqrt(MaximumPixels / ((double)source.Width * source.Height))));
        var width = Math.Max(1, (int)Math.Floor(source.Width * factor));
        var height = Math.Max(1, (int)Math.Floor(source.Height * factor));
        return new(new(width, height), (double)width / source.Width, (double)height / source.Height);
    }
}

/// <summary>Broad screen-relative OCR bands, not button locations. The upper band
/// includes both the Idle banner and Regular-UI Graphics heading; the lower band
/// retains whole control labels above the old 80% crop boundary. Raster work is
/// still capped by PetDanceOcrRaster and the number of ordinary passes is unchanged.</summary>
internal static class PetDanceMenuOcrRegions
{
    internal static Rectangle Header(Size size)
    {
        Validate(size);
        var top = size.Height * 4 / 100;
        return new(size.Width / 3, top, Math.Max(1, size.Width / 3), Math.Max(1, size.Height * 22 / 100 - top));
    }

    internal static Rectangle LowerControls(Size size)
    {
        Validate(size);
        var top = size.Height * 72 / 100;
        return new(size.Width / 4, top, Math.Max(1, size.Width / 2), size.Height - top);
    }

    private static void Validate(Size size)
    {
        if (!ModuleRecognition.ValidGeometry(size)) throw new ArgumentOutOfRangeException(nameof(size));
    }
}

internal enum PetDanceTextContrast { Color, DarkText, YellowText }

/// <summary>Preprocessing only, never control identity. Regular graphics-menu
/// labels are yellow on a red/purple button; the former dark-text threshold
/// erased their contrast. Select this raster mode only from a centered heading.</summary>
internal static class PetDanceMenuOcrContrast
{
    // Calibrated from module-frame-05.png, SHA256
    // 9980bfa5dcb0cb998a9acecb781b2a6b883c23af7de0dfa0aae163dfd0c58faa,
    // actual IDLE glyph rectangle (548,603,37,16). This is a visual template,
    // not OCR text and not a general menu-layout inference.
    internal const string IdleGlyph = "+AAAAAD5/B8H8Pn+Hwfw+e8fB/D55x8HAPnnnwcA+eOPB4D5448H4Pnjjwfg+eOPB4D5448HAPnnjwcA8e8P54Dx/w/n+PH+D+fwAHAOAHA=";

    // Hover changes the same glyph core from yellow to white. This color rule
    // is used only inside the independently anchored gap, then shape-checked.
    internal static bool IsIdleGlyphInk(Color color) => color.R >= 200 && color.G >= 200 &&
        (color.B < 120 || color.B >= 200 && Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B)) <= 35);

    internal static Rectangle? MatchIdleGlyph(byte[] mask, int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 100000 || mask.Length != width * height) return null;
        var left = width; var right = -1; var top = height; var bottom = -1;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (mask[y * width + x] != 0) { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
        var w = right - left + 1; var h = bottom - top + 1;
        if (h < 8 || w < 18 || Math.Abs((double)w / h - 37d / 16) > .25) return null;
        var expected = Convert.FromBase64String(IdleGlyph);
        var mismatch = 0; var intersection = 0; var union = 0;
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 37; x++)
            {
                var actual = mask[(top + Math.Min(h - 1, (int)((y + .5) * h / 16))) * width + left + Math.Min(w - 1, (int)((x + .5) * w / 37))] != 0;
                var reference = (expected[y * 5 + x / 8] & (128 >> (x % 8))) != 0;
                if (actual != reference) mismatch++;
                if (actual && reference) intersection++;
                if (actual || reference) union++;
            }
        return mismatch <= 47 && union > 0 && (double)intersection / union >= .85 ? new(left, top, w, h) : null;
    }

    internal static Rectangle? FindIdleGlyph(Bitmap image, Rectangle region)
    {
        if ((long)region.Width * region.Height > 100000) return null;
        var mask = new byte[region.Width * region.Height];
        for (var y = 0; y < region.Height; y++)
            for (var x = 0; x < region.Width; x++)
            {
                var color = image.GetPixel(region.X + x, region.Y + y);
                mask[y * region.Width + x] = IsIdleGlyphInk(color) ? (byte)1 : (byte)0;
            }
        // Hover also lights the button's vertical rails. Remove only columns
        // spanning the entire padded text row; glyphs retain whitespace above/below.
        RemoveFullHeightRails(mask, region.Width, region.Height);
        if (MatchIdleGlyph(mask, region.Width, region.Height) is not { } match) return null;
        match.Offset(region.Location);
        return match;
    }

    internal static void RemoveFullHeightRails(byte[] mask, int width, int height)
    {
        var columns = Enumerable.Range(0, width)
            .Where(x => Enumerable.Range(0, height).All(y => mask[y * width + x] != 0)).ToArray();
        foreach (var x in columns)
            for (var y = 0; y < height; y++) mask[y * width + x] = 0;
    }

    // This region requests calibrated visual matching, never an inferred control.
    internal static Rectangle? MissingIdleRegion(PetDanceMenuText text)
    {
        if (!HasGraphicsHeading(text)) return null;
        string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        var controls = text.Words.Where(word => PetDancePostGameClassifier.ValidBounds(word.Bounds, text.ImageSize) &&
            word.Bounds.Top >= text.ImageSize.Height * .45f && word.Bounds.Bottom <= text.ImageSize.Height * .95f).ToArray();
        if (controls.Any(word => Normalize(word.Text) == "IDLE")) return null;
        PetDanceMenuWord? Single(string name) => controls.Where(word => Normalize(word.Text).Replace('0', 'O') == name)
            .DistinctBy(word => (Math.Round(word.Bounds.X / 8), Math.Round(word.Bounds.Y / 8))).ToArray() is { Length: 1 } found ? found[0] : null;
        var quit = Single("QUIT"); var help = Single("HELP"); var ok = Single("OK");
        if (quit is null || help is null || ok is null || quit.Bounds.Right >= help.Bounds.Left || help.Bounds.Right >= ok.Bounds.Left) return null;
        var height = Math.Max(quit.Bounds.Height, Math.Max(help.Bounds.Height, ok.Bounds.Height));
        var center = help.Bounds.Top + help.Bounds.Height / 2;
        if (Math.Abs(quit.Bounds.Top + quit.Bounds.Height / 2 - center) > height * .8f ||
            Math.Abs(ok.Bounds.Top + ok.Bounds.Height / 2 - center) > height * .8f) return null;
        // The entire gap between established row anchors, with vertical whitespace.
        // It remains screen-relative and does not assume a fixed UI scale or button center.
        return Rectangle.Intersect(new(Point.Empty, text.ImageSize), Rectangle.FromLTRB(
            (int)Math.Ceiling(quit.Bounds.Right + height), (int)Math.Floor(center - height * .7f),
            (int)Math.Floor(help.Bounds.Left - height), (int)Math.Ceiling(center + height * .7f)));
    }

    internal static bool HasGraphicsHeading(PetDanceMenuText text)
    {
        var area = new RectangleF(text.ImageSize.Width * .25f, text.ImageSize.Height * .12f,
            text.ImageSize.Width * .5f, text.ImageSize.Height * .4f);
        return text.Words.Any(word => new string(word.Text.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray()) is "GRAPHICS" or "SOUND" or "GAMEPLAY" or "PRIVACY" &&
            PetDancePostGameClassifier.ValidBounds(word.Bounds, text.ImageSize) &&
            area.Contains(word.Bounds.Left + word.Bounds.Width / 2, word.Bounds.Top + word.Bounds.Height / 2));
    }

    internal static void Apply(byte[] pixels, PetDanceTextContrast contrast)
    {
        if (pixels.Length % 4 != 0 || pixels.Length > PetDanceOcrRaster.MaximumPixels * 4 || !Enum.IsDefined(contrast))
            throw new InvalidDataException("OCR contrast needs a bounded BGRA raster and a known mode.");
        if (contrast == PetDanceTextContrast.Color) return;
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            var b = pixels[offset]; var g = pixels[offset + 1]; var r = pixels[offset + 2];
            var ink = contrast == PetDanceTextContrast.YellowText
                // Saved Regular-UI pixels have bright yellow glyphs but darker gold
                // borders. Keeping both makes OCR group each label into its frame.
                ? r >= 200 && g >= 200 && b < 120
                : b + g + r < 270;
            pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = ink ? (byte)0 : (byte)255;
        }
    }
}

/// <summary>One optional OCR pass for an already recognized Dance level menu whose
/// PLAY text was missed. The broad lower-center grid includes the menu controls,
/// unlike the bottom banner pass. This helper supplies no scene or action evidence.</summary>
internal static class PetDanceMenuOcrRescue
{
    internal const int RequestedScale = 3;

    public static Rectangle? TryRegion(PetDanceMenuText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var size = text.ImageSize;
        if (size.Width < 4 || size.Height < 4 || text.Words is null || text.Words.Count > 512 ||
            text.Words.Any(word => word is null || word.Text is null || word.Text.Length > 128 ||
                !PetDancePostGameClassifier.ValidBounds(word.Bounds, size))) return null;
        if (PetDancePostGameClassifier.Classify(text).Scene is PetDanceScene.NativeIdle or PetDanceScene.GameResult or
            PetDanceScene.SnackOffer or PetDanceScene.MenuObserved) return null;
        var words = text.Words.Select(word => word with
        {
            Text = new string(word.Text.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray())
        }).ToArray();
        RectangleF Area(float x, float y, float width, float height) =>
            new(size.Width * x, size.Height * y, size.Width * width, size.Height * height);
        bool Within(PetDanceMenuWord word, RectangleF region) =>
            region.Contains(word.Bounds.Left + word.Bounds.Width / 2, word.Bounds.Top + word.Bounds.Height / 2);
        bool Pair(string first, string second, RectangleF region) => words.Where(word => word.Text == first && Within(word, region))
            .Any(left => words.Any(right => right.Text == second && Within(right, region) &&
                right.Bounds.Left >= left.Bounds.Right &&
                right.Bounds.Left - left.Bounds.Right <= Math.Max(left.Bounds.Height, right.Bounds.Height) * 3.5f &&
                Math.Abs(left.Bounds.Top + left.Bounds.Height / 2 - right.Bounds.Top - right.Bounds.Height / 2) <=
                    Math.Max(left.Bounds.Height, right.Bounds.Height) * .8f));
        if (words.Any(word => word.Text == "PLAY" && Within(word, Area(.25f, .5f, .6f, .45f))) ||
            !Pair("DANCE", "GAME", Area(.15f, .08f, .7f, .4f)) ||
            !Pair("SELECT", "LEVEL", Area(.15f, .35f, .7f, .55f))) return null;
        return new(size.Width / 4, (int)((long)size.Height * 3 / 5), size.Width / 2, size.Height / 4);
    }
}

/// <summary>Local Windows OCR used only for slow menu transitions. No network,
/// model service, microphone or client-memory access. The caller owns the bitmap.</summary>
internal sealed class PetDanceMenuOcr
{
    private bool _prepared;
    private readonly OcrEngine _engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"))
        ?? throw new InvalidOperationException("English text recognition is unavailable in Windows. Native Idle needs manual confirmation.");

    internal async Task PrepareAsync(CancellationToken token)
    {
        if (_prepared) return;
        using var blank = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(blank)) graphics.Clear(Color.White);
        _ = await ReadRegionAsync(blank, new(Point.Empty, blank.Size), 1, token).ConfigureAwait(false);
        _prepared = true;
    }

    public async Task<PetDanceMenuText> ReadAsync(Bitmap image, CancellationToken token)
    {
        var words = new List<PetDanceMenuWord>();
        words.AddRange(await ReadRegionAsync(image, new(Point.Empty, image.Size), 1, token));
        words.AddRange(await ReadRegionAsync(image, PetDanceMenuOcrRegions.Header(image.Size), 3, token));
        var lowerContrast = PetDanceMenuOcrContrast.HasGraphicsHeading(new(image.Size, words))
            ? PetDanceTextContrast.YellowText : PetDanceTextContrast.DarkText;
        words.AddRange(await ReadRegionAsync(image, PetDanceMenuOcrRegions.LowerControls(image.Size), 3, token, lowerContrast));
        if (PetDanceMenuOcrContrast.MissingIdleRegion(new(image.Size, words)) is { Width: > 0, Height: > 0 } idleRegion &&
            PetDanceMenuOcrContrast.FindIdleGlyph(image, idleRegion) is { } idleGlyph)
            words.Add(new("IDLE", idleGlyph, "calibrated-idle-glyph-v1"));
        if (PetDanceMenuOcrRescue.TryRegion(new(image.Size, words)) is { } rescue)
            words.AddRange(await ReadRegionAsync(image, rescue, PetDanceMenuOcrRescue.RequestedScale, token));
        var distinct = new List<PetDanceMenuWord>();
        foreach (var word in words)
            if (!distinct.Any(prior => prior.Text.Equals(word.Text, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(prior.Bounds.X - word.Bounds.X) < 8 && Math.Abs(prior.Bounds.Y - word.Bounds.Y) < 8)) distinct.Add(word);
        return new(image.Size, distinct);
    }

    private async Task<IReadOnlyList<PetDanceMenuWord>> ReadRegionAsync(Bitmap image, Rectangle region, int scale, CancellationToken token,
        PetDanceTextContrast contrast = PetDanceTextContrast.Color)
    {
        token.ThrowIfCancellationRequested();
        var raster = PetDanceOcrRaster.Create(region.Size, scale, (int)OcrEngine.MaxImageDimension);
        // Centered menus and the Idle/sigil banners use small decorative text.
        // Enlarge a broad center region instead of relying on tiny fixed buttons.
        using var copy = new Bitmap(raster.Size.Width, raster.Size.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(copy))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(image, new Rectangle(Point.Empty, copy.Size), region, GraphicsUnit.Pixel);
        }
        var data = copy.LockBits(new Rectangle(Point.Empty, copy.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[checked(copy.Width * copy.Height * 4)];
        try
        {
            for (var y = 0; y < copy.Height; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * copy.Width * 4, copy.Width * 4);
        }
        finally { copy.UnlockBits(data); }
        PetDanceMenuOcrContrast.Apply(pixels, contrast);
        using var writer = new DataWriter(); writer.WriteBytes(pixels);
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(writer.DetachBuffer(), BitmapPixelFormat.Bgra8,
            copy.Width, copy.Height, BitmapAlphaMode.Ignore);
        var result = await _engine.RecognizeAsync(bitmap).AsTask(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return result.Lines.SelectMany(line => line.Words).Select(word =>
        {
            var box = word.BoundingRect;
            return new PetDanceMenuWord(word.Text, new(region.X + (float)(box.X / raster.ScaleX), region.Y + (float)(box.Y / raster.ScaleY),
                (float)(box.Width / raster.ScaleX), (float)(box.Height / raster.ScaleY)));
        }).ToArray();
    }

    /// <summary>Offline inspection of explicitly supplied image files; no window capture or input.</summary>
    public static async Task ReviewAsync(string output, IEnumerable<string> images)
    {
        if (File.Exists(output)) throw new IOException("Choose a new menu review report.");
        var ocr = new PetDanceMenuOcr(); var reviews = new List<object>();
        PetDancePostGame? replay = null; PetDancePostGameScene? previous = null;
        var sequenceIndex = 0; var simulatedActions = new List<string>(); var simulatedComplete = false;
        foreach (var path in images)
        {
            using var image = new Bitmap(path);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var text = await ocr.ReadAsync(image, CancellationToken.None);
            var scene = PetDancePostGameClassifier.Classify(text);
            replay ??= new(new("offline-fixture", image.Size, "ordered-saved-frames", Enabled: true, TimeoutMs: 180000));
            var stable = previous?.Scene == scene.Scene && PetDanceNativeIdle.BoundsStable(previous.ActionBounds, scene.ActionBounds);
            var plan = replay.Observe(new(++sequenceIndex * 2000, scene, "offline-fixture", image.Size, "ordered-saved-frames",
                Fresh: stable, TargetReady: true, ContextVerified: true, InputOwned: true,
                GameplayCompleted: true, GameplayInputReleased: true));
            previous = scene;
            if (plan.IsProposal)
            {
                simulatedActions.Add(plan.Action.ToString());
                replay.RecordDispatch(plan.Action, accepted: true, inputReleased: true);
                previous = null;
            }
            simulatedComplete |= plan.Completed;
            reviews.Add(new { source = Path.GetFullPath(path), sha256 = GameExperiments.Digest(path), text,
                classification = scene, simulated_plan = plan, elapsed_ms = watch.Elapsed.TotalMilliseconds });
        }
        GameExperiments.WriteAtomic(output, new { kind = "offline-menu-ocr", inputs_sent = 0,
            dispatch_mode = "simulated-accepted-receipts-only-not-live-verification", simulated_complete = simulatedComplete,
            simulated_actions = simulatedActions, reviews });
    }
}
