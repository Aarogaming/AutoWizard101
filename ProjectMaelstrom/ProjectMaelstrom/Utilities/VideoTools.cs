namespace ProjectMaelstrom.Utilities;

/// <summary>Find a private encoder first, then a user's existing installation.</summary>
internal static class VideoTools
{
    public static string? FindFfmpeg(string applicationDirectory, string? searchPath)
    {
        var directories = new List<string> { Path.Combine(applicationDirectory, "runtime", "ffmpeg") };
        if (searchPath != null) directories.AddRange(searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        foreach (var entry in directories)
        {
            try
            {
                var directory = entry.Trim('"');
                if (!Path.IsPathFullyQualified(directory)) continue;
                var encoder = Path.Combine(directory, "ffmpeg.exe");
                if (File.Exists(encoder) && File.Exists(Path.Combine(directory, "ffprobe.exe"))) return encoder;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
        }
        return null;
    }
}
