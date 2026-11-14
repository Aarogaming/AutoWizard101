using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

/// <summary>Owner-bound W/A/D pulses only. Readiness must validate the selected
/// target, foreground, geometry and user takeover. Delivery is not route progress.
/// This helper never acquires control or activates a window.</summary>
internal static class W101NavigationInput
{
    internal static async Task<RoutineReceipt> DispatchAsync(ModuleDecision proposal, OperationContext context,
        string targetId, Func<bool> ready, Func<string, bool, bool> send, Func<double> clock,
        CancellationToken token, Func<bool>? continuing = null)
    {
        bool Owned(bool beforePress = true) => context.Request.WritesGame && !context.Request.DelegatedInput &&
            context.Request.Target == targetId && ValidTargetId(targetId) &&
            context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current) &&
            !token.IsCancellationRequested && (beforePress ? ready() : (continuing ?? ready)());
        var action = proposal.Action;
        var expected = action?.Key switch
        {
            "W" => "game.move-forward", "S" => "game.move-backward", "A" => "game.turn-left", "D" => "game.turn-right", _ => null
        };
        var assisted = context.Request.Operation == W101NavigationVisit.Id && action?.Key is "W" or "S" && action.ControlId == "w101.assisted-navigation";
        var untilLoading = context.Request.Operation == W101NavigationVisit.Id && action?.Key is "W" or "S" && action.ControlId == "w101.walk-until-loading";
        var untilPrompt = context.Request.Operation == W101NavigationVisit.Id && action?.Key is "W" or "S" && action.ControlId?.StartsWith("w101.walk-until-interaction:", StringComparison.Ordinal) == true;
        bool attempted = false, accepted = false, released = true;
        try
        {
            if (!proposal.IsProposal || proposal.Stopped || proposal.Completed || action is null || expected is null ||
                action.ActionId != expected || proposal.ActionId != expected || action.Kind != "keyboard-pulse" ||
                (!assisted && !untilLoading && !untilPrompt && action.ControlId != "w101.navigation") || action.DurationMs < 1 || action.DurationMs > (untilPrompt ? 30000 : untilLoading ? 15000 : assisted ? 3000 : 300) || !Owned())
                return new(proposal.ProposalId ?? "invalid", proposal.ActionId ?? "invalid", clock(), false, true);
            if (Owned())
            {
                attempted = true;
                var hold = Stopwatch.StartNew();
                accepted = send(action.Key!, false);
                while (accepted && hold.ElapsedMilliseconds < action.DurationMs)
                {
                    if (!Owned(false)) { accepted = false; break; }
                    var remaining = action.DurationMs - hold.ElapsedMilliseconds;
                    if (remaining > 0) await Task.Delay((int)Math.Min(10, remaining), token).ConfigureAwait(false);
                }
                accepted = accepted && Owned(false);
            }
        }
        catch (OperationCanceledException) { accepted = false; }
        catch { accepted = false; }
        finally
        {
            if (attempted)
            {
                released = await ReleaseAsync(() => send(action!.Key!, true)).ConfigureAwait(false);
                if (!released) context.Input!.RequireRecovery("W101 navigation key release was not confirmed.");
            }
        }
        return new(proposal.ProposalId ?? "invalid", proposal.ActionId ?? "invalid", clock(), accepted, released);
    }

    /// <summary>Cleanup-only retries: never press again, even on cancellation.
    /// Loading may temporarily prevent the game thread acknowledging a key-up.</summary>
    internal static async Task<bool> ReleaseAsync(Func<bool> release, int maximumMs = 5000)
    {
        if (maximumMs is < 1 or > 5000) throw new ArgumentOutOfRangeException(nameof(maximumMs));
        var elapsed = Stopwatch.StartNew();
        do
        {
            try { if (release()) return true; } catch { }
            if (elapsed.ElapsedMilliseconds >= maximumMs) break;
            await Task.Delay(100).ConfigureAwait(false);
        } while (elapsed.ElapsedMilliseconds < maximumMs);
        return false;
    }

    private static bool ValidTargetId(string value)
    {
        var parts = value.Split('-');
        return parts.Length == 4 && parts[0] == "pid" && parts[2] == "start" &&
            int.TryParse(parts[1], out var pid) && pid > 0 && long.TryParse(parts[3], out var ticks) && ticks > 0;
    }

    /// <summary>Addressed transport revalidates the process instance for every
    /// message. A successful result confirms message delivery, not movement.</summary>
    internal static bool Send(WizwalkerTarget target, string key, bool release)
    {
        var code = key switch
        {
            "W" => (Virtual: 0x57u, Scan: 0x11u), "S" => (Virtual: 0x53u, Scan: 0x1Fu), "A" => (Virtual: 0x41u, Scan: 0x1Eu),
            "D" => (Virtual: 0x44u, Scan: 0x20u), _ => default
        };
        if (code.Virtual == 0 || target.ProcessName != "WizardGraphicalClient" || !target.IsCurrent()) return false;
        var flags = (code.Scan << 16) | 1u | (release ? 0xC0000000u : 0u);
        var acknowledged = SendMessageTimeoutW((nint)target.Handle, release ? 0x101u : 0x100u,
            code.Virtual, unchecked((nint)(int)flags), 3, 200, out _) != 0;
        // Queue only the matching key-up if loading blocks synchronous delivery.
        // Posting is not acknowledgement; cleanup retries still require a response.
        if (!acknowledged && release && target.IsCurrent())
            PostMessageW((nint)target.Handle, 0x101u, code.Virtual, unchecked((nint)(int)flags));
        return acknowledged;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessageTimeoutW(nint window, uint message, nuint wParam,
        nint lParam, uint flags, uint timeout, out nuint result);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessageW(nint window, uint message, nuint wParam, nint lParam);
}
