using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Trusted host bindings only, never delegates loaded from a game pack.
/// Read must map timestamps into the host clock and bind target/zone/route epoch.
/// Apply must enforce expiry and release independently of this loop. Owned checks
/// target/lease lifetime, not foreground; focus is carried in each observation.</summary>
internal sealed record W101PathRuntime(Func<bool> Owned, Func<double> Clock,
    Func<CancellationToken, Task<PathObservation>> Read,
    Func<PathMotion, CancellationToken, Task<PathMotionReceipt>> Apply,
    Func<CancellationToken, Task<bool>> Release,
    Func<string, CancellationToken, Task<bool>> VerifyPrompt,
    bool MovingReaderQualified = false,
    Func<NavigationRoute, CancellationToken, Task<NavigationArrivalResult>>? VerifyArrival = null);

internal static class W101PathSession
{
    /// <summary>Transport binding only: producer ID/order and target lifetime are
    /// checked before decoding. Wizard/zone-owner qualification remains separate.
    /// The owner must pin expected streamId from this run and retain lastSequence.
    /// Never use an incoming packet to choose its own expected identity.</summary>
    internal static PathObservation DecodeBoundStreamCandidate(string json, string runId, string streamId,
        long lastSequence, WizwalkerTarget target, string zone, string context, long receivedUtcTicks,
        double receivedHostMs, double maximumAgeMs, double clockUncertaintyMs)
    {
        if (json.Length > 16384 || string.IsNullOrWhiteSpace(runId) || streamId.Length != 32 ||
            !streamId.All(Uri.IsHexDigit) || lastSequence < 0) throw new InvalidDataException("Invalid stream binding.");
        using var document = JsonDocument.Parse(json); var root = document.RootElement;
        var binding = root.GetProperty("binding"); var observedTarget = binding.GetProperty("target");
        if (binding.GetProperty("runId").GetString() != runId || root.GetProperty("streamId").GetString() != streamId ||
            root.GetProperty("sequence").GetInt64() != checked(lastSequence + 1) ||
            observedTarget.GetProperty("processId").GetInt32() != target.ProcessId ||
            observedTarget.GetProperty("startedUtcTicks").GetInt64() != target.StartedUtcTicks ||
            observedTarget.GetProperty("handle").GetInt64() != target.Handle)
            throw new InvalidDataException("Stream lifetime or sequence changed.");
        return DecodeStreamCandidate(json, zone, context, receivedUtcTicks, receivedHostMs, maximumAgeMs, clockUncertaintyMs);
    }

    /// <summary>Archived client yaw grows clockwise from -Y. Convert to the
    /// follower's counterclockwise +X frame; input binding is a separate step.</summary>
    internal static double CanonicalHeading(double clientYaw)
    {
        if (!double.IsFinite(clientYaw)) throw new InvalidDataException("Invalid client heading.");
        return Math.Atan2(-Math.Cos(clientYaw), -Math.Sin(clientYaw));
    }

    internal static string TurnKey(int canonicalTurn) => canonicalTurn switch
    {
        -1 => "D", 1 => "A", _ => throw new ArgumentOutOfRangeException(nameof(canonicalTurn))
    };

    /// <summary>Candidate JSONL envelope mapped using a paired host UTC/monotonic
    /// receipt. The declared clock uncertainty is conservatively added to age.
    /// Reject stale/future delivery. Never compare raw source monotonic origins.
    /// This is not a qualification grant; owner binds lifetime and stream epoch.</summary>
    internal static PathObservation DecodeStreamCandidate(string json, string zone, string context,
        long receivedUtcTicks, double receivedHostMs, double maximumAgeMs, double clockUncertaintyMs)
    {
        if (json.Length > 16384 || string.IsNullOrWhiteSpace(zone) || string.IsNullOrWhiteSpace(context) ||
            receivedUtcTicks <= 0 || !new[] { receivedHostMs, maximumAgeMs, clockUncertaintyMs }.All(double.IsFinite) ||
            receivedHostMs < 0 || maximumAgeMs <= 0 || clockUncertaintyMs < 0)
            throw new InvalidDataException("Invalid stream receipt envelope.");
        using var doc = JsonDocument.Parse(json); var r = doc.RootElement;
        if (r.GetProperty("kind").GetString() != "moving-pose-candidate" || r.GetProperty("zone").GetString() != zone)
            throw new InvalidDataException("Stream context changed.");
        var sent = r.GetProperty("observedUtcTicks").GetInt64();
        var age = r.GetProperty("sampleAgeMs").GetDouble();
        if (sent <= 0 || sent > receivedUtcTicks || !double.IsFinite(age) || age < 0)
            throw new InvalidDataException("Invalid stream timing.");
        age += (receivedUtcTicks - sent) / (double)TimeSpan.TicksPerMillisecond + clockUncertaintyMs;
        if (age > maximumAgeMs || age > receivedHostMs) throw new InvalidDataException("Stale stream sample.");
        var sample = r.GetProperty("sample"); var p = sample.GetProperty("position");
        var x = p.GetProperty("x").GetDouble(); var y = p.GetProperty("y").GetDouble(); var z = p.GetProperty("z").GetDouble();
        var yaw = sample.GetProperty("yawRadians").GetDouble();
        if (!new[] { x, y, z, yaw }.All(double.IsFinite) || Math.Abs(yaw) > Math.Tau + 0.01)
            throw new InvalidDataException("Invalid stream pose.");
        return new(x, y, z, CanonicalHeading(yaw), receivedHostMs - age, zone, context);
    }

