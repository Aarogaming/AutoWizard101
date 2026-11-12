namespace ProjectMaelstrom.Utilities;

/// <summary>Trusted owned stages: no scheduler submission, new control scope or
/// activity baseline. A stage must latch its lease before returning/throwing when
/// input or observation-resource cleanup is uncertain.</summary>
internal sealed record W101NavigationVisitRuntime(Func<bool> Ready,
    Func<OperationContext, string, CancellationToken, Task<OperationOutcome>> RunIdle,
    Func<OperationContext, CancellationToken, Task<OperationOutcome>> RunPulse,
    Func<int, CancellationToken, Task<bool>>? ResumeAfterQuiet = null);

/// <summary>One app-owned teaching visit. Wake, one selected pulse and restoration
/// share a single scheduler lease and control scope. Bounded Idle retries, no refocusing,
/// route-arrival inference, or gameplay replay after user takeover. Quiet cleanup resumption is opt-in.</summary>
internal static class W101NavigationVisit
{
    internal const string Id = "w101.navigation-visit";
    internal const string Version = "w101-navigation-visit-v2";
    private sealed record Stage(string Name, string RunId, string Directory, int BudgetSeconds, OperationOutcome Outcome);

    internal static PreparedDeckJob Prepare(WizwalkerSession session, WizwalkerLaunch launch, bool enabled,
        string key, int durationMs, W101NavigationVisitRuntime? runtime = null, WizwalkerRuntime? validation = null, W101VisitPolicy? policy = null,
        bool measurePose = false, int waypointDistance = 0, bool feedbackWaypoint = false)
    {
        policy ??= new();
        policy.Validate();
        if (feedbackWaypoint && (!measurePose || waypointDistance is < 20 or > 100 || key != "W" || durationMs is < 20 or > 100))
            throw new InvalidDataException("Feedback requires a measured 20–100 unit forward waypoint and a 20–100 ms maximum pulse.");
        if (waypointDistance != 0 && (!measurePose || key != "W" || waypointDistance is < 20 or > 100))
            throw new InvalidDataException("Waypoint trials require measured forward movement and a 20â€“100 unit target.");
        // Reuse exactly the same selection validation, reviewed hashes and pins.
        // Preparing does not execute or create another scheduler/control owner.
        var admitted = W101NavigationProbe.Prepare(session, launch, enabled, key, durationMs, validation: validation).Request;
        var request = admitted with { Operation = Id, Version = Version, TimeoutSeconds = feedbackWaypoint ? 125 : 85 };
        if (measurePose)
        {
            var pins = request.Pins.ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var (name, expected) in new[] { ("w101_pose_reader.py", W101UiTreeOperation.PoseReaderSha256), ("w101_combat_reader.py", W101CombatRuntime.FieldReaderSha256) })
            {
                var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(launch.Worker)!, name));
                if (GameExperiments.Digest(path) != expected) throw new InvalidDataException("Reviewed pose reader changed.");
                pins[path] = expected;
            }
            request = request with { Pins = pins };
        }
        var actionId = key switch { "W" => "game.move-forward", "A" => "game.turn-left", _ => "game.turn-right" };
        var action = new ModuleAction(actionId, "keyboard-pulse", key, durationMs, "w101.navigation");
        return new(request, async (context, token) =>
        {
            if (runtime is not null) return await RunOwnedAsync(context, runtime, policy, token).ConfigureAwait(false);
            var target = session.Target;
            await using var scope = await W101ControlScope.CreateAsync(context, target.Window, GameWindows.ClientSize(target.Window), token, policy.ActivateWhenQuiet).ConfigureAwait(false);
            var geometry = WindowObserver.GetCaptureSize(target.Window);
            var idle = W101IdleCycleOperation.CreateRuntime(target, launch, scope, geometry);
            var pulse = W101NavigationProbe.CreateRuntime(target, launch, scope, geometry);
            var live = new W101NavigationVisitRuntime(scope.Ready,
                (owner, mode, cancellation) => W101IdleCycleOperation.RunOwnedAsync(owner, target, idle, cancellation, mode),
                (owner, cancellation) => feedbackWaypoint ? W101FeedbackVisit.RunAsync(owner, target, launch, scope, pulse, waypointDistance, durationMs, cancellation) : measurePose ? MeasureAsync(owner, scope.Ready,
                    (index, ct) => W101UiTreeOperation.ReadOwnedAsync(owner, target, launch, index, ct, true),
                    ct => W101NavigationProbe.RunOwnedAsync(owner, target, action, pulse, ct), cancellation, waypointDistance) :
                    W101NavigationProbe.RunOwnedAsync(owner, target, action, pulse, cancellation),
                scope.ResumeAfterQuietAsync);
            return await RunOwnedAsync(context, live, policy, token, feedbackWaypoint ? 60 : measurePose ? 24 : 12).ConfigureAwait(false);
        });
    }

    internal static async Task<OperationOutcome> MeasureAsync(OperationContext context, Func<bool> ready,
        Func<int, CancellationToken, Task<W101UiTreeResult>> read, Func<CancellationToken, Task<OperationOutcome>> pulse,
        CancellationToken token, int waypointDistance = 0)
    {
        var before = await read(24, token).ConfigureAwait(false);
        bool Usable(W101UiTreeResult sample) => sample.CleanupConfirmed && sample.HookRestored && sample.Loading == false &&
            sample.PoseCandidate is { Status: "candidate" } && W101UiTreeOperation.ValidPose(sample.PoseCandidate, true, DateTime.UtcNow.Ticks);
        if (!Usable(before) || !ready()) return new(OperationState.Failed, "measured-movement-needs-fresh-start-pose", []);
        if (waypointDistance != 0 && waypointDistance is < 20 or > 100)
            throw new InvalidDataException("Invalid waypoint trial distance.");
        var start = before.PoseCandidate!.Position!;
        // Archived W101 heading convention. A live trial must verify this
        // relation; a miss is recorded as failure, never fitted after movement.
        W101PosePoint? waypoint = waypointDistance == 0 ? null : new(
            start.X - Math.Sin(before.PoseCandidate.YawRadians!.Value) * waypointDistance,
            start.Y - Math.Cos(before.PoseCandidate.YawRadians.Value) * waypointDistance, start.Z);
        var planPath = Path.Combine(context.Directory, "movement-plan.json");
        GameExperiments.WriteAtomic(planPath, new { start = before.PoseCandidate, waypoint, arrivalRadius = 10,
            headingConvention = "archived-minus-sin-minus-cos", routeQualified = false });
        var outcome = await pulse(token).ConfigureAwait(false);
        if (outcome.State != OperationState.Completed || !ready()) return outcome.State == OperationState.Completed ?
            outcome with { State = OperationState.Failed, Message = "measured-movement-control-changed" } : outcome;
        var after = await read(25, token).ConfigureAwait(false);
        var valid = Usable(after) && before.Target == after.Target &&
            after.PoseCandidate!.ObservedUtcTicks > before.PoseCandidate!.ObservedUtcTicks;
        var a = before.PoseCandidate!.Position!; var b = after.PoseCandidate?.Position;
        double? distance = valid ? Math.Sqrt(Math.Pow(b!.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2)) : null;
        double? remaining = valid && waypoint is not null ? Math.Sqrt(Math.Pow(b!.X - waypoint.X, 2) + Math.Pow(b.Y - waypoint.Y, 2)) : null;
        var arrived = remaining is <= 10;
        var report = Path.Combine(context.Directory, "movement-measurement.json");
        GameExperiments.WriteAtomic(report, new { before = before.PoseCandidate, after = after.PoseCandidate,
            measurementsValid = valid, planarDistance = distance, waypoint, distanceToWaypoint = remaining,
            waypointCandidateReached = arrived, arrivalVerified = false, routeQualified = false });
        return new(valid && (waypoint is null || arrived) ? outcome.State : OperationState.Failed,
            !valid ? "movement-end-pose-unavailable" : waypoint is null ? "movement-measured-arrival-not-inferred" :
            arrived ? "nearby-waypoint-coordinate-candidate-observed" : "nearby-waypoint-not-reached", [.. outcome.Artifacts, report]);
    }

    private static async Task<OperationOutcome> RunOwnedAsync(OperationContext parent, W101NavigationVisitRuntime runtime, W101VisitPolicy policy, CancellationToken token, int pulseBudgetSeconds = 12)
    {
        var stages = new List<Stage>();
        var resumes = new List<W101IdleResume>();
        OperationOutcome? wake = null, pulse = null, idle = null;
        string? idleSkippedReason = null;
        bool Ready() => !token.IsCancellationRequested && parent.Request.WritesGame && !parent.Request.DelegatedInput &&
            parent.Input is { CanDispatch: true } && ReferenceEquals(parent.Input, GameInputLease.Current) && runtime.Ready();

        async Task<OperationOutcome> StageAsync(string name, int budgetSeconds,
            Func<OperationContext, CancellationToken, Task<OperationOutcome>> execute, CancellationToken stageToken = default)
        {
            var stageId = Guid.NewGuid().ToString("N");
            var directory = Path.Combine(parent.Directory, name);
            if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Teaching stage evidence already exists.");
            Directory.CreateDirectory(directory);
            var child = parent with { Directory = directory,
                Request = parent.Request with { Id = stageId, TimeoutSeconds = budgetSeconds } };
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token, stageToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
            OperationOutcome outcome;
            try
            {
                outcome = Ready() ? await execute(child, deadline.Token).ConfigureAwait(false)
                    : new(OperationState.Failed, "target-owner-or-takeover-changed", []);
            }
            catch (OperationCanceledException)
            { outcome = new(token.IsCancellationRequested ? OperationState.Cancelled : OperationState.TimedOut, "Teaching stage stopped.", []); }
            catch (Exception error) { outcome = new(OperationState.Failed, error.GetType().Name + ": " + error.Message, []); }
            if (outcome.State == OperationState.CleanupFailed && parent.Input?.RecoveryRequired != true)
                parent.Input?.RequireRecovery("Teaching stage reported uncertain cleanup: " + name);
            if (parent.Input?.RecoveryRequired == true)
                outcome = outcome with { State = OperationState.CleanupFailed };
            else if (token.IsCancellationRequested)
                outcome = outcome with { State = OperationState.Cancelled };
            else if (deadline.IsCancellationRequested)
                outcome = outcome with { State = OperationState.TimedOut };
            var stage = new Stage(name, stageId, directory, budgetSeconds, outcome);
            stages.Add(stage);
            GameExperiments.WriteAtomic(Path.Combine(directory, "visit-stage.json"), new { parentRunId = parent.Request.Id, stage });
            return outcome;
        }

        try
        {
            wake = await StageAsync("01-wake", 20, (child, cancellation) => runtime.RunIdle(child, "wake", cancellation)).ConfigureAwait(false);
            if (wake.State == OperationState.Completed && Ready())
                pulse = await StageAsync("02-pulse", pulseBudgetSeconds, runtime.RunPulse).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // Stages own cleanup classification. Evidence-writing/semantic errors
            // do not authorize extra input, but confirmed ownership can still Idle.
            pulse ??= new(OperationState.Failed, error.GetType().Name + ": " + error.Message, []);
        }
        finally
        {
            if (token.IsCancellationRequested) idleSkippedReason = "parent-cancelled";
            else if (parent.Input?.RecoveryRequired == true) idleSkippedReason = "cleanup-uncertain";
            else
            {
                try
                {
                    var finalization = await W101IdleFailsafe.RunAsync(parent, policy, Ready,
                        (attempt, cancellation) => StageAsync(attempt == 0 ? "03-idle" : "03-idle-retry-" + attempt,
                            20, (child, stageToken) => runtime.RunIdle(child, "idle", stageToken), cancellation),
                        runtime.ResumeAfterQuiet, token).ConfigureAwait(false);
                    idle = finalization.Outcome;
                    idleSkippedReason = finalization.SkippedReason;
                    resumes.AddRange(finalization.Resumes);

                }
                catch (Exception error)
                { idle = new(OperationState.Failed, error.GetType().Name + ": " + error.Message, []); }
            }
        }
        var idleVerified = idle?.State == OperationState.Completed && idleSkippedReason is null;
        var state = parent.Input?.RecoveryRequired == true ? OperationState.CleanupFailed :
            token.IsCancellationRequested ? OperationState.Cancelled :
            wake?.State == OperationState.Completed && pulse?.State == OperationState.Completed && idleVerified
                ? OperationState.Completed : OperationState.Failed;
        var reason = state == OperationState.Completed ? "teaching-visit-collected-and-native-idle-observed" :
            idleVerified ? "teaching-visit-incomplete-native-idle-observed" : "teaching-visit-incomplete-idle-unverified";
        var report = Path.Combine(parent.Directory, "navigation-visit.json");
        GameExperiments.WriteAtomic(report, new { schemaVersion = 1, parentRunId = parent.Request.Id,
            state = state.ToString(), reason, wake, pulse, idle, idleSkippedReason, stages, policy, resumes,
            idleVerified, arrivalVerified = false, routeQualified = false });
        return new(state, reason, [report]);
    }
}
