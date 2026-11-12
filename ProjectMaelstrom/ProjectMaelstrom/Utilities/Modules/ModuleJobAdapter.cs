using System.Text;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Known module kinds become prepared work, never a nested scheduler.
/// Saved cards only select offline review; owned execution is trusted code.</summary>
internal static class ModuleJobAdapter
{
    internal static PreparedDeckJob PrepareReplay(string bundlePath, string manifestPath, string expectedKind,
        int timeoutSeconds = 60, string? expectedGameId = null)
    {
        if (timeoutSeconds is < 1 or > 300) throw new InvalidDataException("Module timeout must be 1–300 seconds.");
        bundlePath = Path.GetFullPath(bundlePath); manifestPath = Path.GetFullPath(manifestPath);
        var bundleBytes = RoutineReplay.ReadBounded(bundlePath, ModuleCatalog.MaximumBundleCharacters);
        var bundle = ModuleCatalog.ReadBundle(Encoding.UTF8.GetString(bundleBytes));
        ValidateKind(bundle, expectedKind, expectedGameId);
        var manifestBytes = RoutineReplay.ReadBounded(manifestPath, 65536);
        using var manifest = RoutineReplay.Parse(manifestBytes);
        var root = manifest.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1 || string.IsNullOrWhiteSpace(root.GetProperty("clock_origin").GetString()))
            throw new InvalidDataException("Unsupported session manifest.");
        OperationModules.CheckId(root.GetProperty("session_id").GetString());
        var binding = root.GetProperty("routine_binding").Deserialize<RoutineBinding>(OperationModules.Json)
            ?? throw new InvalidDataException("Missing routine binding.");
        _ = new ModuleRunner(bundle, binding);
        var eventsName = root.GetProperty("events_file").GetString();
        if (string.IsNullOrWhiteSpace(eventsName) || eventsName is "." or ".." ||
            eventsName.IndexOfAny(['/', '\\', ':']) >= 0 || Path.GetFileName(eventsName) != eventsName ||
            new[] { "bundle.json", "session.json", "module-replay.json", "module-operation.json" }.Contains(eventsName, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a local, non-reserved events filename.");
        var eventsPath = Path.Combine(Path.GetDirectoryName(manifestPath)!, eventsName);
        if (File.GetAttributes(eventsPath).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Event links are unsupported.");
        var eventBytes = RoutineReplay.ReadBounded(eventsPath, 16 * 1024 * 1024);
        var pins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [bundlePath] = RoutineReplay.Digest(bundleBytes), [manifestPath] = RoutineReplay.Digest(manifestBytes),
            [eventsPath] = RoutineReplay.Digest(eventBytes)
        };
        var request = new OperationRequest(Guid.NewGuid().ToString("N"), "module.labelled-replay", "1", timeoutSeconds,
            false, "saved-module-session", pins);
        return new(request, (context, token) =>
        {
            token.ThrowIfCancellationRequested();
            if (context.Input != null || GameInputLease.Current != null || context.Request.WritesGame || context.Request.DelegatedInput)
                throw new InvalidOperationException("Offline module replay cannot own game input.");
            var inputs = Path.Combine(context.Directory, "module-inputs"); Directory.CreateDirectory(inputs);
            string Save(string name, byte[] bytes)
            {
                var path = Path.Combine(inputs, name);
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(bytes); return path;
            }
            var frozenBundle = Save("bundle.json", bundleBytes);
            var frozenManifest = Save("session.json", manifestBytes);
            var frozenEvents = Save(eventsName, eventBytes);
            var admission = Path.Combine(context.Directory, "module-operation.json");
            var report = Path.Combine(context.Directory, "module-replay.json");
            GameExperiments.WriteAtomic(admission, new { schema_version = 1, mode = "offline-labelled-replay", module_kind = expectedKind,
                desktop_dispatch_count = 0, live_qualified = false, input_lease_acquired = false, source_pins = context.Request.Pins });
            bool completed;
            try { completed = ModuleReplay.Run(frozenBundle, frozenManifest, report, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return Task.FromResult(new OperationOutcome(OperationState.Cancelled, "Offline module replay cancelled; no game inputs sent.",
                    [admission, frozenBundle, frozenManifest, frozenEvents]));
            }
            using var result = RoutineReplay.Parse(RoutineReplay.ReadBounded(report, 64 * 1024 * 1024));
            var stopped = result.RootElement.GetProperty("stopped").GetBoolean();
            return Task.FromResult(new OperationOutcome(completed ? OperationState.Completed : stopped ? OperationState.Failed : OperationState.Unknown,
                completed ? "Saved module replay completed; no game inputs sent." : stopped ? "Saved module replay stopped; inspect the outcome report." : "Saved module evidence did not establish completion.",
                [admission, frozenBundle, frozenManifest, frozenEvents, report]));
        });
    }

    internal static PreparedDeckJob PrepareOffline(string manifestPath, string expectedKind,
        int timeoutSeconds = 60, string? expectedGameId = null)
    {
        var snapshot = ModuleScreenReviewSnapshot.Load(manifestPath);
        ValidateKind(snapshot.Bundle(), expectedKind, expectedGameId);
        return ModuleOperation.PrepareOffline(snapshot, timeoutSeconds);
    }

    internal static PreparedDeckJob PrepareOwned(OperationModuleBundle bundle, string expectedKind,
        RoutineBinding binding, Func<OperationContext, CancellationToken, Task<OperationOutcome>> execute,
        int timeoutSeconds = 60, string? expectedGameId = null)
    {
        ValidateKind(bundle, expectedKind, expectedGameId);
        return ModuleOperation.PrepareOwned(bundle, binding, execute, timeoutSeconds);
    }

    internal static void ValidateKind(OperationModuleBundle bundle, string expectedKind, string? expectedGameId = null)
    {
        if (expectedKind is not ("navigation" or "sigil" or "menu" or "auto-idle"))
            throw new InvalidDataException("Choose a registered module kind.");
        ModuleCatalog.Compile(bundle);
        if (bundle.Modules.Any(module => module.Kind != expectedKind))
            throw new InvalidDataException("This card accepts only its selected module kind.");
        if (expectedGameId is not null && bundle.GameId != expectedGameId)
            throw new InvalidDataException("The module session belongs to another game.");
    }
}
