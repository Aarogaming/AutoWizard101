using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Opt-in bridge from the reviewed one-shot worker to shared screen routines.
/// Inherits the parent's pinned inputs and exclusive lease. At most 32 serial reads;
/// no hooks, focus changes, input, cached-clear fallback or second controller.</summary>
internal sealed class WizwalkerModuleLoading
{
    internal static readonly ModuleLoadingRequirement Requirement = new(WizwalkerOperation.Version);
    private readonly OperationContext _context;
    private readonly WizwalkerSession _session;
    private readonly WizwalkerLaunch _launch;
    private readonly RoutineBinding _binding;
    private readonly WizwalkerRuntime? _runtime;
    private readonly Func<double> _clock;
    private int _reads, _busy;
    private double _lastStart = double.NegativeInfinity;

    internal WizwalkerModuleLoading(OperationContext context, WizwalkerSession session, WizwalkerLaunch launch,
        RoutineBinding binding, WizwalkerRuntime? runtime = null, Func<double>? clock = null)
    {
        // Validate explicit opt-in and the same source identities as standalone admission.
        var prepared = WizwalkerOperation.Prepare(session, launch, runtime, WizwalkerOperation.Id, WizwalkerOperation.Version);
        if (context.Request.Target != ModuleOwnedInput.TargetId(session.Target.Window) || binding.TargetId != context.Request.Target ||
            !binding.ContextId.EndsWith(".visit." + context.Request.Id, StringComparison.Ordinal) ||
            prepared.Pins.Any(pin => !context.Request.Pins.TryGetValue(pin.Key, out var hash) ||
                !hash.Equals(pin.Value, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Bind the optional loading observer to this admitted target, visit and pinned adapter.");
        _context = context; _session = session; _launch = launch; _binding = binding; _runtime = runtime;
        _clock = clock ?? FrameTimeline.TimestampMs;
    }

    internal async Task<ModuleLoadingObservation> ReadAsync(CancellationToken token)
    {
        var started = _clock();
        var unknown = new ModuleLoadingObservation(_binding, Requirement.Source, started, null);
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return unknown;
        try
        {
            token.ThrowIfCancellationRequested();
            if (!double.IsFinite(started) || started < 0 || started - _lastStart < 500 || _reads >= 32) return unknown;
            _lastStart = started;
            var folder = Path.Combine(_context.Directory, "module-loading-" + (_reads++).ToString("D3"));
            if (Directory.Exists(folder)) throw new IOException("Loading evidence must use a new directory.");
            Directory.CreateDirectory(folder);
            var utcStart = DateTime.UtcNow.Ticks;
            var outcome = await WizwalkerOperation.ReadOwnedAsync(_context with { Directory = folder }, _session.Target,
                _launch, _runtime, token).ConfigureAwait(false);
            if (outcome.State != OperationState.Completed) return unknown;
            var result = JsonSerializer.Deserialize<WizwalkerResult>(GameExperiments.BoundedBytes(Path.Combine(folder, "adapter-result.json"), 16384))!;
            // Timestamp from before the read is conservative: worker startup and
            // disconnect consume freshness instead of making an old reading new.
            if (result.ObservedUtcTicks < utcStart || result.ObservedUtcTicks > DateTime.UtcNow.Ticks ||
                !ModuleLoadingGate.IsCurrent(Requirement, unknown, _binding, _clock())) return unknown;
            return unknown with { Loading = result.Loading };
        }
        finally { Volatile.Write(ref _busy, 0); }
    }
}
