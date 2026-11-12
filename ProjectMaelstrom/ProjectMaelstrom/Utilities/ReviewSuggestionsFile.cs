using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record SavedReviewSuggestion(int Index, string FrameSha256, string State);
internal sealed record SavedReviewSuggestions(int Version, string Source, List<SavedReviewSuggestion> Suggestions);

/// <summary>Loads optional, review-only suggestions bound to exact recorded frames.</summary>
internal static class ReviewSuggestionsFile
{
    internal const string FileName = "review-suggestions.json";

    public static IReadOnlyList<LabelSuggestion> Load(TeachingRecording recording)
    {
        if (GameplaySamplePlan.Load(recording.DirectoryPath)?.Purpose == "check") return Array.Empty<LabelSuggestion>();
        var path = Path.Combine(recording.DirectoryPath, FileName);
        if (!File.Exists(path)) return Array.Empty<LabelSuggestion>();
        if (!recording.Complete) throw new InvalidOperationException("Stop recording before loading suggestions.");
        if (new FileInfo(path).Length > 256 * 1024) throw new InvalidDataException("Suggestion file is too large.");
        SavedReviewSuggestions data;
        try { data = JsonSerializer.Deserialize<SavedReviewSuggestions>(File.ReadAllText(path)) ?? throw new InvalidDataException("Suggestion file is empty."); }
        catch (JsonException ex) { throw new InvalidDataException("Suggestion file is invalid.", ex); }
        if (data.Version != 1 || string.IsNullOrWhiteSpace(data.Source) || data.Source.Length > 80 || data.Source.Any(char.IsControl) || data.Suggestions == null || data.Suggestions.Count > TeachingRecording.MaximumFrames)
            throw new InvalidDataException("Suggestion file has invalid metadata.");
        var frames = recording.Frames;
        var seen = new HashSet<int>();
        var result = new List<LabelSuggestion>();
        foreach (var item in data.Suggestions)
        {
            if (item == null || item.Index < 0 || item.Index >= frames.Count || !seen.Add(item.Index) || string.IsNullOrWhiteSpace(item.State) || item.State.Length > 120 || item.State.Any(char.IsControl) || !string.Equals(item.FrameSha256, frames[item.Index].Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Suggestion file does not match this recording.");
            if (string.IsNullOrWhiteSpace(frames[item.Index].State)) result.Add(new(item.Index, item.State.Trim(), double.NaN, 0, data.Source));
        }
        return result;
    }
}
