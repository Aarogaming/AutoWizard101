using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Checks that losing an owned target ends recording with a failed report.</summary>
internal static class NativeVideoLossVerification
{
    public static void Run(string directory)
    {
        using var target = new Form { Text = "Maelstrom target loss test", ClientSize = new Size(640, 360), BackColor = Color.Blue };
        using var animation = new System.Windows.Forms.Timer { Interval = 8 };
        var frame = 0;
        animation.Tick += (_, _) => target.BackColor = Color.FromArgb((frame++ * 7) % 256, 100, 200);
        target.Shown += async (_, _) =>
        {
            try
            {
                ShowWindow(target.Handle, 4);
                animation.Start();
                await Task.Delay(300);
                var identity = GameWindows.Read(target.Handle) ?? throw new InvalidOperationException("Test window unavailable.");
                var recording = Task.Run(() => NativeWindowVideoProbe.RunAsync(identity, directory, TimeSpan.FromSeconds(15)));
                await Task.Delay(2000);
                target.Hide();
                var failed = false;
                try { await recording.WaitAsync(TimeSpan.FromSeconds(40)); }
                catch (InvalidOperationException) { failed = true; }
                using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "timing.json")));
                if (!failed || report.RootElement.GetProperty("status").GetString() != "failed") throw new InvalidOperationException("Target loss was not reported as a recording failure.");
                File.WriteAllText(Path.Combine(directory, "loss-check.json"), "{\"passed\":true}");
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "loss-check.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString() }));
                Environment.ExitCode = 1;
            }
            finally { animation.Stop(); target.Close(); }
        };
        Application.Run(target);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);
}
