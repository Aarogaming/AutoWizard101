using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace ProjectMaelstrom.Utilities;

internal sealed record MotionSuggestion(string? Label, int DeltaX, int DeltaY, double Similarity, double Margin, string Reason, bool Reliable = false, bool ReverseChecked = false);
internal sealed record MotionTrackResult(IReadOnlyList<LabelSuggestion> Suggestions, int Compared, string StopReason);

/// <summary>Experimental screen-relative motion of a user-selected image region; never sends input.</summary>
internal static class MotionSuggestions
{
    // Initial conservative checks, not calibrated probabilities or a gameplay qualification.
    internal const double MinimumSimilarity = .68;
    internal const double MinimumMargin = .10;
    internal const double MinimumReverseFallbackMargin = .05;
    internal const double MinimumReverseFallbackSimilarity = .80;
    internal const double MaximumReverseErrorPixels = 2;
    internal const double MinimumDiagonalRatio = .75;
    internal const int MinimumMovementPixels = 6;

    public static MotionTrackResult Track(TeachingRecording recording, int start, Rectangle region, CancellationToken token, IProgress<int>? progress = null)
    {
        if (!recording.Complete) throw new InvalidOperationException("Stop recording before tracking motion.");
        if (GameplaySamplePlan.Load(recording.DirectoryPath)?.Purpose == "check")
            throw new InvalidOperationException("Independent check recordings must be labeled without suggestions.");
        var frames = recording.Frames;
        if (start < 0 || start >= frames.Count - 1) throw new InvalidDataException("Choose a frame with at least one later frame.");
        var suggestions = new List<LabelSuggestion>();
        var compared = 0;
        Bitmap earlier = recording.ReadFrame(start);
        try
        {
            for (var index = start + 1; index < frames.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var interval = frames[index].TimeMs - frames[index - 1].TimeMs;
                if (interval <= 0 || interval > 2000) return new(suggestions, compared, "Tracking stopped because adjacent samples are too far apart.");
                var later = recording.ReadFrame(index);
                try
                {
                    var result = Compare(earlier, later, region);
                    compared++;
                    progress?.Report(compared);
                    if (!result.Reliable)
                        return new(suggestions, compared, "Tracking stopped at a weak or ambiguous target match.");
                    region.Offset(result.DeltaX, result.DeltaY);
                    if (result.Label != null && string.IsNullOrWhiteSpace(frames[index].State))
                        suggestions.Add(new(index, result.Label, result.Similarity, result.Margin, result.ReverseChecked ? "Reverse-checked motion" : "Screen motion"));
                    earlier.Dispose();
                    earlier = later;
                    later = null!;
                }
                finally { later?.Dispose(); }
            }
            return new(suggestions, compared, "Reached the end of the recording.");
        }
        finally { earlier.Dispose(); }
    }

    public static MotionSuggestion Compare(Bitmap earlier, Bitmap later, Rectangle region)
    {
        if (earlier.Size != later.Size) throw new InvalidDataException("The two frames must have the same dimensions.");
        if (region.Width < 8 || region.Height < 8 || region.X < 0 || region.Y < 0 || region.Right > earlier.Width || region.Bottom > earlier.Height)
            throw new InvalidDataException("Select a target region at least 8 pixels wide and high, fully inside the earlier frame.");
        using var crop = earlier.Clone(region, earlier.PixelFormat);
        var features = VisualStateRecognizer.Features(crop);
        double largestDeviation = 0;
        for (var channel = 0; channel < 3; channel++)
        {
            var samples = Enumerable.Range(0, features.Length / 3).Select(i => (double)features[i * 3 + channel]).ToArray();
            var average = samples.Average();
            largestDeviation = Math.Max(largestDeviation, Math.Sqrt(samples.Average(value => (value - average) * (value - average))));
        }
        if (largestDeviation < 8) return new(null, 0, 0, 0, 0, "Selected region is too uniform to track reliably.");
        using var source = BitmapExtension.ToMat(later);
        using var template = BitmapExtension.ToMat(crop);
        var (bestLocation, best, margin) = Match(source, template, region.Size);
        var dx = bestLocation.X - region.X;
        var dy = bestLocation.Y - region.Y;
        var reliable = double.IsFinite(best) && double.IsFinite(margin) && best >= MinimumSimilarity && margin >= MinimumMargin;
        var reverseChecked = false;
        if (!reliable && double.IsFinite(best) && double.IsFinite(margin) && best >= MinimumReverseFallbackSimilarity && margin >= MinimumReverseFallbackMargin)
        {
            using var candidate = later.Clone(new Rectangle(bestLocation, region.Size), later.PixelFormat);
            using var reverseSource = BitmapExtension.ToMat(earlier);
            using var reverseTemplate = BitmapExtension.ToMat(candidate);
            var (reverseLocation, reverseBest, reverseMargin) = Match(reverseSource, reverseTemplate, region.Size);
            var reverseError = Math.Sqrt(Math.Pow(reverseLocation.X - region.X, 2) + Math.Pow(reverseLocation.Y - region.Y, 2));
            reliable = double.IsFinite(reverseBest) && double.IsFinite(reverseMargin) && reverseBest >= MinimumReverseFallbackSimilarity && reverseMargin >= MinimumMargin && reverseError <= MaximumReverseErrorPixels;
            reverseChecked = reliable;
        }
        if (!reliable)
            return new(null, dx, dy, best, margin, "The target match is weak or ambiguous; no motion label was suggested.");
        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < MinimumMovementPixels)
            return new(null, dx, dy, best, margin, "No clear screen-relative movement was detected.", true);
        var horizontal = dx < 0 ? "left" : "right";
        var vertical = dy < 0 ? "up" : "down";
        var larger = Math.Max(Math.Abs(dx), Math.Abs(dy));
        var smaller = Math.Min(Math.Abs(dx), Math.Abs(dy));
        var direction = smaller >= MinimumMovementPixels && (double)smaller / larger >= MinimumDiagonalRatio
            ? vertical + "-" + horizontal
            : Math.Abs(dx) > Math.Abs(dy) ? horizontal : vertical;
        return new("screen-motion-" + direction, dx, dy, best, margin, reverseChecked ? "A reverse match supported this screen-relative direction; verify the target and interpret its action manually." : "Screen-relative motion of the selected region; verify the target and interpret its action manually.", true, reverseChecked);
    }

    private static (Point Location, double Best, double Margin) Match(Mat source, Mat template, Size size)
    {
        using var scores = new Mat();
        CvInvoke.MatchTemplate(source, template, scores, TemplateMatchingType.CcoeffNormed);
        double min = 0, best = 0;
        Point minLocation = default, bestLocation = default;
        CvInvoke.MinMaxLoc(scores, ref min, ref best, ref minLocation, ref bestLocation);
        var left = Math.Max(0, bestLocation.X - size.Width / 2);
        var top = Math.Max(0, bestLocation.Y - size.Height / 2);
        var right = Math.Min(scores.Width, bestLocation.X + size.Width / 2 + 1);
        var bottom = Math.Min(scores.Height, bestLocation.Y + size.Height / 2 + 1);
        using (var exclusion = new Mat(scores, new Rectangle(left, top, right - left, bottom - top))) exclusion.SetTo(new MCvScalar(-1));
        double second = 0;
        Point secondLocation = default;
        CvInvoke.MinMaxLoc(scores, ref min, ref second, ref minLocation, ref secondLocation);
        return (bestLocation, best, best - second);
    }
}
