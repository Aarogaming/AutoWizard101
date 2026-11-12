using System.Security.Cryptography;

namespace ProjectMaelstrom.Utilities;

/// <summary>Wizard101 vocabulary over the existing menu OCR and classifiers. This
/// adapter identifies visible menus and exact control text; it does not navigate,
/// infer world/home/clear-area facts, bind outcomes or send input.</summary>
internal static class W101MenuRecognition
{
    internal const int MaximumPngBytes = 8 * 1024 * 1024;
    internal const int ReadTimeoutMs = 5000;
    private static readonly SemaphoreSlim ReaderGate = new(1, 1);
    private static PetDanceMenuOcr? _reader;

    // Initialize the small local OCR engine before acquiring time-sensitive pixels.
    // No screen capture, recognition job, network or model is started here.
    internal static async Task PrepareAsync(CancellationToken token)
    {
        await ReaderGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
            await (_reader ??= new PetDanceMenuOcr()).PrepareAsync(token).ConfigureAwait(false);
        }
        finally { ReaderGate.Release(); }
    }

    public static ModuleVisualRecognition Classify(ModuleVisualFrame frame, PetDanceMenuText text)
    {
        ModuleRecognition.ValidateFrame(frame);
        ArgumentNullException.ThrowIfNull(text);
        if (text.ImageSize != frame.Geometry) throw new InvalidDataException("Text geometry differs from its bound image.");
        ModuleVisualRecognition Result(string scene, string reason, string? controlId = null, RectangleF? bounds = null,
            bool strong = true) => new(frame, scene, controlId is not null && bounds is { } rectangle
                ? [new(controlId, rectangle)] : [], strong ? ["wizard101.ui"] : [], reason);
        var postgame = PetDancePostGameClassifier.Classify(text);
        switch (postgame.Scene)
        {
            case PetDanceScene.NativeIdle: return Result("NativeIdle", postgame.Reason);
            case PetDanceScene.MenuObserved: return Result("MenuObserved", postgame.Reason, "game-menu.idle", postgame.ActionBounds);
            case PetDanceScene.GameResult: return Result("GameResult", postgame.Reason, "pet-dance.next", postgame.ActionBounds);
            case PetDanceScene.SnackOffer: return Result("SnackOffer", postgame.Reason, "pet-dance.finish-without-snack", postgame.ActionBounds);
        }
        var entry = PetDanceEntryVision.Classify(text, frame.ImageSha256, frame.CapturedMs);
        if (entry.Scene is PetDanceScene.InstructionsUnselected or PetDanceScene.InstructionsReady)
        {
            var cancel = UniqueCancel(text);
            return Result("SigilMenu.dance", cancel is null ? "dance-menu-cancel-control-unverified" : "dance-menu-and-cancel-text",
                cancel is null ? null : "sigil.dance.cancel", cancel);
        }
        if (postgame.Scene == PetDanceScene.OutsideAfterGame)
            return Result("OutsideCandidate", "outside-prompt-needs-receipt-bound-transition", strong: false);
        return Result("Unknown", "no-supported-menu-recognition", strong: false);
    }

    public static Task<ModuleVisualRecognition> ReadPngAsync(byte[] encodedPng, ModuleVisualFrame frame, CancellationToken token) =>
        ReadPngCoreAsync(encodedPng, frame, token, ReadTextAsync, "local-windows-ocr");

    internal static Task<ModuleVisualRecognition> ReadPngAsync(byte[] encodedPng, ModuleVisualFrame frame,
        CancellationToken token, Func<Bitmap, CancellationToken, Task<PetDanceMenuText>> readText) =>
        ReadPngCoreAsync(encodedPng, frame, token, readText, "injected-test-ocr");

    private static async Task<ModuleVisualRecognition> ReadPngCoreAsync(byte[] encodedPng, ModuleVisualFrame frame,
        CancellationToken token, Func<Bitmap, CancellationToken, Task<PetDanceMenuText>> readText, string origin)
    {
        ArgumentNullException.ThrowIfNull(encodedPng);
        ArgumentNullException.ThrowIfNull(readText);
        ModuleRecognition.ValidateFrame(frame);
        token.ThrowIfCancellationRequested();
        if (encodedPng.Length is < 33 or > MaximumPngBytes) throw new InvalidDataException("Encoded frame exceeds PNG limits.");
        // Validate and decode the same private snapshot; callers cannot replace
        // the pixels while the asynchronous reader still borrows them.
        var snapshot = encodedPng.ToArray();
        var digest = Convert.ToHexString(SHA256.HashData(snapshot)).ToLowerInvariant();
        if (!digest.Equals(frame.ImageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Encoded image does not match its frame hash.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(ReadTimeoutMs);
        var read = await PetDanceEntryImageReview.ReadSnapshotAsync(snapshot, (image, cancellation) =>
        {
            if (image.Size != frame.Geometry) throw new InvalidDataException("Decoded geometry does not match its frame.");
            return readText(image, cancellation);
        }, deadline.Token).ConfigureAwait(false);
        using var image = read.Image;
        deadline.Token.ThrowIfCancellationRequested();
        return Classify(frame with { ImageSha256 = digest, TextOrigin = origin }, read.Text);
    }

    private static async Task<PetDanceMenuText> ReadTextAsync(Bitmap image, CancellationToken token)
    {
        // One local engine and one outstanding OCR job, including a reader that
        // is still unwinding after cancellation. This is not an input scheduler.
        await ReaderGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
            return await (_reader ??= new PetDanceMenuOcr()).ReadAsync(image, token).ConfigureAwait(false);
        }
        finally { ReaderGate.Release(); }
    }

    private static RectangleF? UniqueCancel(PetDanceMenuText text)
    {
        var region = new RectangleF(text.ImageSize.Width * .15f, text.ImageSize.Height * .5f,
            text.ImageSize.Width * .55f, text.ImageSize.Height * .45f);
        var controls = new List<RectangleF>();
        foreach (var word in text.Words)
        {
            var normalized = new string(word.Text.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
            var bounds = word.Bounds;
            if (normalized != "CANCEL" || !region.Contains(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2)) continue;
            if (controls.Any(existing =>
            {
                var overlap = RectangleF.Intersect(existing, bounds);
                return overlap.Width > 0 && overlap.Height > 0 && overlap.Width * overlap.Height >=
                    Math.Min(existing.Width * existing.Height, bounds.Width * bounds.Height) * .6f;
            })) continue;
            controls.Add(bounds);
        }
        return controls.Count == 1 ? controls[0] : null;
    }
}
