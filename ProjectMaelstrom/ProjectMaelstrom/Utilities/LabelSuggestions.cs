namespace ProjectMaelstrom.Utilities;

internal sealed record LabelSuggestion(int Index, string State, double Similarity, double Margin, string? Source = null);

/// <summary>Proposes labels from human-labeled examples without changing the recording.</summary>
internal static class LabelSuggestions
{
    public static IReadOnlyList<LabelSuggestion> Suggest(TeachingRecording recording)
    {
        if (!recording.Complete) throw new InvalidOperationException("Stop recording before suggesting labels.");
        if (GameplaySamplePlan.Load(recording.DirectoryPath)?.Purpose == "check")
            throw new InvalidOperationException("Independent check recordings must be labeled without model suggestions.");
        var frames = recording.Frames;
        var examples = new List<VisualExample>();
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            if (string.IsNullOrWhiteSpace(frame.State)) continue;
            using var bitmap = recording.ReadFrame(i);
            Rectangle? region = frame.Region is { Length: 4 } r ? new Rectangle(r[0], r[1], r[2], r[3]) : null;
            using var crop = region.HasValue ? bitmap.Clone(region.Value, bitmap.PixelFormat) : null;
            examples.Add(new(frame.State, frame.Sha256, frame.Width, frame.Height, VisualStateRecognizer.Features(crop ?? bitmap), region));
        }
        var model = new VisualStateRecognizer("Recording examples", "not-a-pack", examples);
        var suggestions = new List<LabelSuggestion>();
        for (var i = 0; i < frames.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(frames[i].State)) continue;
            using var image = recording.ReadFrame(i);
            var result = model.Predict(image);
            if (result.State != null) suggestions.Add(new(i, result.State, result.Similarity, result.Margin));
        }
        return suggestions;
    }
}
