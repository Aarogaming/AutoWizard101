using System.Diagnostics;
using System.Reflection;

namespace ProjectMaelstrom.Utilities;

/// <summary>Warms the Windows encoder in an owned helper so its encoder resources are released on exit.</summary>
internal static class EncoderWarmupProcess
{
    public static async Task RunAsync(int width, int height, CancellationToken token)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the Maelstrom executable.");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach (var arg in new[] { "--warmup-native-encoder", width.ToString(System.Globalization.CultureInfo.InvariantCulture), height.ToString(System.Globalization.CultureInfo.InvariantCulture) }) info.ArgumentList.Add(arg);
        using var job = new OwnedProcessJob();
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Encoder warm-up did not start.");
        try { job.Attach(process); }
        catch { if (!process.HasExited) process.Kill(true); throw; }
        var errors = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(40), token); }
        catch { if (!process.HasExited) process.Kill(true); throw; }
        var error = await errors;
        if (process.ExitCode != 0) throw new InvalidOperationException("Encoder warm-up failed: " + error[..Math.Min(error.Length, 1000)]);
    }
}
