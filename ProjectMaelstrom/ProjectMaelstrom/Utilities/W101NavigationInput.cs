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
            "W" => "game.move-forward", "A" => "game.turn-left", "D" => "game.turn-right", _ => null
        };
        bool attempted = false, accepted = false, released = true;
        try
        {
            if (!proposal.IsProposal || proposal.Stopped || proposal.Completed || action is null || expected is null ||
                action.ActionId != expected || proposal.ActionId != expected || action.Kind != "keyboard-pulse" ||
                action.ControlId != "w101.navigation" || action.DurationMs is < 1 or > 300 || !Owned())
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
                try { released = send(action!.Key!, true); } catch { released = false; }
                if (!released) context.Input!.RequireRecovery("W101 navigation key release was not confirmed.");
            }
        }
        return new(proposal.ProposalId ?? "invalid", proposal.ActionId ?? "invalid", clock(), accepted, released);
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
            "W" => (Virtual: 0x57u, Scan: 0x11u), "A" => (Virtual: 0x41u, Scan: 0x1Eu),
            "D" => (Virtual: 0x44u, Scan: 0x20u), _ => default
        };
        if (code.Virtual == 0 || target.ProcessName != "WizardGraphicalClient" || !target.IsCurrent()) return false;
        var flags = (code.Scan << 16) | 1u | (release ? 0xC0000000u : 0u);
        return SendMessageTimeoutW((nint)target.Handle, release ? 0x101u : 0x100u,
            code.Virtual, unchecked((nint)(int)flags), 3, 200, out _) != 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessageTimeoutW(nint window, uint message, nuint wParam,
        nint lParam, uint flags, uint timeout, out nuint result);
}
