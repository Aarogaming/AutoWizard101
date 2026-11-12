namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceNativeIdleResult(bool Confirmed, bool InputReleased, bool CaptureStopped, string Reason, string Report);

/// <summary>A bounded postgame player operation under the existing shared lease.
/// Shared modules own postgame transitions and input receipts. Local menu OCR
/// runs at most twice per second; every transition is reobserved.
/// Its activity baseline is inherited from gameplay and can never be rearmed.</summary>
internal static class PetDanceNativeIdle
{
    internal static async Task<PetDanceNativeIdleResult> RunWithFailsafeAsync(OperationContext context,
        GameWindow target, Size client, string output, DesktopActivityMonitor monitor, long gameplayBaseline,
        nuint tag, Func<bool> heartbeat, Func<bool>? controlPermission, CancellationToken token,
        WizwalkerSession? clientSession = null, WizwalkerLaunch? clientLaunch = null)
    {
        var sink = new WindowsDodgeKeySink(tag);
        bool Ready() => !token.IsCancellationRequested && monitor.Installed && !monitor.EscapeRequested &&
            monitor.Revision == gameplayBaseline && heartbeat() && sink.Ready(target, client) &&
            (controlPermission?.Invoke() ?? true);
        var result = await W101IdleFailsafe.RunAsync(context, new(), Ready, async (index, cancellation) =>
        {
            var directory = Path.Combine(output, "idle-finalizer-" + index);
            if (Directory.Exists(directory)) throw new IOException("Idle evidence already exists.");
            Directory.CreateDirectory(directory);
            var child = context with { Directory = directory, Request = context.Request with
            { Id = Guid.NewGuid().ToString("N"), TimeoutSeconds = 20 } };
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            var attempt = await RunAsync(child, target, client, directory, monitor, gameplayBaseline, tag,
                heartbeat, controlPermission, deadline.Token, clientSession, clientLaunch).ConfigureAwait(false);
            return new OperationOutcome(!attempt.InputReleased || !attempt.CaptureStopped ? OperationState.CleanupFailed :
                attempt.Confirmed ? OperationState.Completed : OperationState.Failed, attempt.Reason, [attempt.Report]);
        }, null, token).ConfigureAwait(false);
        var report = Path.Combine(output, "native-idle-failsafe.json");
        var confirmed = result.Outcome?.State == OperationState.Completed && result.SkippedReason is null;
        var clean = context.Input?.RecoveryRequired != true;
        GameExperiments.WriteAtomic(report, new { confirmed, cleanupConfirmed = clean, result });
        return new(confirmed, clean, clean, result.SkippedReason ?? result.Outcome?.Message ?? "Idle not attempted", report);
    }

