namespace ProjectMaelstrom.Utilities;

internal sealed record VisionScreeningProgress(int Completed, int Total, int ModelRequests, int Reused, int Rejected);

/// <summary>Prescreens unlabeled images once per exact image and crop; never saves labels.</summary>
internal static class VisionScreening
{
    public static async Task<VisionScreeningProgress> RunAsync(
        TeachingRecording recording,
        Func<int, CancellationToken, Task<string>> infer,
        Action<LabelSuggestion> onSuggestion,
        Action<VisionScreeningProgress>? onProgress,
        CancellationToken token,
        int maxModelRequests = 30,
        int cooldownMilliseconds = 500)
    {
        if (maxModelRequests is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(maxModelRequests));
        if (cooldownMilliseconds is < 0 or > 60000) throw new ArgumentOutOfRangeException(nameof(cooldownMilliseconds));
        if (!recording.Complete) throw new InvalidOperationException("Stop recording before suggesting labels.");
        if (GameplaySamplePlan.Load(recording.DirectoryPath)?.Purpose == "check")
            throw new InvalidOperationException("Label independent checks without model suggestions.");
        var frames = recording.Frames;
        var pending = Enumerable.Range(0, frames.Count).Where(i => string.IsNullOrWhiteSpace(frames[i].State)).ToArray();
        var cache = new Dictionary<(string Hash, string Region), string?>();
        var completed = 0;
        var requests = 0;
        var reused = 0;
        var rejected = 0;
        foreach (var index in pending)
        {
            token.ThrowIfCancellationRequested();
            var frame = frames[index];
            var key = (frame.Sha256, frame.Region == null ? "full" : string.Join(",", frame.Region));
            string? state;
            var fromCache = cache.TryGetValue(key, out var existing);
            if (fromCache)
            {
                // Even a repeated image must still match the recording's verified evidence.
                using var verified = recording.ReadFrame(index);
                state = existing;
                reused++;
            }
            else
            {
                if (requests >= maxModelRequests) break;
                if (requests > 0 && cooldownMilliseconds > 0) await Task.Delay(cooldownMilliseconds, token);
                requests++;
                try { state = await infer(index, token); }
                catch (UnusableVisionSuggestionException) { state = null; }
                token.ThrowIfCancellationRequested();
                if (state != null && (string.IsNullOrWhiteSpace(state) || state.Length > 120 || state.Any(char.IsControl)))
                    state = null;
                cache.Add(key, state);
            }
            if (state == null) rejected++;
            else if (string.IsNullOrWhiteSpace(recording.Frames[index].State))
                onSuggestion(new(index, state, double.NaN, 0, fromCache ? "Local AI · same image" : "Local AI"));
            completed++;
            onProgress?.Invoke(new(completed, pending.Length, requests, reused, rejected));
        }
        return new(completed, pending.Length, requests, reused, rejected);
    }
}
