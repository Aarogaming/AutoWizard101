using System.Text;

namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceLevelControl(PetDanceArena Arena, RectangleF Bounds, bool IdentityVerified = false);

/// <summary>Supplemental measured visual facts, never inferred from a tooltip or OCR text.
/// The caller must qualify the detector/annotation and bind it to exactly this image.
/// Null/false means unverified, not absent. No coordinates or default arena are supplied.</summary>
internal sealed record PetDanceEntryEvidence(string ImageSha256, double CapturedMs, Size Geometry,
    IReadOnlyList<PetDanceLevelControl>? LevelControls = null, PetDanceArena? SelectedArena = null,
    bool SelectionHighlightVerified = false, bool PlayEnabledVerified = false,
    RectangleF? VerifiedSigilTitleBounds = null, bool LoadingVerified = false);

internal sealed record PetDanceEntryScene(PetDanceScene Scene, string ImageSha256, double CapturedMs,
    Size Geometry, string Reason, RectangleF? PlayBounds = null,
    PetDanceArena? SelectedArena = null, IReadOnlyList<PetDanceLevelControl>? LevelControls = null,
    bool SelectionVerified = false, bool PlayEnabledVerified = false);

/// <summary>Bounded entry recognition from existing menu OCR and independently verified
/// visual cues. Readable PLAY text alone never establishes enabled Play or level selection.
/// This class neither performs OCR nor accesses a window, image service, or input device.</summary>
internal static class PetDanceEntryVision
{
    internal static bool IsImageHash(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    public static PetDanceEntryScene Classify(PetDanceMenuText text, string imageSha256, double capturedMs,
        PetDanceEntryEvidence? evidence = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!IsImageHash(imageSha256) || !double.IsFinite(capturedMs) || capturedMs < 0)
            throw new ArgumentException("Bind recognition to an image hash and finite capture time.");
        PetDanceEntryScene Result(PetDanceScene scene, string reason, RectangleF? play = null,
            PetDanceArena? selected = null, IReadOnlyList<PetDanceLevelControl>? levels = null) =>
            new(scene, imageSha256, capturedMs, text.ImageSize, reason, play, selected, levels,
                SelectionVerified: scene == PetDanceScene.InstructionsReady,
                PlayEnabledVerified: scene == PetDanceScene.InstructionsReady);
        if (text.ImageSize.Width <= 0 || text.ImageSize.Height <= 0 || text.Words is null || text.Words.Count > 512 ||
            text.Words.Any(word => word is null || word.Text is null || word.Text.Length > 128 ||
                !PetDancePostGameClassifier.ValidBounds(word.Bounds, text.ImageSize)))
            return Result(PetDanceScene.Unknown, "invalid-entry-ocr");
        if (evidence != null && (evidence.ImageSha256 != imageSha256 || evidence.CapturedMs != capturedMs ||
            evidence.Geometry != text.ImageSize)) return Result(PetDanceScene.Unknown, "visual-evidence-frame-mismatch");

        var postgame = PetDancePostGameClassifier.Classify(text);
        // Idle can retain both a sigil and a readable Play prompt underneath.
        if (postgame.Scene == PetDanceScene.NativeIdle) return Result(PetDanceScene.NativeIdle, "native-idle-no-wake-action");
        if (postgame.Scene is PetDanceScene.GameResult or PetDanceScene.SnackOffer or PetDanceScene.MenuObserved)
            return Result(PetDanceScene.Unknown, "postgame-modal-is-not-entry");

        var words = Words(text.Words);
        RectangleF Region(float x, float y, float width, float height) =>
            new(x * text.ImageSize.Width, y * text.ImageSize.Height, width * text.ImageSize.Width, height * text.ImageSize.Height);
        var header = Phrase(words, "DANCE GAME", Region(.15f, .08f, .7f, .4f));
        var objective = Phrase(words, "OBJECTIVE", Region(.15f, .2f, .7f, .5f));
        var selection = Phrase(words, "SELECT LEVEL", Region(.15f, .35f, .7f, .55f));
        var play = Phrase(words, "PLAY", Region(.25f, .5f, .6f, .45f));
        var instructions = header is not null && objective is not null && selection is not null && play is not null &&
            header.Value.Bottom < selection.Value.Top && selection.Value.Bottom < play.Value.Top;
        if (evidence?.LoadingVerified == true)
            return Result(instructions || postgame.Scene == PetDanceScene.OutsideAfterGame
                ? PetDanceScene.Unknown : PetDanceScene.Loading,
                instructions || postgame.Scene == PetDanceScene.OutsideAfterGame ? "conflicting-loading-cue" : "verified-loading-cue");
        if (instructions)
        {
            var controls = evidence?.LevelControls?.ToArray() ?? [];
            if (controls.Length > 5 || controls.Any(control => control is null || !Enum.IsDefined(control.Arena) ||
                !PetDancePostGameClassifier.ValidBounds(control.Bounds, text.ImageSize) ||
                control.Bounds.Top < selection!.Value.Bottom || control.Bounds.Bottom >= play!.Value.Top) ||
                controls.Select(control => control.Arena).Distinct().Count() != controls.Length ||
                controls.Where((control, i) => controls.Skip(i + 1).Any(other => control.Bounds.IntersectsWith(other.Bounds))).Any())
                return Result(PetDanceScene.Unknown, "invalid-level-control-evidence");
            var verified = controls.Where(control => control.IdentityVerified).ToArray();
            var selected = evidence?.SelectedArena;
            var ready = selected is not null && Enum.IsDefined(selected.Value) &&
                evidence!.SelectionHighlightVerified && evidence.PlayEnabledVerified &&
                verified.Any(control => control.Arena == selected.Value);
            return ready
                ? Result(PetDanceScene.InstructionsReady, "selected-level-and-enabled-play-verified", play, selected, verified)
                : Result(PetDanceScene.InstructionsUnselected, "instructions-selection-or-enabled-play-unverified", play, levels: verified);
        }

        var title = evidence?.VerifiedSigilTitleBounds;
        if (postgame.Scene == PetDanceScene.OutsideAfterGame && title is { } bounds &&
            PetDancePostGameClassifier.ValidBounds(bounds, text.ImageSize) &&
            bounds.Top >= text.ImageSize.Height * .6 && bounds.Bottom <= text.ImageSize.Height * .9)
            return Result(PetDanceScene.DanceSigil, "verified-dance-title-and-interaction-prompt");
        // Loading artwork has no reliably recognized words in the saved fixture.
        // Empty OCR, a generic Press/Play prompt, or a hover tooltip remains unknown.
        return Result(PetDanceScene.Unknown, "entry-evidence-insufficient");
    }

