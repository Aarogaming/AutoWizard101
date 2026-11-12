using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Exercises reviewed suggestions and pack creation using isolated, generated frames.</summary>
internal static class TeachingReviewVerification
{
    public static void Run(string directory)
    {
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Choose a new verification directory.");
        Directory.CreateDirectory(directory);
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) };
        host.Shown += (_, _) =>
        {
            try
            {
                var data = Path.Combine(directory, "data");
                var recording = TeachingRecording.Start(data, "Dodge the Creeps (DEBUG)", "test-game");
                using var first = new Bitmap(64, 48);
                using var second = new Bitmap(64, 48);
                using (var graphics = Graphics.FromImage(first)) graphics.Clear(Color.DarkBlue);
                using (var graphics = Graphics.FromImage(second)) graphics.Clear(Color.OrangeRed);
                recording.AddImportedFrame(first, 0);
                recording.AddImportedFrame(second, 200);
                recording.Finish();
                var frames = recording.Frames;
                var suggestions = new SavedReviewSuggestions(1, "Verification review", new() { new(0, frames[0].Sha256, "menu"), new(1, frames[1].Sha256, "playing") });
                File.WriteAllText(Path.Combine(recording.DirectoryPath, ReviewSuggestionsFile.FileName), JsonSerializer.Serialize(suggestions));
                using var review = new TeachingReviewForm(recording, data, null) { StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) };
                review.Show();
                review.VerifyAcceptSuggestedLabel(0, "menu", "dodge-the-creeps");
                review.VerifyAcceptSuggestedLabel(1, "playing", "dodge-the-creeps");
                var pack = recording.ExportPack(data, "review-check", "dodge-the-creeps", "Reviewed test states");
                var model = VisualStateRecognizer.Learn(Path.Combine(pack, "pack.json"));
                if (model.StateCount != 2) throw new InvalidOperationException("The reviewed pack did not preserve both states.");
                review.Close();
                File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { passed = true, reviewed_labels = 2, learned_states = model.StateCount, pack }));
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString() }));
                Environment.ExitCode = 1;
            }
            finally { host.Close(); }
        };
        Application.Run(host);
    }
}
