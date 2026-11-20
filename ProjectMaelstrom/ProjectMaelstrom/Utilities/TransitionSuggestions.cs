namespace ProjectMaelstrom.Utilities;

/// <summary>Suggests human-taught labels from similar pairs of consecutive frames.</summary>
internal static class TransitionSuggestions
{
    internal const double MinimumTemporalCorrelation = .5;
    private sealed record Pair(int Index, string State, Rectangle? Region, byte[] Before, byte[] After);

    internal static double? DeltaCorrelation(byte[] before, byte[] after, byte[] taughtBefore, byte[] taughtAfter)
    {
        if (before.Length != after.Length || before.Length != taughtBefore.Length || before.Length != taughtAfter.Length)
            throw new ArgumentException("Transition feature dimensions must match.");
        double product = 0, candidateEnergy = 0, taughtEnergy = 0;
        for (var i = 0; i < before.Length; i++)
        {
            var candidate = after[i] - before[i];
            var taught = taughtAfter[i] - taughtBefore[i];
            product += candidate * taught;
            candidateEnergy += candidate * candidate;
            taughtEnergy += taught * taught;
        }
        if (candidateEnergy == 0 || taughtEnergy == 0) return null;
        return product / Math.Sqrt(candidateEnergy * taughtEnergy);
    }

    public static IReadOnlyList<LabelSuggestion> Suggest(TeachingRecording recording, CancellationToken token)
    {
        if (!recording.Complete) throw new InvalidOperationException("Stop recording before suggesting transitions.");
        if (GameplaySamplePlan.Load(recording.DirectoryPath)?.Purpose == "check")
            throw new InvalidOperationException("Independent check recordings must be labeled without suggestions.");
        var frames = recording.Frames;
        var features = new Dictionary<(int Index, Rectangle? Region), byte[]>();
        byte[] Feature(int index, Rectangle? region)
        {
            token.ThrowIfCancellationRequested();
            if (features.TryGetValue((index, region), out var cached)) return cached;
            using var bitmap = recording.ReadFrame(index);
            if (region is Rectangle area && (area.X < 0 || area.Y < 0 || area.Right > bitmap.Width || area.Bottom > bitmap.Height))
                throw new InvalidDataException("A taught region does not fit both frames of a transition.");
            using var crop = region.HasValue ? bitmap.Clone(region.Value, bitmap.PixelFormat) : null;
            var value = VisualStateRecognizer.Features(crop ?? bitmap);
            features[(index, region)] = value;
            return value;
        }
        var examples = new List<Pair>();
        var seenRegions = new HashSet<Rectangle?>();
        for (var i = 1; i < frames.Count; i++)
        {
            var interval = frames[i].TimeMs - frames[i - 1].TimeMs;
            if (frames[i].State.Length > 0 && interval > 0 && interval <= 2000 && frames[i].Width == frames[i - 1].Width && frames[i].Height == frames[i - 1].Height)
            {
                var r = frames[i].Region;
                Rectangle? region = r is { Length: 4 } ? new Rectangle(r[0], r[1], r[2], r[3]) : null;
                seenRegions.Add(region);
                if (seenRegions.Count > 8) throw new InvalidDataException("Use at most eight distinct taught regions in one transition comparison.");
                examples.Add(new(i, frames[i].State, region, Feature(i - 1, region), Feature(i, region)));
            }
        }
        if (examples.Select(e => e.State).Distinct(StringComparer.Ordinal).Count() < 2)
            throw new InvalidOperationException("Label at least two different transitions before asking for similar ones.");
        var conflicts = examples.GroupBy(e => (frames[e.Index - 1].Sha256, frames[e.Index].Sha256, e.Region));
        if (conflicts.Any(group => group.Select(e => e.State).Distinct(StringComparer.Ordinal).Count() > 1))
            throw new InvalidDataException("The same frame pair has conflicting human labels.");
        var groups = examples.GroupBy(e => e.State, StringComparer.Ordinal).Select(group => (State: group.Key, Examples: group.ToArray())).ToArray();
        var suggestions = new List<LabelSuggestion>();
        for (var i = 1; i < frames.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var interval = frames[i].TimeMs - frames[i - 1].TimeMs;
            if (frames[i].State.Length != 0 || interval <= 0 || interval > 2000 || frames[i - 1].Sha256 == frames[i].Sha256 || frames[i].Width != frames[i - 1].Width || frames[i].Height != frames[i - 1].Height) continue;
            var scores = groups.Select(group => (group.State, Score: group.Examples.Max(example =>
            {
                if (frames[i].Width != frames[example.Index].Width || frames[i].Height != frames[example.Index].Height) return 0;
                var before = Feature(i - 1, example.Region);
                var after = Feature(i, example.Region);
                var correlation = DeltaCorrelation(before, after, example.Before, example.After);
                if (!correlation.HasValue || correlation.Value < MinimumTemporalCorrelation) return 0;
                return Math.Min(VisualStateRecognizer.Similarity(before, example.Before), VisualStateRecognizer.Similarity(after, example.After));
            })))
                .OrderByDescending(item => item.Score).ToArray();
            var best = scores[0];
            var margin = best.Score - scores[1].Score;
            if (best.Score >= VisualStateRecognizer.MinimumSimilarity && margin >= VisualStateRecognizer.MinimumMargin)
                suggestions.Add(new(i, best.State, best.Score, margin));
        }
        return suggestions;
    }
}
