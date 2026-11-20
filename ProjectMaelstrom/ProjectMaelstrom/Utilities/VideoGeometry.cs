namespace ProjectMaelstrom.Utilities;

/// <summary>H.264 needs even output dimensions; retain the complete source surface.
/// Output dimensions differ by at most one pixel per dimension. These dimensions
/// do not establish the encoder's exact interior pixel mapping.</summary>
internal static class VideoGeometry
{
    internal static Size EncodedSize(int width, int height)
    {
        WindowObserver.CheckSize(width, height);
        return new((width + 1) & ~1, (height + 1) & ~1);
    }

    internal static bool IsRateConverted(VideoFileInspection video, IReadOnlyList<double> submitted, bool allow)
    {
        if (video.Frames == submitted.Count) return false;
        if (!allow) video.EnsureMatchesSubmission(submitted.Count);
        _ = VideoTimingSummary.Create(submitted);
        if (submitted.Count < 2 || video.Frames < submitted.Count ||
            !double.IsFinite(video.DurationSeconds) ||
            Math.Abs(video.DurationSeconds * 1000 - (submitted[^1] - submitted[0])) > 150)
            throw new InvalidDataException("Rate-converted video does not cover the submitted interval within 150 ms.");
        return true;
    }
}
