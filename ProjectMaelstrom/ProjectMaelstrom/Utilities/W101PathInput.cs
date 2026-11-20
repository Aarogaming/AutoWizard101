namespace ProjectMaelstrom.Utilities;

/// <summary>Trusted bridge from a follower intention to the existing navigation
/// dispatcher under the caller's input lease. It never acquires focus or ownership.
/// Initial policy is rotation-first: a combined forward/turn intention dispatches
/// only the turn, then requires a fresh observation before any forward pulse.</summary>
internal static class W101PathInput
{
    internal static async Task<PathMotionReceipt> DispatchAsync(PathMotion motion, OperationContext owner,
        WizwalkerTarget target, Func<bool> ready, Func<double> clock, CancellationToken token,
        Func<string, bool, bool>? send = null)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(ready);
        ArgumentNullException.ThrowIfNull(clock);
        if (motion.SequenceId <= 0 || !double.IsFinite(motion.IssuedMs) || motion.IssuedMs < 0 ||
            !double.IsFinite(motion.ValidUntilMs) || motion.ValidUntilMs <= motion.IssuedMs || motion.Turn is < -1 or > 1)
            throw new InvalidDataException("Invalid bounded path intention.");

        double lastClock = motion.IssuedMs;
        bool invalidClock = false;
        double ReadClock()
        {
            var value = clock();
            if (!double.IsFinite(value) || value < lastClock)
            {
                invalidClock = true;
                throw new InvalidDataException("Path input clock is nonfinite or moved backwards.");
            }
            lastClock = value;
            return value;
        }
        var now = ReadClock();
        PathMotionReceipt Rejected(double reported) => new(motion.SequenceId, false, reported, ForwardApplied: false);
        if (motion.State != "follow" || (!motion.Forward && motion.Turn == 0) || token.IsCancellationRequested ||
            motion.ValidUntilMs - now < 1) return Rejected(now);
        var duration = (int)Math.Floor(Math.Min(300, motion.ValidUntilMs - now));
        var key = motion.Turn != 0 ? W101PathSession.TurnKey(motion.Turn) : "W";
        var actionId = key switch { "W" => "game.move-forward", "A" => "game.turn-left", _ => "game.turn-right" };
        var proposal = new ModuleDecision("path-follower", "bounded-intent", "proposal",
            "path-" + motion.SequenceId.ToString(System.Globalization.CultureInfo.InvariantCulture), actionId,
            new(actionId, "keyboard-pulse", key, duration, "w101.navigation"));
        var targetId = $"pid-{target.ProcessId}-start-{target.StartedUtcTicks}";
        var transport = send ?? ((string name, bool release) => W101NavigationInput.Send(target, name, release));
        double? dispatched = null, released = null;
        bool Guard() => !token.IsCancellationRequested && ReadClock() < motion.ValidUntilMs && ready();
        bool Send(string name, bool release)
        {
            if (!release)
            {
                // Repeat the expiry/takeover guard at the transport boundary: an
                // earlier admission is not permission to send a delayed key-down.
                if (!Guard()) return false;
                var attemptedAt = ReadClock();
                if (attemptedAt >= motion.ValidUntilMs) return false;
                var accepted = transport(name, false);
                if (accepted) dispatched = attemptedAt;
                return accepted;
            }
            var acknowledged = transport(name, true);
            if (acknowledged)
            {
                // Clock failure must not turn an acknowledged release into five
                // seconds of duplicate cleanup. Report it after joining dispatch.
                try { released = ReadClock(); }
                catch (InvalidDataException) { invalidClock = true; }
            }
            return acknowledged;
        }

        var result = await W101NavigationInput.DispatchAsync(proposal, owner, targetId,
            Guard, Send, ReadClock, token, Guard).ConfigureAwait(false);
        var reported = ReadClock();
        if (invalidClock) throw new InvalidDataException("Path input timing was not trustworthy; dispatch cleanup has finished.");
        // Lower-level Accepted also means uninterrupted completion. Here retain
        // actual acknowledged key-down even when expiry/takeover curtailed its hold.
        // No game motion is inferred from a successful message acknowledgement.
        return dispatched is { } started
            ? new(motion.SequenceId, true, reported, started, null, result.InputReleased ? released : null, ForwardApplied: key == "W")
            : Rejected(reported);
    }
}
