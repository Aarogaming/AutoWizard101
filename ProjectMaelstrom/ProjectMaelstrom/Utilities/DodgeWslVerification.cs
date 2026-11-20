using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>A bounded visual Dodge test in a private WSL display, not the Windows desktop.</summary>
internal static class DodgeWslVerification
{
    private const int ActionLimit = 20;
    private const int RequestLimit = 120;
    private const int RunSeconds = 28;
    internal static string? AuditDigest(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }
    internal static string WslPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.Length < 4 || !char.IsAsciiLetter(full[0]) || full[1] != ':' || full[2] != '\\')
            throw new InvalidOperationException("The WSL test needs a local drive mounted under /mnt.");
        return "/mnt/" + char.ToLowerInvariant(full[0]) + "/" + full[3..].Replace('\\', '/');
    }

    internal static string FinalStopReason(string stop, int actions, int requests, int actionLimit = ActionLimit, int requestLimit = RequestLimit) =>
        stop != "time-limit" ? stop : actions >= actionLimit ? "action-limit" : requests >= requestLimit ? "observation-limit" : stop;

    internal static bool LostPlayer(bool seenPlayer, int consecutiveMisses) => seenPlayer && consecutiveMisses >= 3;
    internal static object DecisionRecord(string advice, string baseline, DodgeAction? action, double captureMs, DodgeObservation state, IReadOnlyList<DodgeThreatMotion> tracked, bool observationFresh, DodgeExplorationPolicy.Choice? exploration = null, DodgeOutcomePolicy.Choice? outcomeDiagnostics = null)
    {
        if (action == null && advice == "Hold" && observationFresh)
            return new { advice, baseline_advice = baseline, policy_override = advice != baseline, action = action?.Id, capture_ms = captureMs, observation_fresh = true, state = state.State, before_player = state.Player, threats = tracked.Select(t => new { position = t.Position, velocity_px_s = t.VelocityPxPerSecond, velocity_known = t.VelocityKnown }).ToArray(), exploration, outcome_diagnostics = outcomeDiagnostics };
        return new { advice, baseline_advice = baseline, policy_override = advice != baseline, action = action?.Id, capture_ms = captureMs, exploration, outcome_diagnostics = outcomeDiagnostics };
    }
    internal static string SurvivalObservation(string stop, bool seenPlaying) => !seenPlaying ? "not-observed" : stop == "game-over" ? "game-over-observed" : stop == "score-goal" ? "score-goal-observed" : "censored-at-stop";
    internal static bool ShouldReobserve(string policy, string state, int attempts, bool stepped = false) => (stepped || policy == "guarded-v4") && state == "unknown" && attempts < 2;
    internal static bool ShouldStopAtScoreGoal(bool benchmark, int? score, bool? alive, string state) =>
        !benchmark && score >= 50 && alive == true && state == "playing";
    internal static bool ShouldReuseAfterFrame(bool stepped, string policy, string state, string outcome) =>
        stepped && (policy is "center-continuous-v16" or "center-continuous-track-v17") && state == "playing" && outcome == "movement-observed";
    internal static bool ShouldRetryStaleObservation(bool stepped, double age, int attempts) =>
        stepped && double.IsFinite(age) && age >= 0 && attempts < 2;

    internal static bool ValidTrainingSpeed(bool stepped, double speed) =>
        stepped ? speed is 0.25 or 0.5 or 1.0 : speed == 1.0;
    internal static bool ValidResearchMode(string policy, bool stepped, bool endurance, double speed) =>
        policy is not ("directional-outcome-v34" or "directional-explore-v35" or "directional-outcome-turn-v36" or "directional-outcome-reference-v37" or "directional-outcome-escape-v38") || (stepped && endurance && speed == 0.25);

    public static async Task<string> RunAsync(string workspace, string packPath, CancellationToken token, int? seed = null, bool stepped = false, double speed = 1.0, bool endurance = false, bool benchmark = false)
    {
        if (!ValidTrainingSpeed(stepped, speed)) throw new ArgumentOutOfRangeException(nameof(speed));
        if (endurance && !stepped) throw new ArgumentException("Endurance training requires a controlled private game.");
        if (benchmark && !endurance) throw new ArgumentException("A complete-game benchmark requires endurance mode.");
        workspace = Path.GetFullPath(workspace);
        var gameSeed = seed ?? Random.Shared.Next(1, int.MaxValue);
        if (gameSeed < 1) throw new ArgumentOutOfRangeException(nameof(seed));
        var pack = DodgeLivePack.Load(packPath);
        var context = pack.Context ?? throw new InvalidDataException("Choose a context-aware Dodge pack for the isolated visual test.");
        if (!ValidResearchMode(context.AvoidancePolicy, stepped, endurance, speed))
            throw new InvalidOperationException("Experimental outcome and exploration policies require private 25% stepped endurance mode.");
        int? explorationSeed = context.AvoidancePolicy == "directional-explore-v35" ? System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue) : null;
        var explorationRandom = explorationSeed is int randomSeed ? new Random(randomSeed) : null;
        foreach (var name in new[] { "tools/wsl_dodge_launch.sh", "tools/wsl_dodge_session.sh", "tools/wsl_dodge_guest.py", ".local/wsl-dodge-game-check/project.godot", ".local/wsl-dodge-tools/godot/Godot_v4.7.2-stable_linux.x86_64", ".local/wsl-dodge-tools/root-current/usr/bin/Xvfb" })
            if (!File.Exists(Path.Combine(workspace, name.Replace('/', Path.DirectorySeparatorChar))))
                throw new FileNotFoundException("An optional isolated Dodge test component is missing.", name);
        if (stepped && !File.Exists(Path.Combine(workspace, ".local", "wsl-dodge-step-game", "maelstrom_step_controller.gd")))
            throw new FileNotFoundException("Prepare the optional stepped Dodge test copy first using scripts/prepare_wsl_step_game.ps1.");
        var runId = "wsl-dodge-" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(workspace, ".local", "wsl-dodge-runs", runId);
        Directory.CreateDirectory(directory);
        var linuxWorkspace = WslPath(workspace);
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "wsl.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var value in new[] { "-d", "Ubuntu", "--exec", "timeout", endurance ? "1230" : stepped ? "70" : "40", "sh", linuxWorkspace + "/tools/wsl_dodge_launch.sh", linuxWorkspace, WslPath(directory), gameSeed.ToString(System.Globalization.CultureInfo.InvariantCulture), stepped ? "stepped" : "realtime", speed.ToString(System.Globalization.CultureInfo.InvariantCulture), endurance ? "endurance" : "standard" })
            info.ArgumentList.Add(value);
        using var ownedJob = new OwnedProcessJob();
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start the isolated WSL test.");
        try { ownedJob.Attach(process); }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        var stderr = process.StandardError.ReadToEndAsync();
        var stopwatch = Stopwatch.StartNew();
        var runSeconds = endurance ? 1200 : stepped ? 55 : RunSeconds;
        var actionLimit = endurance ? 2400 : stepped ? 40 : ActionLimit;
        var requestLimit = endurance ? 9600 : stepped ? 180 : RequestLimit;
        var actions = new List<object>();
        var decisions = new List<object>();
        var stop = "time-limit";
        var requests = 0;
        var tracker = new DodgeMotionTracker(predictive: context.AvoidancePolicy is "center-track-v15" or "center-continuous-track-v17", guardVelocity: context.AvoidancePolicy is "velocity-guard-v23" or "directional-guard-v27", carryUncertainVelocity: context.AvoidancePolicy == "directional-carry-v28", smoothVelocity: context.AvoidancePolicy == "directional-smooth-v29");
        var alternateTracker = context.AvoidancePolicy == "center-consensus-v18" ? new DodgeMotionTracker(predictive: true) : null;
        var stateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var identityScore = 0;
        var ownedSession = false; var viewportMatched = false; var visualMatched = false;
        var seenPlayer = false;
        var consecutiveMisses = 0;
        var consecutiveStale = 0; var staleReobservations = 0;
        double? lastStaleAgeMs = null;
        string? lastUnknownFrame = null;
        string? previousAdvice = null;
        double? playingStartedMs = null, lastPlayingObservationMs = null;
        int? observedScore = null; bool? observedAlive = null;
        string? goalFrame = null;
        Bitmap? lastPlayingFrame = null;
        PrivateFrame? pendingFrame = null;
        string? lastPlayingName = null;
        void RememberPlayingFrame(Bitmap image)
        {
            if (!endurance) return;
            var replacement = new Bitmap(image);
            lastPlayingFrame?.Dispose();
            lastPlayingFrame = replacement;
        }
        void SaveLastPlayingFrame()
        {
            if (lastPlayingFrame == null || lastPlayingName != null) return;
            lastPlayingName = "last-playing.bmp";
            lastPlayingFrame.Save(Path.Combine(directory, lastPlayingName), System.Drawing.Imaging.ImageFormat.Bmp);
        }
        try
        {
            using var ready = await ReadResponseAsync(process, token, endurance);
            if (ready.RootElement.GetProperty("kind").GetString() != "ready") throw new InvalidDataException("Private display did not become ready.");
            if (ready.RootElement.GetProperty("mode").GetString() != (stepped ? "stepped" : "realtime") || ready.RootElement.GetProperty("speed").GetDouble() != speed)
                throw new InvalidDataException("Private display started with the wrong training settings.");
            ownedSession = true;
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(runSeconds) && actions.Count < actionLimit && requests < requestLimit)
            {
                var observationStartedMs = stopwatch.Elapsed.TotalMilliseconds;
                var observationReused = pendingFrame != null;
                using var before = pendingFrame ?? await FrameAsync(process, directory, requests++, new { kind = "observe" }, token, stepped, endurance);
                var observationWaitMs = stopwatch.Elapsed.TotalMilliseconds - observationStartedMs;
                pendingFrame = null;
                var state = DodgeSpecialist.Analyze(before.Image, context);
                if (state.State == "playing" && state.Player != null) RememberPlayingFrame(before.Image);
                if (endurance)
                {
                    (observedScore, observedAlive) = ReadEvaluation(directory, before.CaptureMs, speed);
                    // The private game's score is used only to stop and verify the benchmark;
                    // the action policy receives visual observations, never this value.
                    if (ShouldStopAtScoreGoal(benchmark, observedScore, observedAlive, state.State))
                    {
                        goalFrame = "score-goal.bmp";
                        File.Copy(Path.Combine(directory, "frame.bmp"), Path.Combine(directory, goalFrame));
                        lastPlayingObservationMs = before.CaptureMs;
                        stop = "score-goal";
                        break;
                    }
                }
                stateCounts[state.State] = stateCounts.GetValueOrDefault(state.State) + 1;
                if (state.State == "playing") playingStartedMs ??= before.CaptureMs;
                if (playingStartedMs != null) lastPlayingObservationMs = before.CaptureMs;
                viewportMatched = before.Image.Width == context.ViewportWidth && before.Image.Height == context.ViewportHeight;
                visualMatched = state.State != "unknown";
                identityScore = context.IdentityScore(ownedSession, viewportMatched, visualMatched);
                if (state.State == "game_over" && identityScore >= context.IdentityThreshold)
                {
                    SaveLastPlayingFrame();
                    lastUnknownFrame = "game-over.bmp";
                    File.Copy(Path.Combine(directory, "frame.bmp"), Path.Combine(directory, lastUnknownFrame));
                    stop = "game-over";
                    break;
                }
                if (state.State == "menu" && seenPlayer) { stop = "menu-after-play"; break; }
                if (state.State is "menu" or "ready") { await Task.Delay(stepped ? 10 : 250, token); continue; }
                if (identityScore < context.IdentityThreshold && state.State != "unknown") { stop = "identity-insufficient"; break; }
                if (state.Player == null)
                {
                    consecutiveMisses++;
                    if (LostPlayer(seenPlayer, consecutiveMisses))
                    {
                        SaveLastPlayingFrame();
                        lastUnknownFrame = "last-unrecognized.bmp";
                        File.Copy(Path.Combine(directory, "frame.bmp"), Path.Combine(directory, lastUnknownFrame));
                        stop = "player-not-recognized";
                        break;
                    }
                    await Task.Delay(stepped ? 10 : 250, token);
                    continue;
                }
                seenPlayer = true;
                consecutiveMisses = 0;
                if (state.State != "playing" || identityScore < context.IdentityThreshold) { stop = "identity-insufficient"; break; }
                var tracked = tracker.Update(state.Threats, before.CaptureMs, context);
                if (alternateTracker != null)
                    tracked = DodgeMotionConsensus.Merge(tracked, alternateTracker.Update(state.Threats, before.CaptureMs, context));
                if (!tracker.HasHistory) { await Task.Delay(stepped ? 10 : 150, token); continue; }
                var baselineAdvice = DodgeMotionAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs);
                var exploration = explorationRandom == null ? null : DodgeExplorationPolicy.Select(state.Player.Value, tracked, context, pack.NominalDurationMs, explorationRandom);
                var outcomeDiagnostics = context.AvoidancePolicy is "directional-outcome-v34" or "directional-outcome-turn-v36" or "directional-outcome-reference-v37" or "directional-outcome-escape-v38" ? pack.OutcomePolicy!.Decide(state.Player.Value, tracked, context, pack.NominalDurationMs, turnAwareFallback: context.AvoidancePolicy is "directional-outcome-turn-v36" or "directional-outcome-reference-v37" or "directional-outcome-escape-v38", retainSafeReference: context.AvoidancePolicy is "directional-outcome-reference-v37" or "directional-outcome-escape-v38", earlyEscape: context.AvoidancePolicy == "directional-outcome-escape-v38") : null;
                var advice = context.AvoidancePolicy switch
                {
                    "resilient-v2" => DodgeResilientAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs, previousAdvice),
                    "guarded-v3" => DodgeGuardedAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs, previousAdvice, baselineAdvice),
                    "guarded-v4" => DodgeGuardedAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs, previousAdvice, baselineAdvice, minimumGain: 2),
                    "geometry-v5" => DodgeMotionAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs, collisionAware: true),
                    "edge-aware-v6" => DodgeMotionAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs, collisionAware: true, edgeAware: true),
                    "safe-edge-v7" => DodgeSafeEdgeAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "diagonal-v8" => DodgeMotionAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs, collisionAware: true, diagonal: true),
                    "diagonal-v9" => DodgeMotionAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs, collisionAware: true, diagonal: true, edgePenalty: .25),
                    "lookahead-v10" => DodgeLookaheadAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "center-guard-v11" => DodgeCenterGuardAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "center-first-v12" => DodgeCenterFirstAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "adaptive-center-v13" => DodgeAdaptiveCenterAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "trajectory-v14" => DodgeTrajectoryAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "center-track-v15" => DodgeCenterFirstAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "center-continuous-v16" => DodgeCenterFirstAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "center-continuous-track-v17" => DodgeCenterFirstAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "center-consensus-v18" => DodgeCenterFirstAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "center-anticipate-v19" => DodgeCenterFirstAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs, horizonSeconds: .7, safeClearance: 30),
                    "center-configurable-v20" => DodgeCenterFirstAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs, horizonSeconds: context.HorizonMs / 1000.0),
                    "adaptive-horizon-v21" => DodgeAdaptiveHorizonAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "rollout-v22" => DodgeRolloutAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "velocity-guard-v23" => DodgeCenterFirstAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "edge-fallback-v24" => DodgeCenterFirstAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs, edgeAwareFallback: true),
                    "guarded-edge-v25" => DodgeGuardedEdgeFallback.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "directional-v26" => DodgeDirectionalAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "directional-guard-v27" => DodgeDirectionalAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "directional-carry-v28" => DodgeDirectionalAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "directional-smooth-v29" => DodgeDirectionalAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "directional-configurable-v30" => DodgeDirectionalAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs, configurable: true),
                    "directional-two-phase-v31" => DodgeTwoPhaseAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "directional-pulse-v32" => DodgeTwoPhaseAvoidance.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs, pulseTiming: true),
                    "directional-learned-v33" => pack.LearnedPolicy!.Recommend(state.Player.Value, tracked, context, pack.NominalDurationMs),
                    "directional-outcome-v34" => outcomeDiagnostics!.Advice,
                    "directional-outcome-turn-v36" => outcomeDiagnostics!.Advice,
                    "directional-outcome-reference-v37" => outcomeDiagnostics!.Advice,
                    "directional-outcome-escape-v38" => outcomeDiagnostics!.Advice,
                    "directional-explore-v35" => exploration!.Advice,
                    _ => baselineAdvice
                };
                var action = pack.Resolve(advice);
                var freshHold = action == null && advice == "Hold" && DodgeLiveRun.IsFreshCapture(before.RequestedMs, FrameTimeline.TimestampMs(), context.MaxObservationAgeMs);
                decisions.Add(DecisionRecord(advice, baselineAdvice, action, before.CaptureMs, state, tracked, freshHold, exploration, outcomeDiagnostics));
                if (action == null) { await Task.Delay(stepped ? 10 : 250, token); continue; }
                if (stopwatch.Elapsed >= TimeSpan.FromSeconds(runSeconds) || requests >= requestLimit) break;
                var frameAge = FrameTimeline.TimestampMs() - before.RequestedMs;
                if (!double.IsFinite(frameAge) || frameAge < 0 || frameAge > context.MaxObservationAgeMs)
                {
                    lastStaleAgeMs = double.IsFinite(frameAge) ? frameAge : null;
                    if (ShouldRetryStaleObservation(stepped, frameAge, consecutiveStale))
                    {
                        consecutiveStale++; staleReobservations++;
                        continue; // Withhold input and obtain a fresh stepped frame.
                    }
                    stop = "stale-observation"; break;
                }
                consecutiveStale = 0;
                var beforeName = $"before-{actions.Count:D2}.bmp";
                var saveFrame = !endurance || actions.Count % 10 == 0;
                if (saveFrame) File.Copy(Path.Combine(directory, "frame.bmp"), Path.Combine(directory, beforeName));
                var actionStartedMs = stopwatch.Elapsed.TotalMilliseconds;
                var decisionWorkMs = actionStartedMs - observationStartedMs - observationWaitMs;
                var after = await FrameAsync(process, directory, requests++, new { kind = "action", key = action.Key, duration_ms = action.DurationMs }, token, stepped, endurance);
                var actionWaitMs = stopwatch.Elapsed.TotalMilliseconds - actionStartedMs;
                try
                {
                    var afterState = DodgeSpecialist.Analyze(after.Image, context);
                    if (endurance) (observedScore, observedAlive) = ReadEvaluation(directory, after.CaptureMs, speed);
                    var reobservations = 0;
                    var uncertainFrames = new List<string>();
                    while (ShouldReobserve(context.AvoidancePolicy, afterState.State, reobservations, stepped) && requests < requestLimit && stopwatch.Elapsed < TimeSpan.FromSeconds(runSeconds))
                    {
                        stateCounts[afterState.State] = stateCounts.GetValueOrDefault(afterState.State) + 1;
                        var uncertainName = $"uncertain-{actions.Count:D2}-{reobservations:D2}.bmp";
                        File.Copy(Path.Combine(directory, "frame.bmp"), Path.Combine(directory, uncertainName));
                        uncertainFrames.Add(uncertainName);
                        await Task.Delay(stepped ? 10 : 100, token);
                        var next = await FrameAsync(process, directory, requests++, new { kind = "observe" }, token, stepped, endurance);
                        after.Dispose();
                        after = next;
                        afterState = DodgeSpecialist.Analyze(after.Image, context);
                        if (endurance) (observedScore, observedAlive) = ReadEvaluation(directory, after.CaptureMs, speed);
                        reobservations++;
                    }
                    var outcome = DodgeLiveRun.ClassifyMovement(state.Player, afterState.Player, action.Id, before.Image.Size);
                    while (endurance && outcome == "movement-not-observed" && afterState.State == "playing" && reobservations < 2 && requests < requestLimit && stopwatch.Elapsed < TimeSpan.FromSeconds(runSeconds))
                    {
                        var uncertainName = $"motion-uncertain-{actions.Count:D2}-{reobservations:D2}.bmp";
                        File.Copy(Path.Combine(directory, "frame.bmp"), Path.Combine(directory, uncertainName));
                        uncertainFrames.Add(uncertainName);
                        var next = await FrameAsync(process, directory, requests++, new { kind = "observe" }, token, stepped, endurance);
                        after.Dispose();
                        after = next;
                        afterState = DodgeSpecialist.Analyze(after.Image, context);
                        (observedScore, observedAlive) = ReadEvaluation(directory, after.CaptureMs, speed);
                        outcome = DodgeLiveRun.ClassifyMovement(state.Player, afterState.Player, action.Id, before.Image.Size);
                        reobservations++;
                    }
                    if (playingStartedMs != null) lastPlayingObservationMs = after.CaptureMs;
                    if (afterState.State == "playing" && afterState.Player != null) RememberPlayingFrame(after.Image);
                    else if (afterState.State == "game_over") SaveLastPlayingFrame();
                    stateCounts[afterState.State] = stateCounts.GetValueOrDefault(afterState.State) + 1;
                    if (!saveFrame && (outcome != "movement-observed" || afterState.State != "playing"))
                    {
                        before.Image.Save(Path.Combine(directory, beforeName), System.Drawing.Imaging.ImageFormat.Bmp);
                        saveFrame = true;
                    }
                    var afterName = $"after-{actions.Count:D2}.bmp";
                    var saveAfter = saveFrame || outcome != "movement-observed" || afterState.State != "playing";
                    if (saveAfter) File.Copy(Path.Combine(directory, "frame.bmp"), Path.Combine(directory, afterName));
                    actions.Add(new { action = action.Id, action.Key, action.DurationMs, outcome, state = state.State, after_state = afterState.State, reobservations, uncertain_frames = uncertainFrames, advice, baseline_advice = baselineAdvice, policy_override = advice != baselineAdvice, exploration, outcome_diagnostics = outcomeDiagnostics, tracked_threats = tracked.Count(t => t.VelocityKnown), unknown_velocity_threats = tracked.Count(t => !t.VelocityKnown), threats = tracked.Select(t => new { position = t.Position, velocity_px_s = t.VelocityPxPerSecond, velocity_known = t.VelocityKnown }).ToArray(), frame_age_ms = frameAge, observation_reused = observationReused, observation_wait_wall_ms = observationWaitMs, decision_work_wall_ms = decisionWorkMs, action_wait_wall_ms = actionWaitMs, before_capture_ms = before.CaptureMs, after_capture_ms = after.CaptureMs, before_player = state.Player, after_player = afterState.Player, before_file = saveFrame ? beforeName : null, after_file = saveAfter ? afterName : null });
                    previousAdvice = advice;
                    if (afterState.State == "game_over") { stop = "game-over"; break; }
                    if (outcome != "movement-observed") { stop = outcome; break; }
                    if (afterState.State != "playing") { stop = "state-not-playing"; break; }
                    if (ShouldReuseAfterFrame(stepped, context.AvoidancePolicy, afterState.State, outcome))
                        pendingFrame = new PrivateFrame(new Bitmap(after.Image), after.CaptureMs, after.RequestedMs);
                    else
                    {
                        tracker.Update(afterState.Threats, after.CaptureMs, context);
                        alternateTracker?.Update(afterState.Threats, after.CaptureMs, context);
                    }
                }
                finally { after.Dispose(); }
                await Task.Delay(stepped ? 10 : 100, token);
            }
            stop = FinalStopReason(stop, actions.Count, requests, actionLimit, requestLimit);
        }
        catch (OperationCanceledException) { stop = token.IsCancellationRequested ? "cancelled" : "response-timeout"; }
        catch (Exception ex) { stop = "error: " + ex.Message; }
        finally
        {
            try { await process.StandardInput.WriteLineAsync("{\"id\":5000,\"kind\":\"stop\"}"); process.StandardInput.Close(); } catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException) { }
            try { using var close = new CancellationTokenSource(endurance ? 15000 : 3000); await process.WaitForExitAsync(close.Token); }
            catch (OperationCanceledException) { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            lastPlayingFrame?.Dispose();
            pendingFrame?.Dispose();
        }
        var errorText = await stderr;
        var observedPlaySeconds = playingStartedMs != null && lastPlayingObservationMs >= playingStartedMs ? (lastPlayingObservationMs - playingStartedMs) / 1000 : null;
        var auditSha256 = endurance ? AuditDigest(Path.Combine(directory, "evaluation-audit.jsonl")) : null;
        var report = new { schema_version = 1, run_id = runId, mode = endurance ? "wsl-private-endurance" : "wsl-private-visual", training = new { mode = stepped ? "stepped" : "realtime", speed, fixed_fps = stepped ? 30 : (int?)null, clock = stepped ? "simulated" : "monotonic", action_limit = actionLimit, request_limit = requestLimit, time_limit_seconds = runSeconds, benchmark, observation_cadence = stepped && (context.AvoidancePolicy is "center-continuous-v16" or "center-continuous-track-v17") ? "reuse-action-frame" : "advance-before-decision" }, observation_source = "private-x11-framebuffer", input_source = "private-x11-keyboard", seed = gameSeed, policy = context.AvoidancePolicy, exploration_seed = explorationSeed, pack = new { pack.Id, pack.Version, pack.Sha256 }, goal = context.Goal, evaluation = endurance ? new { score = observedScore, alive = observedAlive, target_score = benchmark ? (int?)null : 50, goal_frame = goalFrame, source = "private-game-evaluator-not-policy", evidence = "Every tenth action plus uncertain and terminal frames is saved." } : null, evaluation_audit_sha256 = auditSha256, identification = new { score = identityScore, threshold = context.IdentityThreshold, owned_session = ownedSession, viewport = viewportMatched, visual_state = visualMatched, note = "Rubric match is not authentication or proof of identity." }, state_counts = stateCounts, elapsed_ms = stopwatch.Elapsed.TotalMilliseconds, observed_play_seconds = observedPlaySeconds, survival_observation = SurvivalObservation(stop, playingStartedMs != null), stop_reason = stop, action_count = actions.Count, stale_reobservations = staleReobservations, last_stale_age_ms = lastStaleAgeMs, last_unrecognized_frame = lastUnknownFrame, last_playing_frame = lastPlayingName, decisions, actions, guest_stderr = errorText.Length > 4096 ? errorText[..4096] : errorText, note = "Play time begins at first recognized playing frame. Private Linux test display; does not verify Windows desktop capture or arbitrary games." };
        var path = Path.Combine(directory, "report.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static async Task<JsonDocument> ReadResponseAsync(Process process, CancellationToken token, bool endurance)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(endurance ? 90 : 8));
        var line = await process.StandardOutput.ReadLineAsync(deadline.Token);
        if (line == null || line.Length > 4096) throw new IOException("Private display closed or returned an invalid response.");
        var json = JsonDocument.Parse(line);
        if (json.RootElement.GetProperty("kind").GetString() == "error")
        { var message = json.RootElement.GetProperty("message").GetString(); json.Dispose(); throw new IOException("Private display: " + message); }
        return json;
    }

    private sealed record PrivateFrame(Bitmap Image, double CaptureMs, double RequestedMs) : IDisposable { public void Dispose() => Image.Dispose(); }

    internal static (int Score, bool Alive) ReadEvaluation(string directory, double captureMs, double speed)
    {
        using var stream = OpenEvaluation(directory);
        using var report = JsonDocument.Parse(stream);
        var root = report.RootElement;
        var score = root.GetProperty("score").GetInt32();
        var alive = root.GetProperty("alive").GetBoolean();
        var ticks = root.GetProperty("ticks").GetInt64();
        if (score < 0 || score > 1000 || ticks < 0 || Math.Abs(ticks * 1000.0 / 30 * speed - captureMs) > 1)
            throw new InvalidDataException("Private score evaluator did not match the current frame.");
        return (score, alive);
    }

    internal static FileStream OpenEvaluation(string directory) => new(Path.Combine(directory, "evaluation.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static async Task<PrivateFrame> FrameAsync(Process process, string directory, int id, object command, CancellationToken token, bool stepped, bool endurance)
    {
        var requestedMs = FrameTimeline.TimestampMs();
        var fields = JsonSerializer.SerializeToElement(command);
        var request = new Dictionary<string, object?> { ["id"] = id };
        foreach (var property in fields.EnumerateObject()) request[property.Name] = property.Value;
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request));
        using var response = await ReadResponseAsync(process, token, endurance);
        // The game is paused after rendering a stepped frame; time spent advancing it is not frame age.
        if (stepped) requestedMs = FrameTimeline.TimestampMs();
        var root = response.RootElement;
        if (root.GetProperty("kind").GetString() != "frame" || root.GetProperty("id").GetInt32() != id || root.GetProperty("frame").GetString() != "frame.bmp")
            throw new InvalidDataException("Unexpected private-display frame response.");
        var capturedMs = root.GetProperty("capture_ms").GetDouble();
        if (!double.IsFinite(capturedMs) || capturedMs < 0) throw new InvalidDataException("Invalid private-display timestamp.");
        var path = Path.Combine(directory, "frame.bmp");
        if (new FileInfo(path).Length != 54 + 480 * 720 * 4) throw new InvalidDataException("Invalid private-display frame size.");
        using var stream = File.OpenRead(path);
        using var source = new Bitmap(stream);
        return new PrivateFrame(new Bitmap(source), capturedMs, requestedMs);
    }
}