    /// <summary>Decode the newest candidate from the Python window. Host request
    /// start is used as a conservative acquisition lower bound: no unsupported
    /// comparison of Python monotonic counters with the host clock. This does not
    /// promote candidate observations to qualified navigation telemetry.</summary>
    internal static PathObservation DecodeCandidate(string json, string zone, string context,
        double hostRequestMs, double hostReceivedMs, double maximumRoundtripMs)
    {
        if (json.Length > 65536 || string.IsNullOrWhiteSpace(zone) || string.IsNullOrWhiteSpace(context) ||
            !new[] { hostRequestMs, hostReceivedMs, maximumRoundtripMs }.All(double.IsFinite) ||
            hostRequestMs < 0 || hostReceivedMs < hostRequestMs || maximumRoundtripMs <= 0 ||
            hostReceivedMs - hostRequestMs > maximumRoundtripMs)
            throw new InvalidDataException("Unbounded or stale moving-pose window.");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("status").GetString() != "candidate" || root.GetProperty("layoutId").GetString() != "archived-wizwalker-actor-body")
            throw new InvalidDataException("No candidate moving pose.");
        var samples = root.GetProperty("samples");
        if (samples.GetArrayLength() is < 2 or > 8) throw new InvalidDataException("Invalid moving-pose window.");
        double previous = -1;
        foreach (var sample in samples.EnumerateArray())
        {
            var observed = sample.GetProperty("observed").GetDouble();
            if (!double.IsFinite(observed) || observed <= previous) throw new InvalidDataException("Invalid source chronology.");
            previous = observed;
        }
        var last = samples[samples.GetArrayLength() - 1]; var p = last.GetProperty("position");
        var x = p.GetProperty("x").GetDouble(); var y = p.GetProperty("y").GetDouble(); var z = p.GetProperty("z").GetDouble();
        var yaw = last.GetProperty("yawRadians").GetDouble();
        if (!new[] { x, y, z, yaw }.All(double.IsFinite) || Math.Abs(yaw) > 2 * Math.PI + 0.01)
            throw new InvalidDataException("Invalid moving-pose values.");
        return new(x, y, z, CanonicalHeading(yaw), hostRequestMs, zone, context);
    }

    /// <summary>Injected scheduler seam; no production moving-reader binding is
    /// enabled yet. Coordinate arrival alone never completes a named destination.</summary>
    internal static async Task<OperationOutcome> RunOwnedAsync(OperationContext owner, NavigationRoute route,
        PathFollowerOptions options, W101PathRuntime runtime, CancellationToken token)
    {
        bool Owned() => owner.Request.WritesGame && !owner.Request.DelegatedInput &&
            owner.Input is { CanDispatch: true } && ReferenceEquals(owner.Input, GameInputLease.Current) && runtime.Owned();
        if (!Enum.IsDefined(route.ArrivalKind) ||
            route.ArrivalKind == NavigationArrivalKind.ZoneTransition && string.IsNullOrWhiteSpace(route.DestinationZone) ||
            route.ArrivalKind != NavigationArrivalKind.ZoneTransition && route.DestinationZone is not null && route.DestinationZone != options.Zone ||
            !route.Ready || !runtime.MovingReaderQualified || route.Identity.Zone != options.Zone || !Owned())
            return new(OperationState.Failed, "navigation-prerequisites-unqualified", []);
        var follower = new PathFollower(route.Points, options);
        string reason = "navigation-sample-budget";
        bool arrived = false, released = false, portalLoading = false;
        try
        {
            for (int i = 0; i < 4096; i++)
            {
                token.ThrowIfCancellationRequested();
                if (!Owned()) { reason = "navigation-owner-changed"; break; }
                var observation = await runtime.Read(token).ConfigureAwait(false);
                if (!Owned()) { reason = "navigation-owner-changed"; break; }
                var now = runtime.Clock();
                var motion = follower.Observe(observation, now);
                // Loading ends this follower permanently; a separately verified
                // destination needs a new route/localization epoch.
                if (route.ArrivalKind == NavigationArrivalKind.ZoneTransition && observation.Loading && !observation.Stopped &&
                    observation.Fresh && double.IsFinite(observation.TimeMs) && observation.TimeMs >= 0 && observation.TimeMs <= now &&
                    now - observation.TimeMs <= options.MaximumSampleAgeMs &&
                    motion.Reason == "loading-requires-new-route-context")
                    portalLoading = true;
                reason = motion.Reason;
                if (motion.State == "arrived") { arrived = true; break; }
                if (motion.State is "invalidated" or "replan" or "stopped" or "failed" or "blocked") break;
                if (motion.SequenceId > 0 && (motion.Forward || motion.Turn != 0))
                {
                    var receipt = await runtime.Apply(motion, token).ConfigureAwait(false);
                    follower.RecordApplied(motion, receipt);
                    if (receipt.DispatchAccepted && receipt.ReleasedMs is null)
                    { reason = "navigation-release-unconfirmed"; break; }
                }
                else if (motion.State is "paused" or "waiting" or "awaiting-release")
                    await Task.Delay(50, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { reason = "navigation-cancelled"; }
        catch { reason = "navigation-observation-or-action-failed"; }
        finally
        {
            // Cleanup-only delegate; must not acquire control or send another key-down.
            try { released = await runtime.Release(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(6)).ConfigureAwait(false); }
            catch { released = false; }
            if (!released) owner.Input!.RequireRecovery("Navigation release unconfirmed.");
        }
        if (!released) return new(OperationState.CleanupFailed, "navigation-release-unconfirmed", []);
        async Task<T> ObserveArrival<T>(Func<CancellationToken, Task<T>> read, int seconds)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
            var observation = read(deadline.Token);
            try { return await observation.WaitAsync(deadline.Token).ConfigureAwait(false); }
            finally
            {
                deadline.Cancel();
                try { await observation.WaitAsync(TimeSpan.FromSeconds(6)).ConfigureAwait(false); }
                catch (TimeoutException)
                {
                    owner.Input!.RequireRecovery("Arrival observer cleanup unconfirmed.");
                    throw;
                }
                catch when (observation.IsCompleted) { /* joined fault/cancellation is handled by caller */ }
            }
        }
        if ((arrived || portalLoading) && route.ArrivalKind != NavigationArrivalKind.InteractionPrompt &&
            runtime.VerifyArrival is { } verifyArrival && Owned() && !token.IsCancellationRequested)
        {
            try
            {
                var arrival = await ObserveArrival(ct => verifyArrival(route, ct), 20).ConfigureAwait(false);
                if (arrival.State == NavigationArrivalState.Verified && Owned() && !token.IsCancellationRequested)
                    return OperationOutcome.Completed("typed-arrival-verified; parent-must-finalize-idle");
                reason = "typed-arrival-unverified-or-owner-changed";
            }
            catch (OperationCanceledException) { reason = "navigation-cancelled"; }
            catch { reason = "typed-arrival-observation-failed"; }
        }
        if (arrived && route.ArrivalKind == NavigationArrivalKind.InteractionPrompt && Owned() && !token.IsCancellationRequested && route.RequiredPrompt is { } prompt)
        {
            try
            {
                if (await ObserveArrival(ct => runtime.VerifyPrompt(prompt, ct), 6).ConfigureAwait(false) &&
                    Owned() && !token.IsCancellationRequested)
                    return OperationOutcome.Completed("named-arrival-verified; parent-must-finalize-idle");
                reason = "named-arrival-unverified-or-owner-changed";
            }
            catch (OperationCanceledException) { reason = "navigation-cancelled"; }
            catch { reason = "named-arrival-observation-failed"; }
        }
        if (owner.Input is not { CanDispatch: true })
            return new(OperationState.CleanupFailed, "navigation-owner-or-arrival-cleanup-unconfirmed", []);
        return new(token.IsCancellationRequested ? OperationState.Cancelled : OperationState.Failed, reason, []);
    }
}
