using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

/// <summary>Addressed, bounded X/Escape pulses for the reviewed W101 module
/// bindings. Does not acquire ownership, activate a window, inject global keys,
/// or infer success from message delivery. The caller supplies fresh semantic
/// readiness and independently observes the resulting game state.</summary>
internal static class W101ModuleInput
{
    internal static async Task<RoutineReceipt> DispatchAsync(ModuleDecision proposal, OperationContext context,
        Func<bool> ready, Func<string, bool, bool> send, Func<double> clock, CancellationToken token,
        Func<bool>? continuing = null)
    {
        bool Owned(bool beforePress = true) => context.Request.WritesGame && !context.Request.DelegatedInput &&
            context.Input is { CanDispatch: true } && ReferenceEquals(context.Input, GameInputLease.Current) &&
            !token.IsCancellationRequested && (beforePress ? ready() : (continuing ?? ready)());
        var action = proposal.Action;
        var allowed = action == W101CharacterOperation.CharacterInfo || action == W101CharacterOperation.GoHome || action == W101IdleCycle.Wake || action == W101IdleCycle.CloseMenu ||
            action == new ModuleAction("game.interact", "keyboard-pulse", "X", 40, "w101.interaction") ||
            action == new ModuleAction("game.open-menu", "keyboard-pulse", "ESCAPE", 40, "outside-menu-command");
        if (!proposal.IsProposal || proposal.Stopped || proposal.Completed || action is null || !allowed ||
            proposal.ActionId != action.ActionId || !Owned())
            return new(proposal.ProposalId ?? "invalid", proposal.ActionId ?? "invalid", clock(), false, true);

        bool attempted = false, accepted = false, released = true;
        try
        {
            // Recheck immediately before down: the caller may have lost its
            // selected window or observed a user takeover since admission.
            if (Owned())
            {
                attempted = true;
                accepted = send(action.Key!, false);
                if (accepted) await Task.Delay(40, token).ConfigureAwait(false);
                // Evidence must be fresh before press; its natural aging during
                // the fixed pulse does not undo delivery. Ownership/takeover must
                // still hold, and the caller independently observes the outcome.
                accepted &= Owned(beforePress: false);
            }
        }
        catch (OperationCanceledException) { accepted = false; }
        catch { accepted = false; }
        finally
        {
            // Release even after a timeout, exception, cancellation or takeover.
            // Do not require fresh semantic readiness for release-only cleanup.
            if (attempted)
            {
                released = await W101NavigationInput.ReleaseAsync(() => send(action.Key!, true)).ConfigureAwait(false);
                if (!released) context.Input!.RequireRecovery("W101 module key release was not confirmed.");
            }
        }
        return new(proposal.ProposalId!, proposal.ActionId!, clock(), accepted, released);
    }

    /// <summary>Only reviewed X and Escape bindings, addressed to the same
    /// process instance. A successful send is a transport receipt, not an outcome.</summary>
    internal static bool Send(WizwalkerTarget target, string key, bool release)
    {
        var code = key switch { "C" => (Virtual: 0x43u, Scan: 0x2Eu), "HOME" => (Virtual: 0x24u, Scan: 0x147u), "X" => (Virtual: 0x58u, Scan: 0x2Du), "ESCAPE" => (Virtual: 0x1Bu, Scan: 0x01u), _ => default };
        if (code.Virtual == 0 || !target.IsCurrent()) return false;
        var flags = (code.Scan << 16) | 1u | (release ? 0xC0000000u : 0u);
        return SendMessageTimeoutW((nint)target.Handle, release ? 0x101u : 0x100u,
            code.Virtual, unchecked((nint)(int)flags), 3, 200, out _) != 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessageTimeoutW(nint window, uint message, nuint wParam,
        nint lParam, uint flags, uint timeout, out nuint result);
}
