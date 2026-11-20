namespace ProjectMaelstrom.Utilities;

internal static class VideoProbeVerification
{
    public static void Run(string directory, bool native = false, bool hd = false, bool sustained = false, bool gpu = false, bool paced = false, bool warm = false, bool direct = false, int? seconds = null)
    {
        using var target = new Form { Text = "Maelstrom video test", ClientSize = hd ? new Size(1278, 688) : new Size(640, 360), BackColor = Color.Blue, StartPosition = FormStartPosition.CenterScreen };
        // Windows timer resolution can make a 16 ms interval tick at roughly 32 ms.
        using var update = new System.Windows.Forms.Timer { Interval = 8 };
        var paints = new List<double>();
        var active = false;
        target.Paint += (_, _) => { if (active) paints.Add(FrameTimeline.TimestampMs()); };
        var frame = 0;
        using var stopAnimation = new CancellationTokenSource();
        Task? animation = null;
        update.Tick += (_, _) => { target.BackColor = Color.FromArgb((frame++ * 7) % 256, 100, 200); };
        target.Shown += async (_, _) =>
        {
            try
            {
                ShowWindow(target.Handle, 4);
                await Task.Delay(300);
                var identity = GameWindows.Read(target.Handle) ?? throw new InvalidOperationException("Test window unavailable.");
                if (paced)
                {
                    animation = Task.Run(() =>
                    {
                        while (!stopAnimation.IsCancellationRequested)
                            target.Invoke(() => { target.BackColor = Color.FromArgb((frame++ * 7) % 256, 100, 200); target.Refresh(); DwmFlush(); });
                    });
                }
                else update.Start();
                active = true;
                if (gpu) await Task.Run(() => GpuSurfaceVideoProbe.RunAsync(identity, directory, TimeSpan.FromSeconds(seconds ?? (sustained ? 20 : 5)), warm, direct));
                else if (native) await Task.Run(() => NativeWindowVideoProbe.RunAsync(identity, directory, TimeSpan.FromSeconds(sustained ? 20 : 5)));
                else await Task.Run(() => WindowVideoProbe.RunAsync(identity, @"C:\ProgramData\chocolatey\lib\ffmpeg\tools\ffmpeg\bin\ffmpeg.exe", directory, TimeSpan.FromSeconds(5)));
                active = false;
                System.IO.File.WriteAllText(Path.Combine(directory, "source-timing.json"), System.Text.Json.JsonSerializer.Serialize(new { paint_count = paints.Count, paint_timestamp_ms = paints, paint_timing = VideoTimingSummary.Create(paints) }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                // Never overwrite diagnostics or recordings from an earlier run.
                Directory.CreateDirectory(directory);
                var error = Path.Combine(directory, "error-" + Guid.NewGuid().ToString("N") + ".txt");
                using var stream = new FileStream(error, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream);
                writer.Write(ex.ToString());
                Environment.ExitCode = 1;
            }
            finally
            {
                active = false;
                update.Stop();
                stopAnimation.Cancel();
                if (animation != null) { try { await animation; } catch { } }
                target.Close();
            }
        };
        Application.Run(target);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}
