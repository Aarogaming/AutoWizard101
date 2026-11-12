namespace ProjectMaelstrom.Utilities;

internal sealed record FootageQuality(int Frames, int LabeledFrames, int UniqueImages, double? MedianSampleGapMs,
    bool ConsistentDimensions, bool ActionTrainingEligible, string[] Findings);

/// <summary>Conservative dataset checks, not a gameplay understanding or action-inference model.</summary>
internal static class FootageAssessment
{
    public static FootageQuality Verify(string assessmentPath)
    {
        using var json = System.Text.Json.JsonDocument.Parse(GameExperiments.BoundedBytes(assessmentPath, 2_000_000));
        var root = json.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1 || root.GetProperty("use").GetString() != "visual-examples-only" ||
            root.GetProperty("observed_inputs").GetBoolean() || root.GetProperty("verified_outcomes").GetBoolean()) throw new InvalidDataException("Unsupported footage evidence.");
        var folder = Path.GetDirectoryName(assessmentPath)!;
        var files = root.GetProperty("files");
        if (!files.TryGetProperty("recording.json", out _)) throw new InvalidDataException("Footage manifest is missing from the evidence list.");
        foreach (var file in files.EnumerateObject())
            if (GameExperiments.Digest(GameExperiments.Child(folder, file.Name)) != file.Value.GetString()) throw new InvalidDataException("Attached footage changed.");
        var recording = TeachingRecording.Open(Path.Combine(folder, "recording.json"));
        if (recording.Frames.Any(frame => !files.TryGetProperty(frame.File, out _))) throw new InvalidDataException("Footage frame is missing from its evidence list.");
        return Assess(recording.Frames);
    }

    public static FootageQuality Assess(IReadOnlyList<TeachingFrame> frames)
    {
        var gaps = frames.Zip(frames.Skip(1), (a, b) => b.TimeMs - a.TimeMs).Order().ToArray();
        var median = gaps.Length == 0 ? (double?)null : (gaps[(gaps.Length - 1) / 2] + gaps[gaps.Length / 2]) / 2;
        var unique = frames.Select(f => f.Sha256).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var labels = frames.Count(f => !string.IsNullOrWhiteSpace(f.State));
        var dimensions = frames.Select(f => (f.Width, f.Height)).Distinct().Count() == 1;
        var findings = new List<string> { "Video supplies visual observations. Keys, intent, rewards and successful outcomes are not verified by these images." };
        if (frames.Count < 2) findings.Add("Too few images for temporal examples.");
        if (gaps.Any(g => !double.IsFinite(g) || g <= 0)) findings.Add("Missing or non-increasing sample times: temporal training is unsuitable.");
        if (median > 100) findings.Add("Samples are more than 100 ms apart; fast actions may fall between samples.");
        if (frames.Count > 0 && unique < frames.Count / 2.0) findings.Add("Many exactly repeated images. Review for menus, pauses or held frames; similarity alone does not prove an error.");
        if (!dimensions) findings.Add("Frame dimensions change or are unavailable; separate and calibrate clips before matching.");
        if (frames.Any(f => f.Width < 320 || f.Height < 240)) findings.Add("Small images may hide important cues.");
        if (labels == 0) findings.Add("Review and label relevant states before exporting visual examples to a game pack.");
        findings.Add("Check cuts, overlays, playback speed and game viewport manually. These checks cannot certify perception or adaptability.");
        return new(frames.Count, labels, unique, median, dimensions, false, findings.ToArray());
    }

    public static string Attach(GameExperiments store, string experimentId, TeachingRecording recording, string sourceNote)
    {
        var plan = store.Load(experimentId);
        if (!recording.Complete || string.IsNullOrWhiteSpace(sourceNote) || sourceNote.Length > 2000) throw new InvalidDataException("Finish the recording and describe its source and intended use.");
        var manifest = Path.Combine(recording.DirectoryPath, "recording.json");
        var verified = TeachingRecording.Open(manifest);
        var files = verified.Frames.Select(f => f.File).Prepend("recording.json").ToList();
        if (File.Exists(Path.Combine(recording.DirectoryPath, "video-source.json"))) files.Add("video-source.json");
        if (files.Sum(f => new FileInfo(GameExperiments.Child(recording.DirectoryPath, f)).Length) > TeachingRecording.MaximumPackImageBytes)
            throw new InvalidDataException("This evidence exceeds 128 MiB. Import a shorter clip or fewer samples; the original is preserved.");
        var folder = Path.Combine(store.DirectoryPath(experimentId), "footage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var hashes = new Dictionary<string, string>();
        foreach (var file in files)
        {
            var source = GameExperiments.Child(recording.DirectoryPath, file);
            var hash = GameExperiments.Digest(source);
            var destination = Path.Combine(folder, file);
            File.Copy(source, destination);
            if (GameExperiments.Digest(destination) != hash) throw new IOException("Footage changed during attachment.");
            hashes[file] = hash;
        }
        var snapshot = TeachingRecording.Open(Path.Combine(folder, "recording.json"));
        var path = Path.Combine(folder, "assessment.json");
        GameExperiments.WriteNew(path, new
        {
            schema_version = 1,
            game_id = plan.GameId,
            created_utc = DateTime.UtcNow.ToString("O"),
            sourceNote,
            quality = Assess(snapshot.Frames),
            files = hashes,
            use = "visual-examples-only",
            observed_inputs = false,
            verified_outcomes = false
        });
        return path;
    }
}
