namespace ProjectMaelstrom.Utilities;

/// <summary>Static raw-frame diagnostics, not a sequence replay or live qualification.</summary>
internal static class PetDanceRawFrameReview
{
    public static void Run(string profile, string directory, string output)
    {
        if (File.Exists(output) || Directory.Exists(output)) throw new IOException("Choose a new diagnostic report.");
        var files = Directory.GetFiles(directory, "frame-*.png").OrderBy(p => p, StringComparer.Ordinal).Take(129).ToArray();
        if (files.Length is < 1 or > 128) throw new InvalidDataException("Review 1–128 saved raw frames.");
        var rows = new List<object>();
        foreach (var file in files)
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked images are not supported.");
            var bytes = GameExperiments.BoundedBytes(file, 8 * 1024 * 1024);
            using var stream = new MemoryStream(bytes, writable: false);
            using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            WindowObserver.CheckSize(source.Width, source.Height);
            using var image = new Bitmap(source);
            using var vision = new PetDancePanelVision(profile);
            var analysis = System.Diagnostics.Stopwatch.StartNew();
            using var panel = vision.Extract(image);
            // Each independent static image starts a new reader. No invented
            // sequence timing or success latch is used in this diagnostic.
            var observation = vision.Read(panel, 0);
            rows.Add(new
            {
                file = Path.GetFileName(file),
                sha256 = GameExperiments.Digest(file),
                phase = observation.Phase.ToString(),
                cue = observation.Cue.ToString(),
                observation.Error,
                observation.Margin,
                observation.PhaseError,
                observation.Opacity,
                observation.CueOffsetX,
                observation.CueOffsetY,
                observation.CueAlignmentQualified,
                observation.Layout,
                observation.ArrowShape,
                analysis_ms = analysis.Elapsed.TotalMilliseconds
            });
            vision.VerifyInputsUnchanged();
        }
        GameExperiments.WriteAtomic(output, new
        {
            kind = "static-raw-frame-profile-review",
            profile_sha256 = GameExperiments.Digest(profile),
            frames = rows,
            sequence_replayed = false,
            live_qualified = false,
            inputs_sent = 0
        });
    }
}
