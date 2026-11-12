using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Headless test-only adapter for an explicitly instrumented, owned Dodge copy.</summary>
internal static class DodgeNativeVerification
{
    public static string PrepareProject(string sourceDirectory, string probePath, string outputRoot, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        sourceDirectory = Path.GetFullPath(sourceDirectory);
        if (!File.Exists(Path.Combine(sourceDirectory, "project.godot")) || !File.Exists(Path.Combine(sourceDirectory, "main.gd")) || !File.Exists(probePath))
            throw new FileNotFoundException("The local Dodge game or the packaged test adapter is missing.");
        if (new FileInfo(probePath).Length > 64 * 1024) throw new InvalidDataException("Test adapter is too large.");
        var probe = File.ReadAllText(probePath);
        if (!probe.Contains("# MAELSTROM_DODGE_NATIVE_TEST_V1", StringComparison.Ordinal)) throw new InvalidDataException("Invalid Dodge test adapter.");
        var directory = Path.Combine(outputRoot, "dodge-native-projects", Guid.NewGuid().ToString("N"));
        var relativeDestination = Path.GetRelativePath(sourceDirectory, Path.GetFullPath(directory));
        if (!Path.IsPathRooted(relativeDestination) && relativeDestination != ".." && !relativeDestination.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("The test destination must be outside the source game.");
        if (Directory.Exists(directory)) throw new IOException("Test-project destination already exists.");
        Directory.CreateDirectory(directory);
        var pending = new Stack<string>();
        pending.Push(sourceDirectory);
        var count = 0;
        long bytes = 0;
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var current = pending.Pop();
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Test-game source cannot contain linked folders.");
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                token.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Test-game source cannot contain links.");
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (++count > 2048) throw new InvalidDataException("Test-game source exceeds the copy limit.");
                    pending.Push(entry);
                    continue;
                }
                var length = new FileInfo(entry).Length;
                if (++count > 2048 || (bytes += length) > 64 * 1024 * 1024) throw new InvalidDataException("Test-game source exceeds the copy limit.");
                var target = Path.Combine(directory, Path.GetRelativePath(sourceDirectory, entry));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(entry, target, overwrite: false);
            }
        }
        var main = Path.Combine(directory, "main.gd");
        if (File.ReadAllText(main).Contains("# MAELSTROM_DODGE_NATIVE_TEST_V1", StringComparison.Ordinal))
            throw new InvalidDataException("Source game is already instrumented; choose the original test copy.");
        File.AppendAllText(main, Environment.NewLine + probe);
        return directory;
    }

    public static async Task<string> RunAsync(string godotPath, string projectDirectory, string packPath, string outputRoot, CancellationToken token)
    {
        var main = Path.Combine(projectDirectory, "main.gd");
        if (!File.Exists(godotPath) || !File.Exists(Path.Combine(projectDirectory, "project.godot")) || !File.Exists(main) ||
            new FileInfo(main).Length > 1024 * 1024 || !File.ReadAllText(main).Contains("# MAELSTROM_DODGE_NATIVE_TEST_V1", StringComparison.Ordinal))
            throw new InvalidOperationException("Choose the isolated, instrumented Dodge test copy.");
        var pack = DodgeLivePack.Load(packPath);
        if (pack.Context != null) throw new InvalidOperationException("Context-aware Dodge packs currently require the isolated visual test.");
        var runId = "dodge-native-" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(outputRoot, "dodge-native-runs", runId);
        if (Directory.Exists(directory)) throw new IOException("Run destination already exists.");
        Directory.CreateDirectory(directory);
        var channel = Path.Combine(directory, "channel");
        Directory.CreateDirectory(channel);
        var info = new ProcessStartInfo(godotPath) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = projectDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("--headless");
        info.ArgumentList.Add("--path");
        info.ArgumentList.Add(projectDirectory);
        info.ArgumentList.Add("--audio-driver");
        info.ArgumentList.Add("Dummy");
        info.ArgumentList.Add("--log-file");
        info.ArgumentList.Add(Path.Combine(directory, "godot-engine.log"));
        info.Environment["MAELSTROM_DODGE_CHANNEL"] = channel;
        using var job = new OwnedProcessJob();
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start the owned headless test game.");
        try { job.Attach(process); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (System.ComponentModel.Win32Exception) { }
        var stdout = ReadBoundedAsync(process.StandardOutput);
        var stderr = ReadBoundedAsync(process.StandardError);
        var actions = new List<Dictionary<string, object?>>();
        var elapsed = Stopwatch.StartNew();
        var sequence = 0;
        var stop = "time-limit";
        try
        {
            while (elapsed.Elapsed < TimeSpan.FromSeconds(8) && actions.Count < 10)
            {
                token.ThrowIfCancellationRequested();
                using var observation = await RequestAsync(process, channel, sequence++, new { kind = "observe" }, token).ConfigureAwait(false);
                var root = observation.RootElement;
                if (root.GetProperty("kind").GetString() != "observation") throw new InvalidDataException("Unexpected observation response.");
                if (!root.GetProperty("visible").GetBoolean()) { stop = "game-over-or-player-hidden"; break; }
                var player = Point(root.GetProperty("player"));
                var threatsElement = root.GetProperty("threats");
                if (threatsElement.ValueKind != JsonValueKind.Array || threatsElement.GetArrayLength() > 32) throw new InvalidDataException("Invalid threat data.");
                var threats = threatsElement.EnumerateArray().Select(Point).ToArray();
                var advice = DodgeSpecialist.Recommend(player, threats);
                var action = pack.Resolve(advice);
                if (action == null) { await Task.Delay(150, token).ConfigureAwait(false); continue; }
                var requestId = sequence++;
                var entry = new Dictionary<string, object?> { ["request_id"] = requestId, ["advice"] = advice, ["action"] = action.Id, ["duration_ms"] = action.DurationMs, ["input_status"] = "issued", ["outcome"] = "unknown" };
                actions.Add(entry);
                using var result = await RequestAsync(process, channel, requestId, new { kind = "action", action = action.Id, duration_ms = action.DurationMs }, token).ConfigureAwait(false);
                var response = result.RootElement;
                if (response.GetProperty("kind").GetString() != "action-result" || response.GetProperty("action").GetString() != action.Id) throw new InvalidDataException("Action response does not match the request.");
                var before = Point(response.GetProperty("before"));
                var after = Point(response.GetProperty("player"));
                var visible = response.GetProperty("visible").GetBoolean();
                var outcome = visible ? DodgeLiveRun.ClassifyMovement(before, after, action.Id, new Size(480, 720)) : "unknown";
                var evidence = Path.Combine(channel, $"response-{requestId:D4}.json");
                entry["input_status"] = "acknowledged";
                entry["before"] = new[] { before.X, before.Y };
                entry["after"] = new[] { after.X, after.Y };
                entry["outcome"] = outcome;
                entry["visible"] = visible;
                entry["score"] = response.GetProperty("score").GetInt32();
                entry["evidence"] = Path.GetRelativePath(directory, evidence).Replace('\\', '/');
                entry["evidence_sha256"] = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(evidence, token).ConfigureAwait(false)));
                if (!visible) { stop = "game-over-or-player-hidden"; break; }
                if (outcome != "movement-observed") { stop = outcome; break; }
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            if (actions.Count >= 10) stop = "action-limit";
        }
        catch (OperationCanceledException) { stop = "cancelled"; }
        catch (Exception ex) { stop = "error: " + ex.Message; }
        double? cpuSeconds = null;
        long? memoryMiB = null;
        try { process.Refresh(); cpuSeconds = process.TotalProcessorTime.TotalSeconds; memoryMiB = process.PeakWorkingSet64 / (1024 * 1024); }
        catch (InvalidOperationException) { }
        var report = new { schema_version = 1, run_id = runId, mode = "godot-native-test", observation_source = "instrumented-game-state", input_source = "godot-input-action", pack = new { pack.Id, pack.Version, pack.Sha256 }, probe_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(main))), elapsed_ms = elapsed.Elapsed.TotalMilliseconds, game_cpu_seconds = cpuSeconds, game_peak_memory_mib = memoryMiB, stop_reason = stop, action_count = actions.Count, actions, note = "Headless instrumented test. This does not verify screen capture, visual detection, desktop focus, desktop key input, or general gameplay skill." };
        var reportPath = Path.Combine(directory, "report.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        job.Dispose();
        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(directory, "game.log"), stdout.Result + Environment.NewLine + stderr.Result).ConfigureAwait(false);
        return reportPath;
    }

    private static PointF Point(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 2) throw new InvalidDataException("Invalid position.");
        var x = value[0].GetDouble(); var y = value[1].GetDouble();
        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) > 2000 || Math.Abs(y) > 2000) throw new InvalidDataException("Position is out of bounds.");
        return new PointF((float)(x / 480), (float)(y / 720));
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        var output = new System.Text.StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) != 0)
        {
            var keep = Math.Min(read, Math.Max(0, 65536 - output.Length));
            if (keep > 0) output.Append(buffer, 0, keep);
        }
        return output.ToString();
    }

    private static async Task<JsonDocument> RequestAsync(Process process, string channel, int id, object payload, CancellationToken token)
    {
        var node = JsonSerializer.SerializeToNode(payload)!.AsObject();
        node["id"] = id;
        var request = Path.Combine(channel, $"request-{id:D4}.json");
        var temporary = request + ".tmp";
        await File.WriteAllTextAsync(temporary, node.ToJsonString(), token).ConfigureAwait(false);
        File.Move(temporary, request);
        var response = Path.Combine(channel, $"response-{id:D4}.json");
        var wait = Stopwatch.StartNew();
        while (!File.Exists(response) && wait.ElapsedMilliseconds < 2500 && !process.HasExited)
            await Task.Delay(50, token).ConfigureAwait(false);
        if (!File.Exists(response)) throw new TimeoutException("The owned test game did not respond.");
        if (new FileInfo(response).Length > 64 * 1024) throw new InvalidDataException("Game response is too large.");
        var json = JsonDocument.Parse(await File.ReadAllBytesAsync(response, token).ConfigureAwait(false));
        if (json.RootElement.GetProperty("id").GetInt32() != id) { json.Dispose(); throw new InvalidDataException("Mismatched game response."); }
        return json;
    }
}
