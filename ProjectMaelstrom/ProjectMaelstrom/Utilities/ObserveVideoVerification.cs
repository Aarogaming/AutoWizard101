using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Exercises the workspace controls against an owned animated test window.</summary>
internal static class ObserveVideoVerification
{
    public static void Run(string directory, bool checkPrioritizedPause = false, bool checkStaticTarget = false, bool checkStaticVideo = false, bool initialChange = true, bool checkInputCapture = false, bool requireTestInput = false)
    {
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Choose a new verification directory.");
        Directory.CreateDirectory(directory);
        using var target = new Form { Text = "Maelstrom video workspace verification", ClientSize = new Size(640, 360), BackColor = Color.Blue };
        using var animation = new System.Windows.Forms.Timer { Interval = 8 };
        var frame = 0;
        animation.Tick += (_, _) => target.BackColor = Color.FromArgb((frame++ * 7) % 256, 100, 200);
        target.Shown += async (_, _) =>
        {
            try
            {
                ShowWindow(target.Handle, 4);
                if (!checkStaticTarget && !checkStaticVideo) animation.Start();
                await Task.Delay(300);
                var identity = GameWindows.Read(target.Handle) ?? throw new InvalidOperationException("Test window unavailable.");
                using var workspace = new GameWorkspaceForm(dataRoot: Path.Combine(directory, "data"));
                workspace.StartPosition = FormStartPosition.Manual;
                workspace.Location = new Point(-30000, -30000);
                workspace.Show();
                if (checkStaticTarget)
                {
                    await workspace.VerifyStaticTargetAsync(identity);
                    File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { passed = true, static_target_remains_attached = true }));
                    workspace.Close();
                    return;
                }
                if (checkPrioritizedPause)
                {
                    await workspace.VerifyPrioritizedPauseAsync(identity);
                    File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { passed = true, prioritized_pause_stays_paused = true }));
                    workspace.Close();
                    return;
                }
                if (checkStaticVideo)
                {
                    using var changeUntilFirstFrame = new System.Windows.Forms.Timer { Interval = 200 };
                    var toggle = false;
                    changeUntilFirstFrame.Tick += (_, _) =>
                    {
                        if (workspace.HasVideoFrame) { changeUntilFirstFrame.Stop(); return; }
                        target.BackColor = (toggle = !toggle) ? Color.Blue : Color.Green;
                    };
                    if (initialChange) changeUntilFirstFrame.Start();
                    var staticVideo = await workspace.VerifyVideoWorkflowAsync(identity, 8000);
                    changeUntilFirstFrame.Stop();
                    using var staticReport = JsonDocument.Parse(File.ReadAllText(Path.Combine(staticVideo, "timing.json")));
                    var missing = staticReport.RootElement.GetProperty("recording_coverage").GetProperty("UnrepresentedTailMs").GetDouble();
                    var terminal = staticReport.RootElement.GetProperty("synthetic_terminal_frame").GetBoolean();
                    if (missing > 250 || !terminal || workspace.ReportsMissingVideoTail)
                        throw new InvalidOperationException("Static-video check did not preserve the final still image through the stop time.");
                    File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { passed = true, unrepresented_tail_ms = missing, synthetic_terminal_frame = terminal }));
                    workspace.Close();
                    return;
                }
                var videoDirectory = await workspace.VerifyVideoWorkflowAsync(identity, requireTestInput ? 20000 : 2500,
                    onRecordingReady: requireTestInput ? () => File.WriteAllText(Path.Combine(directory, "recording-ready.txt"), target.Handle.ToInt64().ToString()) : null,
                    recordInput: checkInputCapture);
                using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(videoDirectory, "timing.json")));
                if (report.RootElement.GetProperty("status").GetString() != "complete" || !File.Exists(Path.Combine(videoDirectory, "capture.mp4")))
                    throw new InvalidOperationException("The workspace did not produce a verified video.");
                if (checkInputCapture)
                {
                    using var input = JsonDocument.Parse(File.ReadAllText(Path.Combine(videoDirectory, "input-events.json")));
                    var root = input.RootElement;
                    var video = Path.Combine(videoDirectory, "capture.mp4");
                    using var stream = File.OpenRead(video);
                    var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
                    var origin = report.RootElement.GetProperty("capture_timestamp_ms")[0].GetDouble();
                    if (!root.GetProperty("VideoVerified").GetBoolean() || root.GetProperty("VideoSha256").GetString() != hash ||
                        Math.Abs(root.GetProperty("VideoCaptureOriginMs").GetDouble() - origin) > 0.001)
                        throw new InvalidOperationException("Input observations do not match the video or capture clock.");
                    var inputs = root.GetProperty("Events").EnumerateArray().ToArray();
                    if (requireTestInput && (!inputs.Any(e => e.GetProperty("Type").GetString() == "key_down" && e.GetProperty("Control").GetString() == "W") ||
                        !inputs.Any(e => e.GetProperty("Type").GetString() == "key_up" && e.GetProperty("Control").GetString() == "W")))
                        throw new InvalidOperationException("The expected controlled W key transitions were not recorded.");
                    File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { passed = true, video_directory = videoDirectory, hooks_installed = true, video_hash_matches = true, first_frame_clock_matches = true, test_key_observed = requireTestInput, note = requireTestInput ? "Controlled test input; not a human gameplay demonstration." : "No human input transitions were verified by this test." }));
                    workspace.Close();
                    return;
                }
                var ffmpeg = VideoTools.FindFfmpeg(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH")) ?? throw new InvalidOperationException("FFmpeg is unavailable.");
                var dataRoot = Path.Combine(directory, "data");
                var review = await VideoTeachingImport.RunAsync(dataRoot, "Verification", Path.Combine(videoDirectory, "capture.mp4"), ffmpeg, CancellationToken.None, framesPerSecond: 5);
                if (review.Frames.Count < 5 || !review.Complete) throw new InvalidOperationException("Video sampling did not produce a reviewable recording at the requested rate.");
                if (Math.Abs(review.Frames[1].TimeMs - 200) > .01) throw new InvalidOperationException("Video sample timing did not match the requested rate.");
                review.Label(0, "test-window");
                var reopened = TeachingRecording.Open(Path.Combine(review.DirectoryPath, "recording.json"));
                var pack = reopened.ExportPack(dataRoot, "video-check", "test-game", "Video capture check");
                File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { passed = true, video_directory = videoDirectory, review_directory = review.DirectoryPath, review_frames = review.Frames.Count, pack }));
                workspace.Close();
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString() }));
                Environment.ExitCode = 1;
            }
            finally { animation.Stop(); target.Close(); }
        };
        Application.Run(target);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);
}
