using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

/// <summary>One addressed W101 Space pulse, based on Deimos's dialogue binding.
/// No focus change, global key injection, mouse movement or arbitrary key API.
/// SendMessageTimeout bounds an unresponsive client; it does not prove gameplay.</summary>
internal static class W101DialogueInput
{
    internal static async Task<RoutineReceipt> DispatchAsync(ModuleDecision proposal, OperationContext context,
        Func<bool> ready, Func<bool, bool> send, Func<double> clock, CancellationToken token)
    {
        bool Owned() => context.Request.WritesGame && !context.Request.DelegatedInput && context.Input is { CanDispatch: true } &&
            ReferenceEquals(context.Input, GameInputLease.Current) && !token.IsCancellationRequested && ready();
        if (!proposal.IsProposal || proposal.ActionId != W101QuestConversation.ActionId ||
            proposal.Action != new ModuleAction(W101QuestConversation.ActionId, "keyboard-pulse", "SPACE", 40, W101QuestStep.ControlId) ||
            proposal.Stopped || proposal.Completed || !Owned())
            return new(proposal.ProposalId ?? "invalid", proposal.ActionId ?? "invalid", clock(), false, true);
        bool accepted = false, released = false;
        try
        {
            accepted = send(false);
            if (accepted) await Task.Delay(40, token).ConfigureAwait(false);
            accepted &= Owned();
        }
        catch (OperationCanceledException) { accepted = false; }
        catch { accepted = false; }
        finally
        {
            // Key-up is mandatory even if key-down timed out or cancellation arrived.
            try { released = send(true); } catch { released = false; }
            if (!released) context.Input!.RequireRecovery("Quest dialogue Space release was not confirmed.");
        }
        return new(proposal.ProposalId!, proposal.ActionId!, clock(), accepted, released);
    }

    internal static bool Send(WizwalkerTarget target, bool release)
    {
        if (!target.IsCurrent()) return false;
        var flags = release ? unchecked((nint)(int)0xC0390001) : (nint)0x00390001;
        return SendMessageTimeoutW((nint)target.Handle, release ? 0x101u : 0x100u, 0x20, flags,
            3, 200, out _) != 0;
    }

    internal static uint Activity()
    {
        var value = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
        if (!GetLastInputInfo(ref value)) throw new InvalidOperationException("Desktop activity information is unavailable.");
        return value.Tick;
    }

    internal static bool Ready(WizwalkerTarget target, uint baseline) => target.IsCurrent() &&
        !GameWindows.Minimized(target.Window) && Activity() == baseline && !DesktopActivityMonitor.AnyKeyHeld();

    [StructLayout(LayoutKind.Sequential)] private struct LastInput { public uint Size, Tick; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInput info);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SendMessageTimeoutW(nint window, uint message,
        nuint wParam, nint lParam, uint flags, uint timeout, out nuint result);
}
