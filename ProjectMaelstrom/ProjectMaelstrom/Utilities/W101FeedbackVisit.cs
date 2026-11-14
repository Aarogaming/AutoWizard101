namespace ProjectMaelstrom.Utilities;

/// <summary>At most three forward pulses under the caller's existing scope.
/// Every action uses the established input transport and independent scene checks.</summary>
internal static class W101FeedbackVisit
{
    /// <summary>Probe contract latches any missing/unconfirmed release on the
    /// shared lease. Do not retry the pulse; only permit a new pose observation.</summary>
    internal static async Task<bool> ResumeReleasedAsync(OperationContext owner, OperationOutcome outcome,
        Func<bool> ready, Func<CancellationToken, Task<bool>> waitForFocus, CancellationToken token)
    {
        if (token.IsCancellationRequested || outcome.State is OperationState.CleanupFailed or OperationState.Cancelled or OperationState.TimedOut ||
            owner.Input is not { CanDispatch: true } || !ReferenceEquals(owner.Input, GameInputLease.Current) || ready()) return false;
        return await waitForFocus(token).ConfigureAwait(false) && owner.Input.CanDispatch &&
            ReferenceEquals(owner.Input, GameInputLease.Current) && ready();
    }

    internal static async Task<OperationOutcome> RunAsync(OperationContext owner, WizwalkerTarget target,
        WizwalkerLaunch launch, W101ControlScope scope, W101NavigationProbeRuntime pulse,
        int distance, int maximumPulseMs, CancellationToken token)
    {
        var evidence = new List<object>();
        WaypointFeedback? controller = null;
        string reason = "observation-budget-exhausted";
        bool arrived = false;
        var path = Path.Combine(owner.Directory, "feedback-result.json");
        void Save() => GameExperiments.WriteAtomic(path, new { arrived, reason, coordinateEvidenceOnly = true, routeQualified = false, evidence });
        for (int i = 0; i < 6; i++)
        {
            token.ThrowIfCancellationRequested();
            if (!scope.Ready() && !await scope.WaitForFocusAsync(token).ConfigureAwait(false)) { reason = "control-changed"; break; }
            // This stage owns its directory; pulse observations live in separate
            // child directories. Keep indexes inside the owned reader's 0..31 budget.
            W101UiTreeResult sample;
            try { sample = await W101UiTreeOperation.ReadOwnedAsync(owner, target, launch, i, token, true).ConfigureAwait(false); }
            catch (InvalidOperationException) when (!scope.Ready() && owner.Input is { CanDispatch: true })
            {
                // The reader latches uncertain hook cleanup. Only clean focus
                // loss may wait; discard this observation and acquire a fresh one.
                if (!await scope.WaitForFocusAsync(token).ConfigureAwait(false)) { reason = "control-changed"; break; }
                evidence.Add(new { checkpoint = "observation-discarded-after-focus-loss" }); Save(); continue;
            }
            if (sample.Target != target || !sample.CleanupConfirmed || !sample.HookRestored || sample.Loading != false ||
                sample.PoseCandidate is not { Status: "candidate" } pose ||
                !W101UiTreeOperation.ValidPose(pose, true, DateTime.UtcNow.Ticks) ||
                new[] { "nativeIdle", "menu", "dialogue" }.Any(key => sample.States?.GetValueOrDefault(key) != false || sample.StableStates?.GetValueOrDefault(key) != false))
            { reason = "fresh-awake-pose-required"; break; }
            var current = new WaypointPose(pose.Position!.X, pose.Position.Y, pose.Position.Z, pose.YawRadians!.Value, pose.ObservedUtcTicks);
            if (controller is null)
            {
                var destination = current with { X = current.X - Math.Sin(current.Heading) * distance, Y = current.Y - Math.Cos(current.Heading) * distance };
                controller = new(destination, maximumPulseMs);
                GameExperiments.WriteAtomic(Path.Combine(owner.Directory, "feedback-plan.json"), new { current, destination, radius = 10, maximumMoves = 3 });
            }
            var decision = controller.Observe(current);
            evidence.Add(new { current, decision });
            Save();
            if (!scope.Ready())
            {
                if (!await scope.WaitForFocusAsync(token).ConfigureAwait(false)) { reason = "control-changed"; break; }
                // A plan made before the pause is never dispatched. Reobserve
                // against the same frozen destination on the next iteration.
                evidence.Add(new { checkpoint = "proposal-discarded-after-focus-loss" }); Save(); continue;
            }
            reason = decision.Reason;
            if (decision.State == "arrived") { arrived = true; break; }
            if (decision.State == "stopped") break;
            if (decision.State == "confirm") continue;
            var directory = Path.Combine(owner.Directory, "pulse-" + i);
            Directory.CreateDirectory(directory);
            var action = new ModuleAction("game.move-forward", "keyboard-pulse", "W", decision.PulseMs, "w101.navigation");
            var outcome = await W101NavigationProbe.RunOwnedAsync(owner with { Directory = directory }, target, action, pulse, token).ConfigureAwait(false);
            evidence.Add(new { outcome });
            Save();
            if (outcome.State != OperationState.Completed)
            {
                reason = outcome.Message;
                if (await ResumeReleasedAsync(owner, outcome, scope.Ready, scope.WaitForFocusAsync, token).ConfigureAwait(false))
                {
                    evidence.Add(new { checkpoint = "released-pulse-reobserve-after-focus-loss" }); Save(); continue;
                }
                break;
            }
        }
        Save();
        return new(arrived ? OperationState.Completed : OperationState.Failed, reason, [path]);
    }
}
