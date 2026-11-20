using System.Diagnostics;

namespace ProjectMaelstrom.Utilities;

/// <summary>Runs only a child test game created and owned by this verification process.</summary>
internal static class DodgeOwnedVerification
{
    public static async Task<string> RunAsync(string godotPath, string projectDirectory, string packPath, string outputRoot)
    {
        if (!File.Exists(godotPath) || !File.Exists(Path.Combine(projectDirectory, "project.godot"))) throw new FileNotFoundException("The isolated Dodge test game is missing.");
        var pack = DodgeLivePack.Load(packPath);
        if (pack.Context != null) throw new InvalidOperationException("Context-aware Dodge packs currently require the isolated visual test.");
        var info = new ProcessStartInfo(godotPath) { UseShellExecute = false, WorkingDirectory = projectDirectory };
        info.ArgumentList.Add("--path");
        info.ArgumentList.Add(projectDirectory);
        info.ArgumentList.Add("--audio-driver");
        info.ArgumentList.Add("Dummy");
        using var job = new OwnedProcessJob();
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start the isolated Dodge game.");
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (System.ComponentModel.Win32Exception) { }
        try { job.Attach(process); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var timer = Stopwatch.StartNew();
        GameWindow? target = null;
        while (timer.ElapsedMilliseconds < 5000 && !process.HasExited)
        {
            process.Refresh();
            if (process.MainWindowHandle != 0) target = GameWindows.Read(process.MainWindowHandle);
            if (target?.ProcessId == process.Id) break;
            await Task.Delay(100).ConfigureAwait(false);
        }
        if (target == null || target.ProcessId != process.Id) throw new InvalidOperationException("The owned Dodge test window did not appear.");
        await Task.Delay(500).ConfigureAwait(false);
        await WindowsDodgeKeySink.TryFocusAsync(target).ConfigureAwait(false);
        var sink = new WindowsDodgeKeySink();
        var size = GameWindows.ClientSize(target);
        if (!sink.Ready(target, size)) throw new InvalidOperationException("Windows did not focus the owned Dodge test. No inputs were sent.");
        var marker = Path.Combine(projectDirectory, "__maelstrom_start_test.signal");
        using (File.Open(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        await using var observer = new WindowObserver(target);
        var path = await DodgeLiveRun.RunAsync(observer, pack, outputRoot, () => true, CancellationToken.None, seconds: 3, actionLimit: 1).ConfigureAwait(false);
        process.Refresh();
        Console.WriteLine($"Report: {path}");
        Console.WriteLine($"Owned game CPU time: {process.TotalProcessorTime.TotalSeconds:F2}s; peak memory: {process.PeakWorkingSet64 / (1024 * 1024)} MiB.");
        return path;
    }
}
