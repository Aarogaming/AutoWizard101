using System.Diagnostics;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Short, evidence-producing Dodge test. A dispatch is not a game outcome.</summary>
internal static class DodgeLiveRun
{
    internal static bool IsFreshCapture(double capturedMs, double nowMs, double maxAgeMs) =>
        double.IsFinite(capturedMs) && double.IsFinite(nowMs) && capturedMs <= nowMs && nowMs - capturedMs <= maxAgeMs;
    internal static bool WithinBudget(TimeSpan elapsed, int seconds, int actions, int actionLimit) =>
        elapsed < TimeSpan.FromSeconds(seconds) && actions < actionLimit;
    internal static string ClassifyMovement(PointF? before, PointF? after, string action, Size frameSize)
    {
        if (before == null || after == null) return "unknown";
        var dx = (after.Value.X - before.Value.X) * frameSize.Width;
        var dy = (after.Value.Y - before.Value.Y) * frameSize.Height;
        var delta = action switch { "move-left" => -dx, "move-right" => dx, "move-up" => -dy, "move-down" => dy, "move-up-left" => Math.Min(-dx, -dy), "move-up-right" => Math.Min(dx, -dy), "move-down-left" => Math.Min(-dx, dy), "move-down-right" => Math.Min(dx, dy), _ => 0 };
        return delta >= 4 ? "movement-observed" : "movement-not-observed";
    }

    public static async Task<string> RunAsync(WindowObserver observer, DodgeLivePack pack, string dataRoot, Func<bool> permitted, CancellationToken token, int seconds = 8, int actionLimit = 10)
    {
        if (pack.Context != null) throw new InvalidOperationException("Context-aware Dodge packs currently require the isolated visual test.");
        if (seconds is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (actionLimit is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(actionLimit));
        using var inputOwner = GameInputLease.Acquire("dodge.live-test");
        using var inputScope = GameInputLease.EnterScope(inputOwner);
        var target = observer.Target;
        var clientSize = GameWindows.ClientSize(target);
        var sink = new WindowsDodgeKeySink();
        var pulse = new DodgeInputPulse(sink, permitted);
        var runId = "dodge-live-" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(dataRoot, "dodge-runs", runId);
        if (Directory.Exists(directory)) throw new IOException("Run destination already exists.");
        Directory.CreateDirectory(directory);
        var entries = new List<Dictionary<string, object?>>();
        var staleRejections = 0;
        var elapsed = Stopwatch.StartNew();
        var stopReason = "time-limit";
        try
        {
            while (WithinBudget(elapsed.Elapsed, seconds, entries.Count, actionLimit))
            {
                token.ThrowIfCancellationRequested();
                if (!permitted()) { stopReason = "live-controls-disabled"; break; }
                if (!sink.Ready(target, clientSize)) { stopReason = "target-not-focused-or-changed"; break; }
                if (sink.StopRequested()) { stopReason = "escape"; break; }
                using var before = await NextFrameAsync(observer, null, 500, token).ConfigureAwait(false);
                if (before == null) continue;
                var observation = DodgeSpecialist.Analyze(before.Image);
                var action = observation.Player == null ? null : pack.Resolve(observation.Advice);
                if (action == null || before.CaptureTime == null) { await Task.Delay(100, token).ConfigureAwait(false); continue; }
                if (!WithinBudget(elapsed.Elapsed, seconds, entries.Count, actionLimit)) { stopReason = "time-limit"; break; }
                var captured = before.CaptureTime.Value.TotalMilliseconds;
                var receipt = await pulse.RunAsync(target, clientSize, action.Key, action.DurationMs, captured, token).ConfigureAwait(false);
                Dictionary<string, object?>? entry = null;
                if (receipt.DownMs != null)
                {
                    entry = new Dictionary<string, object?>
                    {
                        ["source_capture_ms"] = captured,
                        ["advice"] = observation.Advice,
                        ["action"] = action.Id,
                        ["key"] = action.Key,
                        ["duration_ms"] = action.DurationMs,
                        ["receipt"] = receipt.Status,
                        ["down_ms"] = receipt.DownMs,
                        ["up_ms"] = receipt.UpMs,
                        ["outcome"] = "unknown"
                    };
                    entries.Add(entry);
                }
                if (receipt.Status != "dispatched" || receipt.UpMs == null)
                {
                    if (receipt.Reason == "stale-or-invalid-frame") { staleRejections++; continue; }
                    stopReason = receipt.Reason == null ? receipt.Status : receipt.Status + ":" + receipt.Reason;
                    break;
                }
                using var after = await NextFrameAsync(observer, receipt.UpMs, 600, token).ConfigureAwait(false);
                var afterObservation = after == null ? null : DodgeSpecialist.Analyze(after.Image);
                var outcome = ClassifyMovement(observation.Player, afterObservation?.Player, action.Id, before.Image.Size);
                var beforeFile = $"before-{entries.Count - 1:D2}.png";
                var afterFile = after == null ? null : $"after-{entries.Count - 1:D2}.png";
                var beforeHash = SaveFrame(before.Image, Path.Combine(directory, beforeFile));
                var afterHash = after == null ? null : SaveFrame(after.Image, Path.Combine(directory, afterFile!));
                entry!["after_capture_ms"] = after?.CaptureTime?.TotalMilliseconds;
                entry["outcome"] = outcome;
                entry["before_player"] = observation.Player;
                entry["after_player"] = afterObservation?.Player;
                entry["before_file"] = beforeFile;
                entry["before_sha256"] = beforeHash;
                entry["after_file"] = afterFile;
                entry["after_sha256"] = afterHash;
                if (outcome != "movement-observed") { stopReason = outcome; break; }
                await Task.Delay(150, token).ConfigureAwait(false);
            }
            if (entries.Count >= actionLimit && stopReason == "time-limit") stopReason = "action-limit";
        }
        catch (OperationCanceledException) { stopReason = "cancelled"; }
        catch (Exception ex) { stopReason = "error: " + ex.Message; }
        var report = new
        {
            schema_version = 1,
            run_id = runId,
            mode = "bounded_dodge_test",
            target = new { target.ProcessId, target.StartedUtcTicks, handle = target.Handle.ToInt64() },
            pack = new { pack.Id, pack.Version, pack.Sha256 },
            elapsed_ms = elapsed.Elapsed.TotalMilliseconds,
            stop_reason = stopReason,
            action_count = entries.Count,
            action_limit = actionLimit,
            stale_frame_rejections = staleRejections,
            actions = entries,
            note = "Movement is inferred from two frames; it does not prove survival, game success, or causation."
        };
        var path = Path.Combine(directory, "report.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        return path;
    }

    private static async Task<CapturedWindowFrame?> NextFrameAsync(WindowObserver observer, double? afterMs, int timeoutMs, CancellationToken token)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.ElapsedMilliseconds < timeoutMs)
        {
            token.ThrowIfCancellationRequested();
            var frame = await observer.ReadTimestampedAsync().ConfigureAwait(false);
            if (frame != null)
            {
                var captured = frame.CaptureTime?.TotalMilliseconds;
                if (captured != null && (afterMs == null
                    ? IsFreshCapture(captured.Value, FrameTimeline.TimestampMs(), 150)
                    : captured.Value >= afterMs)) return frame;
                frame.Dispose();
            }
            await observer.WaitForFrameAsync(token).ConfigureAwait(false);
        }
        return null;
    }

    private static string SaveFrame(Bitmap image, string path)
    {
        image.Save(path, ImageFormat.Png);
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }
}
