using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Checks Observe and review sampling against one explicitly identified external window.</summary>
internal static class ExternalObserveVerification
{
    public static string ExportReviewed(string recordingPath, string dataRoot, int firstIndex, string firstLabel, int secondIndex, string secondLabel)
    {
        firstLabel = firstLabel.Trim();
        secondLabel = secondLabel.Trim();
        if (firstIndex == secondIndex || string.IsNullOrWhiteSpace(firstLabel) || string.IsNullOrWhiteSpace(secondLabel) || firstLabel == secondLabel ||
            firstLabel.Length > 120 || secondLabel.Length > 120 || firstLabel.Any(char.IsControl) || secondLabel.Any(char.IsControl))
            throw new ArgumentException("Choose two different reviewed frames and labels.");
        var recording = TeachingRecording.Open(recordingPath);
        if (GameplaySamplePlan.Load(recording.DirectoryPath)?.Purpose == "check")
            throw new InvalidOperationException("Independent check recordings cannot be exported as teaching examples.");
        if (!recording.Complete || firstIndex < 0 || secondIndex < 0 || firstIndex >= recording.Frames.Count || secondIndex >= recording.Frames.Count)
            throw new ArgumentException("Choose two frames in a completed recording.");
        if (recording.Frames[firstIndex].State.Length != 0 || recording.Frames[secondIndex].State.Length != 0)
            throw new InvalidOperationException("Do not replace existing labels in a verification recording.");
        recording.Label(firstIndex, firstLabel);
        recording.Label(secondIndex, secondLabel);
        var pack = recording.ExportPack(dataRoot, "dodge-observe-check", "dodge-the-creeps", "Dodge reviewed states");
        var model = VisualStateRecognizer.Learn(Path.Combine(pack, "pack.json"));
        if (model.StateCount != 2) throw new InvalidDataException("The exported pack did not retain both labels.");
        return pack;
    }

    public static void Run(nint handle, string directory, int seconds, bool prioritizeVideo = false, bool recordInput = false)
    {
        if (seconds is < 2 or > 90) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Choose a new verification directory.");
        var target = GameWindows.Read(handle) ?? throw new InvalidOperationException("The selected window is not available.");
        Directory.CreateDirectory(directory);
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) };
        host.Shown += async (_, _) =>
        {
            try
            {
                var dataRoot = Path.Combine(directory, "data");
                using var workspace = new GameWorkspaceForm(dataRoot: dataRoot) { StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) };
                workspace.Show();
                var videoDirectory = await workspace.VerifyVideoWorkflowAsync(target, seconds * 1000, prioritizeVideo,
                    () => File.WriteAllText(Path.Combine(directory, "recording-ready.txt"), "The selected window is recording."), recordInput);
                if (recordInput)
                {
                    var inputPath = Path.Combine(videoDirectory, "input-events.json");
                    using var input = JsonDocument.Parse(File.ReadAllText(inputPath));
                    if (!input.RootElement.GetProperty("VideoVerified").GetBoolean()) throw new InvalidDataException("Input recording is not bound to a verified video.");
                    var events = input.RootElement.GetProperty("Events").EnumerateArray().ToArray();
                    var downs = events.Where(e => e.GetProperty("Type").GetString() == "key_down").Select(e => e.GetProperty("Control").GetString()).ToHashSet();
                    if (!events.Any(e => e.GetProperty("Type").GetString() == "key_up" && downs.Contains(e.GetProperty("Control").GetString())))
                        throw new InvalidDataException("No complete game-key press was recorded. Focus the selected game and play during the recording; the saved video and input log are available for inspection.");
                }
                var ffmpeg = VideoTools.FindFfmpeg(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH")) ?? throw new InvalidOperationException("FFmpeg is unavailable.");
                var review = await VideoTeachingImport.RunAsync(dataRoot, target.Title, Path.Combine(videoDirectory, "capture.mp4"), ffmpeg, CancellationToken.None, 5);
                new GameplaySamplePlan("teaching", "Review the selected game's visible states", null, null).Save(review.DirectoryPath);
                File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new
                {
                    passed = true,
                    target = new { target.Title, target.ProcessName, target.ProcessId },
                    prioritize_video = prioritizeVideo,
                    input_recorded = recordInput,
                    video_directory = videoDirectory,
                    review_recording = Path.Combine(review.DirectoryPath, "recording.json"),
                    review_frames = review.Frames.Count,
                    note = "Frames are unlabeled; review before exporting a teaching pack. No game inputs were sent."
                }, new JsonSerializerOptions { WriteIndented = true }));
                workspace.Close();
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