    private static PetDanceMenuWord[] Words(IReadOnlyList<PetDanceMenuWord> source)
    {
        var result = new List<PetDanceMenuWord>();
        foreach (var word in source)
        {
            string normalized;
            try { normalized = new string(word.Text.Normalize(NormalizationForm.FormKC).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray()); }
            catch (ArgumentException) { continue; }
            if (normalized.Length == 0) continue;
            var candidate = word with { Text = normalized };
            if (result.Any(prior => prior.Text == candidate.Text && Overlap(prior.Bounds, candidate.Bounds))) continue;
            result.Add(candidate);
        }
        return result.ToArray();
    }

    private static bool Overlap(RectangleF a, RectangleF b)
    {
        var intersection = RectangleF.Intersect(a, b);
        return intersection.Width > 0 && intersection.Height > 0 && intersection.Width * intersection.Height >=
            Math.Min(a.Width * a.Height, b.Width * b.Height) * .6f;
    }

    private static RectangleF? Phrase(PetDanceMenuWord[] words, string phrase, RectangleF region)
    {
        var terms = phrase.Split(' ');
        var eligible = words.Where(word => region.Contains(word.Bounds.Left + word.Bounds.Width / 2,
            word.Bounds.Top + word.Bounds.Height / 2)).ToArray();
        var matches = new List<RectangleF>();
        foreach (var first in eligible.Where(word => word.Text == terms[0]))
        {
            var previous = first.Bounds; var combined = previous; var found = true;
            foreach (var term in terms.Skip(1))
            {
                var next = eligible.Where(word => word.Text == term && word.Bounds.Left >= previous.Right &&
                    word.Bounds.Left - previous.Right <= Math.Max(previous.Height, word.Bounds.Height) * 3.5f &&
                    Math.Abs(word.Bounds.Top + word.Bounds.Height / 2 - previous.Top - previous.Height / 2) <=
                        Math.Max(previous.Height, word.Bounds.Height) * .8f).ToArray();
                if (next.Length != 1) { found = false; break; }
                previous = next[0].Bounds; combined = RectangleF.Union(combined, previous);
            }
            if (found) matches.Add(combined);
        }
        return matches.Count == 1 ? matches[0] : null;
    }
}