    public static async Task<PetDanceNativeIdleResult> RunAsync(OperationContext context, GameWindow target, Size client, string output,
        DesktopActivityMonitor monitor, long gameplayBaseline, nuint tag, Func<bool> heartbeat,
        Func<bool>? controlPermission, CancellationToken token, WizwalkerSession? clientSession = null, WizwalkerLaunch? clientLaunch = null)
    {
        var report = Path.Combine(output, "native-idle-operation.json");
        var entries = new List<object>(); var released = true; var confirmed = false; var reason = "postgame-deadline-exceeded";
        var rejectedFrames = new Dictionary<string, int>(StringComparer.Ordinal);
        var rejectionSamples = new List<PetDancePostGameFrameAdmission>();
        var cadence = new VisualTransitionCadence();
        var readAttempts = 0; var admittedFrames = 0; var alignedFrames = 0;
        OperationModuleBundle? bundle = null; RoutineBinding? binding = null; ModuleScreenSession? modules = null;
        var moduleReceipts = 0;
        WindowObserver? observer = null; var captureStopped = true;
        var sink = new WindowsDodgeKeySink(tag);
        bool Ready() => !token.IsCancellationRequested && monitor.Installed && !monitor.EscapeRequested &&
            monitor.Revision == gameplayBaseline && heartbeat() && context.Request.WritesGame && !context.Request.DelegatedInput &&
            context.Request.Target == ModuleOwnedInput.TargetId(target) && context.Input is { CanDispatch: true } &&
            ReferenceEquals(context.Input, GameInputLease.Current) &&
            sink.Ready(target, client) && (controlPermission?.Invoke() ?? true);
        try
        {
            if (!Ready()) throw new InvalidOperationException("Postgame continuation lost desktop ownership.");
            var ocr = new PetDanceMenuOcr();
            observer = new WindowObserver(target); captureStopped = false;
            var geometry = new Size(observer.Width, observer.Height);
            bundle = W101ModulePrograms.Postgame();
            binding = bundle.Bind(ModuleOwnedInput.TargetId(target),
                FormattableString.Invariant($"{geometry.Width}x{geometry.Height}"), context.Request.Id);
            var policy = W101ModulePrograms.Policy();
            var clientLoading = clientSession is null ? null : new WizwalkerModuleLoading(context, clientSession,
                clientLaunch ?? throw new InvalidDataException("Client loading requires its reviewed adapter paths."), binding);
            modules = new ModuleScreenSession(bundle, binding, geometry, policy, context.Request.Id + ".postgame",
                clientLoading is null ? null : WizwalkerModuleLoading.Requirement);
            var input = ModuleOwnedInput.ForPetDance(context, binding, geometry, target, client, tag, Ready, FrameTimeline.TimestampMs);
            var start = FrameTimeline.TimestampMs(); double previousCapture = -1;
            while (FrameTimeline.TimestampMs() - start < 45000)
            {
                if (!Ready()) { reason = "desktop-activity-or-target-change"; break; }
                // Take the bounded client read before capture, so it cannot make
                // the image stale. Every read disconnects before any UI action.
                var loading = clientLoading is null ? null : await clientLoading.ReadAsync(token).ConfigureAwait(false);
                if (!Ready()) { reason = "desktop-activity-or-target-change"; break; }
                readAttempts++;
                using var frame = await PetDanceObservationRead.ReadAsync(() => observer.ReadTimestampedAsync(latestOnly: true), token).ConfigureAwait(false);
                var admission = await PetDancePostGameFrameGate.InspectAsync(frame != null, frame?.CaptureTime?.TotalMilliseconds,
                    previousCapture, FrameTimeline.TimestampMs, Ready, token).ConfigureAwait(false);
                if (admission.AlignmentWaitMs > 0) alignedFrames++;
                if (!admission.Accepted)
                {
                    rejectedFrames[admission.Reason] = rejectedFrames.GetValueOrDefault(admission.Reason) + 1;
                    if (rejectionSamples.Count < 32) rejectionSamples.Add(admission);
                    if (admission.Reason is "cancelled" or "not-ready")
                    { reason = admission.Reason == "cancelled" ? "postgame-cancelled" : "desktop-activity-or-target-change"; break; }
                    var retry = cadence.Plan(FrameTimeline.TimestampMs(), freshFrame: false, token);
                    frame?.Dispose();
                    await Task.Delay(retry.DelayMs, token).ConfigureAwait(false);
                    continue;
                }
                if (frame == null || admission.CapturedMs is not { } captured)
                    throw new InvalidDataException("Postgame frame admission requires the original captured frame and timestamp.");
                admittedFrames++;
                previousCapture = captured;
                var cadencePlan = cadence.Plan(FrameTimeline.TimestampMs(), freshFrame: true, token);
                if (!cadencePlan.Analyze)
                {
                    frame.Dispose();
                    await Task.Delay(cadencePlan.DelayMs, token).ConfigureAwait(false);
                    continue;
                }
                if (!Ready()) { reason = "desktop-activity-or-target-change"; break; }
                byte[] encoded;
                using (var png = new MemoryStream())
                {
                    frame.Image.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                    encoded = png.ToArray();
                }
                var imageHash = RoutineReplay.Digest(encoded);
                var text = await ocr.ReadAsync(frame.Image, token).ConfigureAwait(false);
                var recognition = W101MenuRecognition.Classify(new(imageHash, captured, geometry, binding, "local-windows-ocr"), text);
                if (!Ready()) { reason = "desktop-activity-or-target-change"; break; }
                // This trusted continuation is entered only after gameplay allows
                // postgame work and confirms capture cleanup and released input.
                // Policy context facts describe that completed Dance visit; they
                // do not infer a home, world, route or clear movement area.
                var observedMs = FrameTimeline.TimestampMs();
                var observation = modules.Observe(recognition, observedMs,
                    new(TargetReady: true, ContextVerified: true, InputOwned: true, VerifiedFacts: policy.ContextFacts), loading);
                var plan = observation.Decision;
                entries.Add(new { captured_ms = captured, observed_ms = observedMs, scene = observation.Recognition.Scene,
                    recognition = observation.Recognition, stable = observation.Stable, plan, loading,
                    outcome_for_proposal_id = observation.OutcomeForProposalId,
                    alignment_wait_ms = admission.AlignmentWaitMs, age_before_ocr_ms = admission.AgeMs });
                if (plan.Completed)
                {
                    File.WriteAllBytes(Path.Combine(output, "idle-final.png"), encoded);
                    GameExperiments.WriteAtomic(Path.Combine(output, "idle-return.json"), new
                    {
                        confirmed_by = "player-operation-local-visual-recognition", captured_ms = captured,
                        sha256 = GameExperiments.Digest(Path.Combine(output, "idle-final.png")), autonomous_idle_recognition = true
                    });
                    confirmed = true; reason = "native-idle-observed"; break;
                }
                if (plan.Stopped) { reason = plan.Reason; break; }
                if (plan.IsProposal)
                {
                    var action = W101ModulePrograms.Resolve(plan.Action ?? throw new InvalidDataException("A module proposal needs its action binding."));
                    var priorReleased = released;
                    released = false;
                    var receipt = await input.DispatchAsync(observation, token).ConfigureAwait(false);
                    released = priorReleased && receipt.InputReleased;
                    moduleReceipts++;
                    modules.RecordReceipt(receipt);
                    entries.Add(new { action, action_id = plan.ActionId, receipt, module_status = modules.Status });
                    // Persist the exact analyzed PNG after key/button release so
                    // disk writes do not consume the pre-dispatch freshness budget.
                    File.WriteAllBytes(Path.Combine(output, "postgame-" + action + ".png"), encoded);
                    if (!receipt.Accepted || !receipt.InputReleased) { reason = "postgame-input-rejected: " + modules.Status.Reason; break; }
                }
                frame.Dispose();
                await Task.Delay(cadencePlan.DelayMs, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { reason = "postgame-cancelled-or-capture-timeout"; }
        catch (Exception error) { reason = "postgame-error: " + error.Message; }
        finally
        {
            if (observer != null)
            {
                try { await observer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); captureStopped = true; }
                catch (Exception error) { reason = "capture-cleanup-unconfirmed: " + error.Message; }
            }
        }
        GameExperiments.WriteAtomic(report, new { confirmed, input_released = released, capture_stopped = captureStopped, reason, entries,
            module_execution = new { schema_version = 1, bundle, binding, status = modules?.Status,
                proposals = modules?.Proposals ?? 0, receipts = moduleReceipts, completed_modules = modules?.CompletedModules ?? 0,
                context_facts_source = "trusted-completed-Dance-continuation-with-confirmed-gameplay-cleanup-and-input-release",
                recognition = "existing-local-menu-OCR", outcome_binding = "registered-newer-changed-screen-after-released-receipt" },
            frame_admission = new { read_attempts = readAttempts, admitted_frames = admittedFrames, aligned_frames = alignedFrames,
                maximum_age_ms = PetDancePostGameFrameGate.MaximumAgeMs, maximum_alignment_wait_ms = PetDancePostGameFrameGate.MaximumAlignmentWaitMs,
                rejections = rejectedFrames, rejection_samples = rejectionSamples, rejection_sample_limit = 32 },
            visual_cadence = new { minimum_analysis_interval_ms = VisualTransitionCadence.MinimumAnalysisIntervalMs,
                quick_retry_delay_ms = VisualTransitionCadence.QuickRetryDelayMs, maximum_quick_retries = VisualTransitionCadence.MaximumQuickRetries,
                maximum_reacquisition_ms = VisualTransitionCadence.MaximumReacquisitionMs, cooldown_ms = VisualTransitionCadence.CooldownMs,
                analysis_admissions = cadence.AnalysisCount, quick_retries = cadence.QuickRetryCount,
                cooldowns = cadence.CooldownCount, rate_limited_frames = cadence.RateLimitedCount },
            optional_client_loading = clientSession is not null,
            feeding_enabled = false, crown_shop_locked = true, focus_changed = false });
        return new(confirmed, released, captureStopped, reason, report);
    }

    internal static bool BoundsStable(RectangleF? before, RectangleF? after) => before == null && after == null ||
        before is { } first && after is { } second && Math.Abs(first.X - second.X) <= 8 && Math.Abs(first.Y - second.Y) <= 8 &&
        Math.Abs(first.Width - second.Width) <= 8 && Math.Abs(first.Height - second.Height) <= 8;
}
