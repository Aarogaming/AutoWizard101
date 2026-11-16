namespace ProjectMaelstrom.Utilities;

/// <summary>One bounded turn-only stage under the existing scheduler/control
/// owner. No translation, route discovery, independent controller or Idle input.</summary>
internal static class W101HeadingVisit
{
    internal static async Task<OperationOutcome> RunAsync(OperationContext owner, WizwalkerTarget target,
        WizwalkerLaunch launch, W101ControlScope scope, W101NavigationProbeRuntime pulse,
        double heading, int maximumPulseMs, CancellationToken token)
    {
        if (!double.IsFinite(heading) || heading < 0 || heading > Math.PI * 2 || maximumPulseMs is < 20 or > 300)
            throw new ArgumentOutOfRangeException(nameof(heading), "Use a finite heading from zero through 2pi and a 20-300 ms pulse cap.");
        var evidence = new List<object>();
        var controller = new HeadingFeedback(heading, maximumPulseMs);
        var geometry = GameWindows.ClientSize(target.Window);
        var reason = "heading-observation-budget-exhausted";
        var state = OperationState.Failed;
        var arrived = false;
        var report = Path.Combine(owner.Directory, "heading-result.json");
        using var fallbackBudget = owner.FocusBudget is null ? new OperationFocusBudget() : null;
        using var deadline = (owner.FocusBudget ?? fallbackBudget!).CreateDeadline(TimeSpan.FromSeconds(70), token);
        var bounded = deadline.Token;
        bool Owned() => owner.Request.WritesGame && !owner.Request.DelegatedInput &&
            owner.Request.Target == ModuleOwnedInput.TargetId(target.Window) &&
            owner.Input is { CanDispatch: true } && ReferenceEquals(owner.Input, GameInputLease.Current);
        void Save() => GameExperiments.WriteAtomic(report, new { arrived, reason, heading, maximumPulseMs,
            maximumMoves = 4, maximumSeconds = 70, observedMoves = controller.Moves,
            arrivalToleranceRadians = 0.06, translationLimit = 20, coordinateEvidenceOnly = true,
            routeQualified = false, zoneVerified = false, evidence });
        try
        {
            for (int index = 0; index < 10; index++)
            {
                bounded.ThrowIfCancellationRequested();
                if (!Owned()) { reason = "heading-input-ownership-lost"; break; }
                if (!scope.Ready() && !await scope.WaitForFocusAsync(bounded).ConfigureAwait(false)) { reason = "control-changed"; break; }
                W101UiTreeResult sample;
                try { sample = await W101UiTreeOperation.ReadOwnedAsync(owner, target, launch, index, bounded, true).ConfigureAwait(false); }
                catch (InvalidOperationException) when (!scope.Ready() && Owned())
                {
                    if (!await scope.WaitForFocusAsync(bounded).ConfigureAwait(false)) { reason = "control-changed"; break; }
                    evidence.Add(new { checkpoint = "heading-observation-discarded-after-focus-loss" }); Save(); continue;
                }
                if (sample.Target != target || !sample.CleanupConfirmed || !sample.HookRestored || sample.Loading != false ||
                    GameWindows.ClientSize(target.Window) != geometry || sample.PoseCandidate is not { Status: "candidate" } pose ||
                    !W101UiTreeOperation.ValidPose(pose, true, DateTime.UtcNow.Ticks) ||
                    new[] { "nativeIdle", "menu", "dialogue" }.Any(key => sample.States?.GetValueOrDefault(key) != false ||
                        sample.StableStates?.GetValueOrDefault(key) != false))
                { reason = "fresh-awake-heading-pose-required"; break; }
                var current = new WaypointPose(pose.Position!.X, pose.Position.Y, pose.Position.Z, pose.YawRadians!.Value, pose.ObservedUtcTicks);
                var decision = controller.Observe(current, DateTime.UtcNow.Ticks);
                evidence.Add(new { current, decision }); reason = decision.Reason; Save();
                if (!Owned()) { reason = "heading-input-ownership-lost"; break; }
                if (!scope.Ready())
                {
                    if (decision.State == "turn") controller.DiscardProposal(decision);
                    if (!await scope.WaitForFocusAsync(bounded).ConfigureAwait(false)) { reason = "control-changed"; break; }
                    evidence.Add(new { checkpoint = "heading-proposal-discarded-after-focus-loss" }); Save(); continue;
                }
                if (decision.State == "arrived") { arrived = true; state = OperationState.Completed; break; }
                if (decision.State == "stopped") break;
                if (decision.State == "confirm") continue;
                var directory = Path.Combine(owner.Directory, "heading-pulse-" + index);
                Directory.CreateDirectory(directory);
                var action = new ModuleAction(decision.Key == "A" ? "game.turn-left" : "game.turn-right",
                    "keyboard-pulse", decision.Key!, decision.PulseMs, "w101.navigation");
                var outcome = await W101NavigationProbe.RunOwnedAsync(owner with { Directory = directory }, target, action, pulse, bounded).ConfigureAwait(false);
                controller.RecordTurn(decision, outcome.State == OperationState.Completed);
                evidence.Add(new { outcome }); Save();
                if (outcome.State != OperationState.Completed)
                {
                    reason = outcome.Message;
                    if (await W101FeedbackVisit.ResumeReleasedAsync(owner, outcome, scope.Ready, scope.WaitForFocusAsync, bounded).ConfigureAwait(false))
                    { evidence.Add(new { checkpoint = "heading-released-pulse-reobserve-after-focus-loss" }); Save(); continue; }
                    state = outcome.State; break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            state = token.IsCancellationRequested ? OperationState.Cancelled : OperationState.TimedOut;
            reason = token.IsCancellationRequested ? "heading-cancelled" : "heading-stage-deadline-exceeded";
        }
        finally { Save(); }
        return new(state, reason, [report]);
    }
}
