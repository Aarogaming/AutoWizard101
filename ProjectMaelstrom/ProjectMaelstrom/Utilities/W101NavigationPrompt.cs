using System.Diagnostics;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Exact prompt interruption over one owned streaming UI observer.
/// Matching stops movement; it never presses X or chooses a destination.</summary>
internal static class W101NavigationPrompt
{
    internal sealed record Snapshot(long Stamp, bool Safe, string? Title, string? StableTitle);
    internal static bool Matches(string? actual, string expected) =>
        !string.IsNullOrWhiteSpace(expected) && expected.Length <= 160 &&
        string.Equals(actual?.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

    internal static Snapshot? ParseLine(string line, Snapshot? previous)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        if (!root.TryGetProperty("kind", out var kind)) return previous;
        if (kind.GetString() == "loading")
            return root.TryGetProperty("loading", out var loading) && loading.ValueKind == JsonValueKind.False ? previous : null;
        if (kind.GetString() != "tree") return null;
        var states = root.GetProperty("states");
        var safe = root.GetProperty("complete").GetBoolean() &&
            new[] { "nativeIdle", "menu", "dialogue" }.All(name => states.GetProperty(name).ValueKind == JsonValueKind.False);
        string? Text(string name) => root.TryGetProperty(name, out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null;
        return new(root.GetProperty("statesObservedUtcTicks").GetInt64(), safe,
            states.GetProperty("interactionPrompt").ValueKind == JsonValueKind.True ? Text("interactionTitle") : null,
            states.GetProperty("interactionPrompt").ValueKind == JsonValueKind.True ? Text("stableInteractionTitle") : null);
    }

    private static Snapshot? Read(string path)
    {
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length > 4 * 1024 * 1024) throw new InvalidDataException("Prompt trace budget exceeded.");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        var end = text.LastIndexOf('\n');
        if (end < 0) return null;
        Snapshot? value = null;
        foreach (var line in text[..end].Split('\n', StringSplitOptions.RemoveEmptyEntries)) value = ParseLine(line, value);
        return value;
    }

    internal static async Task<RoutineReceipt> RunAsync(OperationContext owner, WizwalkerTarget target,
        WizwalkerLaunch launch, string expected, ModuleDecision proposal,
        Func<Func<bool>, Func<bool>, Task<RoutineReceipt>> move, Func<bool> owned, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(expected) || expected.Length > 160 || expected.Any(char.IsControl))
            throw new InvalidDataException("Choose one exact interaction title.");
        var directory = Path.Combine(owner.Directory, "prompt-watch");
        Directory.CreateDirectory(directory);
        var watch = W101UiTreeOperation.WatchOwnedAsync(owner with { Directory = directory }, target, launch, token);
        var clock = Stopwatch.StartNew();
        Snapshot? snapshot = null;
        var keepMoving = true;
        Task<RoutineReceipt>? motion = null;
        RoutineReceipt? receipt = null;
        var matched = false;
        bool Fresh() => snapshot is { Safe: true } && snapshot.Stamp <= DateTime.UtcNow.Ticks &&
            DateTime.UtcNow.Ticks - snapshot.Stamp <= TimeSpan.FromSeconds(2).Ticks;
        bool Continue() => keepMoving && owned() && Fresh() && !Matches(snapshot?.Title, expected);
        try
        {
            while (!watch.IsCompleted && owned() && clock.ElapsedMilliseconds < 8000)
            {
                snapshot = Read(Path.Combine(directory, "ui-observations.jsonl"));
                if (Fresh()) break;
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            if (Fresh() && owned())
            {
                if (proposal.Action!.DurationMs > 0 && !Matches(snapshot!.Title, expected)) motion = move(() => owned() && Fresh(), Continue);
                while (owned() && !watch.IsCompleted && clock.ElapsedMilliseconds < 35000)
                {
                    snapshot = Read(Path.Combine(directory, "ui-observations.jsonl"));
                    if (Fresh() && Matches(snapshot!.Title, expected))
                    {
                        keepMoving = false;
                        if (Matches(snapshot.StableTitle, expected)) { matched = true; break; }
                    }
                    else if (proposal.Action!.DurationMs == 0 || motion?.IsCompleted == true || !Fresh()) break;
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            keepMoving = false;
            try { if (motion is not null) receipt = await motion.ConfigureAwait(false); }
            finally
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "stop.request"), "stop").ConfigureAwait(false);
                await watch.ConfigureAwait(false);
            }
        }
        var clean = owner.Input?.RecoveryRequired == false;
        var reached = matched && clean && owned() && (receipt?.InputReleased ?? true);
        GameExperiments.WriteAtomic(Path.Combine(owner.Directory, "interaction-arrival.json"), new
        {
            expected, matched, reached, snapshot, receipt, inputDispatched = motion is not null,
            interactionDispatched = false, routeQualified = false
        });
        return new(proposal.ProposalId!, proposal.ActionId!, FrameTimeline.TimestampMs(), reached, receipt?.InputReleased ?? true);
    }
}
