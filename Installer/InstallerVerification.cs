using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace Installer;

internal static class InstallerVerification
{
    public static void Run(string reportPath)
    {
        var id = Guid.NewGuid().ToString("N");
        var sandbox = Path.Combine(Path.GetTempPath(), "MaelstromInstallerVerification-" + id);
        var root = Path.Combine(sandbox, "application");
        var data = Path.Combine(sandbox, "userdata");
        var registry = @"Software\MaelstromInstallerTests\" + id;
        var start = Path.Combine(sandbox, "Shortcuts", "Start.lnk");
        var desktop = Path.Combine(sandbox, "Shortcuts", "Desktop.lnk");
        var engine = new InstallEngine(root, data, new WindowsRegistration(root, registry, start, desktop));
        Directory.CreateDirectory(Path.Combine(data, "game-packs"));
        Directory.CreateDirectory(Path.Combine(data, "reports"));
        Directory.CreateDirectory(Path.Combine(data, "recordings", "example"));
        Directory.CreateDirectory(Path.Combine(data, "video-recordings", "example"));
        Directory.CreateDirectory(Path.Combine(data, "experiments", "example", "decisions"));
        Directory.CreateDirectory(Path.Combine(data, "experiments", "example", "footage"));
        Directory.CreateDirectory(Path.Combine(data, "home-movement", "example"));
        Directory.CreateDirectory(Path.Combine(data, "routines", "wizard101"));
        Directory.CreateDirectory(Path.Combine(data, "operations", "example"));
        var operation = Path.Combine(data, "operations", "example", "receipt.json");
        File.WriteAllText(operation, "user operation receipt sentinel");
        var routine = Path.Combine(data, "routines", "wizard101", "keep.json");
        var idleSettings = Path.Combine(data, "auto-idle-settings.json");
        File.WriteAllText(routine, "user routine sentinel");
        File.WriteAllText(idleSettings, "user idle choices sentinel");
        var pack = Path.Combine(data, "game-packs", "keep.json");
        var report = Path.Combine(data, "reports", "keep.json");
        var recording = Path.Combine(data, "recordings", "example", "recording.json");
        var video = Path.Combine(data, "video-recordings", "example", "capture.mp4");
        var audio = Path.Combine(data, "video-recordings", "example", "game-audio.wav");
        var audioTiming = Path.Combine(data, "video-recordings", "example", "audio-timing.json");
        byte[] audioSentinel = [82, 73, 70, 70, 0, 1, 255, 128, 0, 42]; // Binary preservation fixture, not playable WAV.
        const string audioTimingSentinel = "{\"kind\":\"audio-timing-preservation-sentinel\",\"clock\":\"QPC100ns\"}";
        var experiment = Path.Combine(data, "experiments", "example", "plan.json");
        var decision = Path.Combine(data, "experiments", "example", "decisions", "keep.json");
        var footage = Path.Combine(data, "experiments", "example", "footage", "frame.png");
        var homeReport = Path.Combine(data, "home-movement", "example", "report.json");
        File.WriteAllText(pack, "user pack sentinel");
        File.WriteAllText(report, "user report sentinel");
        File.WriteAllText(recording, "user recording sentinel");
        File.WriteAllText(video, "user video sentinel");
        File.WriteAllBytes(audio, audioSentinel);
        File.WriteAllText(audioTiming, audioTimingSentinel);
        File.WriteAllText(experiment, "user experiment sentinel");
        File.WriteAllText(decision, "user decision sentinel");
        File.WriteAllText(footage, "user footage sentinel");
        File.WriteAllText(homeReport, "user home movement sentinel");
        void Kept()
        {
            if (File.ReadAllText(operation) != "user operation receipt sentinel") throw new IOException("Operation history was not preserved.");
            if (!File.ReadAllBytes(audio).SequenceEqual(audioSentinel) || File.ReadAllText(audioTiming) != audioTimingSentinel)
                throw new IOException("Audio recording and timing sidecars were not preserved.");
            if (File.ReadAllText(pack) != "user pack sentinel" || File.ReadAllText(report) != "user report sentinel" ||
                File.ReadAllText(recording) != "user recording sentinel" || File.ReadAllText(video) != "user video sentinel" ||
                File.ReadAllText(experiment) != "user experiment sentinel" || File.ReadAllText(decision) != "user decision sentinel" || File.ReadAllText(footage) != "user footage sentinel" ||
                File.ReadAllText(homeReport) != "user home movement sentinel" || File.ReadAllText(routine) != "user routine sentinel" || File.ReadAllText(idleSettings) != "user idle choices sentinel")
                throw new IOException("User data was not preserved.");
        }
        Stream Payload() => Assembly.GetExecutingAssembly().GetManifestResourceStream("Maelstrom.Payload.zip") ?? throw new IOException("No embedded payload.");
        try
        {
            using (var payload = Payload()) engine.Install(payload, Environment.ProcessPath!, "verify-1", true);
            if (!File.Exists(start) || !File.Exists(desktop)) throw new IOException("Missing installed shortcuts.");
            Kept();
            var first = engine.ReadInstalled()!;
            using (var payload = Payload()) engine.Install(payload, Environment.ProcessPath!, "verify-2", false);
            if (File.Exists(desktop) || engine.ReadInstalled()!.Version != "verify-2") throw new IOException("Update registration failed.");
            if (File.Exists(Path.Combine(root, first.CurrentDirectory, "Maelstrom.exe"))) throw new IOException("Old unchanged application remains.");
            Kept();
            engine.Uninstall();
            if (File.Exists(start) || engine.ReadInstalled()!.Installed) throw new IOException("Uninstall did not complete.");
            using (var key = Registry.CurrentUser.OpenSubKey(registry)) if (key != null) throw new IOException("Uninstall registration remains.");
            Kept();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { passed = true, install = true, update = true, uninstall = true, packsPreserved = true, reportsPreserved = true, recordingsPreserved = true, videosPreserved = true, audioRecordingsPreserved = true, audioTimingPreserved = true, experimentsPreserved = true, decisionsPreserved = true, footagePreserved = true, homeMovementReportsPreserved = true, routinesPreserved = true, idleChoicesPreserved = true, operationHistoryPreserved = true, isolatedWindowsRegistration = true }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(registry, false);
            // This is a unique diagnostic folder, never the user's actual install or data directory.
            var full = Path.GetFullPath(sandbox);
            if (InstallEngine.IsWithin(full, Path.GetFullPath(Path.GetTempPath())) && Path.GetFileName(full) == "MaelstromInstallerVerification-" + id)
            {
                InstallEngine.RejectLinks(full);
                foreach (var entry in Directory.EnumerateFileSystemEntries(full, "*", SearchOption.AllDirectories)) InstallEngine.RejectLinks(entry);
                Directory.Delete(full, true);
            }
        }
    }
}
