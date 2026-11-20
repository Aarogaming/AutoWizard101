using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Runs the paced source in another owned process, like an external game.</summary>
internal static class SeparateCaptureVerification
{
    public static async Task RunAsync(string directory, int seconds, bool cover = false)
    {
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Choose a new verification directory.");
        Directory.CreateDirectory(directory);
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate Maelstrom.");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardError = true };
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        info.ArgumentList.Add("--capture-target-server");
        info.ArgumentList.Add(directory);
        using var job = new OwnedProcessJob();
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Test target did not start.");
        try { job.Attach(process); }
        catch { if (!process.HasExited) process.Kill(true); throw; }
        var errors = process.StandardError.ReadToEndAsync();
        var identityPath = Path.Combine(directory, "identity.json");
        try
        {
            for (var i = 0; i < 300 && !File.Exists(identityPath); i++)
            {
                if (process.HasExited) throw new InvalidOperationException("Test target exited: " + await errors);
                await Task.Delay(100);
            }
            if (!File.Exists(identityPath)) throw new TimeoutException("Test target did not open.");
            using var identity = JsonDocument.Parse(File.ReadAllText(identityPath));
            var handle = (nint)identity.RootElement.GetProperty("handle").GetInt64();
            var window = GameWindows.Read(handle) ?? throw new InvalidOperationException("Cannot identify test target window.");
            if (window.ProcessId != process.Id) throw new InvalidOperationException("Test target process identity changed.");
            var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var recording = GpuSurfaceVideoProbe.RunAsync(window, Path.Combine(directory, "recording"), TimeSpan.FromSeconds(seconds), warm: true, direct: true, onFirstFrame: () => first.TrySetResult());
            if (cover)
            {
                await first.Task.WaitAsync(TimeSpan.FromSeconds(40));
                File.WriteAllText(Path.Combine(directory, "cover.txt"), "cover");
                await Task.Delay(5000);
                File.Delete(Path.Combine(directory, "cover.txt"));
            }
            await recording;
        }
        finally
        {
            File.WriteAllText(Path.Combine(directory, "stop.txt"), "stop");
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
            catch { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
        }
        if (process.ExitCode != 0) throw new InvalidOperationException("Test target failed: " + await errors);
    }
}

internal static class CaptureTargetServer
{
    public static void Run(string directory)
    {
        using var target = new Form { Text = "Maelstrom independent capture target", ClientSize = new Size(1278, 688), BackColor = Color.Blue, StartPosition = FormStartPosition.CenterScreen };
        using var stop = new CancellationTokenSource();
        using var monitor = new System.Windows.Forms.Timer { Interval = 250 };
        var paints = new List<double>();
        var coverChanges = new List<object>();
        Form? cover = null;
        target.Paint += (_, _) => paints.Add(FrameTimeline.TimestampMs());
        var frame = 0;
        Task? animation = null;
        monitor.Tick += async (_, _) =>
        {
            var shouldCover = File.Exists(Path.Combine(directory, "cover.txt"));
            if (shouldCover && cover == null)
            {
                cover = new Form { Text = "Maelstrom occlusion test", Bounds = target.Bounds, BackColor = Color.Red, StartPosition = FormStartPosition.Manual, TopMost = true };
                cover.Show();
                coverChanges.Add(new { covered = true, timestamp_ms = FrameTimeline.TimestampMs() });
            }
            else if (!shouldCover && cover != null)
            {
                cover.Close();
                cover.Dispose();
                cover = null;
                coverChanges.Add(new { covered = false, timestamp_ms = FrameTimeline.TimestampMs() });
            }
            if (!File.Exists(Path.Combine(directory, "stop.txt"))) return;
            monitor.Stop();
            stop.Cancel();
            if (animation != null) { try { await animation; } catch { } }
            cover?.Close();
            cover?.Dispose();
            using (var file = new FileStream(Path.Combine(directory, "source-timing.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                JsonSerializer.Serialize(file, new { paint_count = paints.Count, paint_timestamp_ms = paints, paint_timing = VideoTimingSummary.Create(paints), cover_changes = coverChanges }, new JsonSerializerOptions { WriteIndented = true });
            target.Close();
        };
        target.Shown += (_, _) =>
        {
            ShowWindow(target.Handle, 4);
            var identityTemporary = Path.Combine(directory, "identity.json.tmp");
            using (var file = new FileStream(identityTemporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(file, new { handle = (long)target.Handle });
            File.Move(identityTemporary, Path.Combine(directory, "identity.json"));
            animation = Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                    target.Invoke(() => { target.BackColor = Color.FromArgb((frame++ * 7) % 256, 100, 200); target.Refresh(); DwmFlush(); });
            });
            monitor.Start();
        };
        Application.Run(target);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}
