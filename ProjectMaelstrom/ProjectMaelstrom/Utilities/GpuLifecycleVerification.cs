using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Exercises stop, target loss, and resize against an owned test window.</summary>
internal static class GpuLifecycleVerification
{
    public static void Run(string directory, string mode)
    {
        if (mode is not ("stop" or "loss" or "resize" or "warmstop")) throw new ArgumentOutOfRangeException(nameof(mode));
        using var target = new Form { Text = "Maelstrom GPU lifecycle test", ClientSize = new Size(640, 360), BackColor = Color.Blue };
        using var animation = new System.Windows.Forms.Timer { Interval = 8 };
        using var stop = new CancellationTokenSource();
        var frame = 0;
        animation.Tick += (_, _) => target.BackColor = Color.FromArgb((frame++ * 7) % 256, 100, 200);
        target.Shown += async (_, _) =>
        {
            Task? recording = null;
            try
            {
                ShowWindow(target.Handle, 4);
                animation.Start();
                await Task.Delay(300);
                var identity = GameWindows.Read(target.Handle) ?? throw new InvalidOperationException("Test window unavailable.");
                var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                recording = Task.Run(() => GpuVideoRecording.RunAsync(identity, directory, TimeSpan.FromSeconds(30), stop.Token, () => first.TrySetResult()));
                if (mode == "warmstop") await Task.Delay(100);
                else { await first.Task.WaitAsync(TimeSpan.FromSeconds(40)); await Task.Delay(1500); }
                if (mode is "stop" or "warmstop") stop.Cancel();
                else if (mode == "loss") target.Hide();
                else target.Width += 80;
                var failed = false;
                try { await recording.WaitAsync(TimeSpan.FromSeconds(40)); }
                catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException) { failed = true; }
                if ((mode == "stop" && failed) || (mode != "stop" && !failed)) throw new InvalidOperationException("Unexpected recording outcome for " + mode);
                using var result = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "timing.json")));
                if (result.RootElement.GetProperty("status").GetString() != (mode == "stop" ? "complete" : "failed")) throw new InvalidOperationException("Recording outcome was not reported correctly.");
                File.WriteAllText(directory + ".lifecycle.json", JsonSerializer.Serialize(new { passed = true, mode }));
            }
            catch (Exception ex)
            {
                stop.Cancel();
                File.WriteAllText(directory + ".lifecycle.json", JsonSerializer.Serialize(new { passed = false, mode, error = ex.ToString() }));
                Environment.ExitCode = 1;
            }
            finally
            {
                stop.Cancel();
                if (recording != null) { try { await recording.WaitAsync(TimeSpan.FromSeconds(40)); } catch { } }
                animation.Stop();
                target.Close();
            }
        };
        Application.Run(target);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);
}
