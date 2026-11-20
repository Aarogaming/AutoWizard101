namespace ProjectMaelstrom.Utilities;

/// <summary>Admits the real UI reader into a module's existing scheduler request.
/// Worker and target pins are frozen before queueing; this grants no semantic
/// qualification and never creates another controller or input lease.</summary>
internal static class W101ModuleAdmission
{
    internal static PreparedDeckJob Prepare(string mode, WizwalkerSession session, WizwalkerLaunch launch,
        RoutineBinding binding, bool enabled, W101ModuleRuntime runtime, int maximumReadings = 24,
        int timeoutSeconds = 60, string? expectedPrompt = null, WizwalkerRuntime? validation = null,
        W101RoutineIdleRuntime? idleRuntime = null)
    {
        if (!enabled) throw new InvalidOperationException("Enable the optional W101 module actions first.");
        var helper = Path.Combine(Path.GetDirectoryName(launch.Worker)!, "wizwalker_adapter.py");
        if (validation is null && !GameExperiments.Digest(helper).Equals(WizwalkerOperation.WorkerSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The reviewed module observation helper changed.");
        var admitted = WizwalkerOperation.Prepare(session, launch, validation,
            ModuleOperation.OwnedId, ModuleOperation.Version, W101UiTreeOperation.WorkerSha256);
        var pins = new Dictionary<string, string>(admitted.Pins, StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetFullPath(helper)] = GameExperiments.Digest(helper)
        };
        var job = W101ModuleOperation.Prepare(mode, session.Target, binding, enabled, runtime,
            maximumReadings, timeoutSeconds, expectedPrompt);
        // Preserve the module binding target and operation ID. The observer only
        // borrows its owner's pin set and lease, not a separately queued request.
        var admittedJob = job with { Request = job.Request with { Pins = pins } };
        return validation is null || idleRuntime is not null
            ? W101RoutineIdle.Wrap(admittedJob, session.Target, launch, idleRuntime) : admittedJob;
    }
}
