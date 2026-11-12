using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

/// <summary>Trusted application seams. Send is synchronous and bounded; true means
/// the addressed message returned, not that a card was selected or a battle won.</summary>
internal sealed record W101CombatInputRuntime(Func<WizwalkerTarget, bool> IsCurrent,
    Func<WizwalkerTarget, Size> ClientSize, Func<WizwalkerTarget, Point, bool, bool> Send,
    Func<double> Clock, Func<int, CancellationToken, Task> Delay);

/// <summary>One addressed left-click per admitted combat proposal, under the existing
/// scheduler lease. Coordinates are normalized client coordinates, not UI-layout units.
/// This transport never moves the desktop cursor, changes focus or installs hooks.
/// Actual W101 mouse-message behavior requires separate live qualification.</summary>
internal sealed class W101CombatInput
{
    private const int PulseMilliseconds = 40, MaximumProposals = 128;
    private readonly WizwalkerTarget _target;
    private readonly string _geometryId;
    private readonly Size _clientSize;
    private readonly W101CombatInputRuntime _runtime;
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);
    private int _busy;

    internal W101CombatInput(WizwalkerTarget target, string geometryId, Size clientSize, W101CombatInputRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(runtime);
        if (target.Handle <= 0 || target.ProcessId <= 0 || target.StartedUtcTicks <= 0 ||
            target.ProcessName != "WizardGraphicalClient" || !Text(geometryId) || !ValidSize(clientSize))
            throw new InvalidDataException("Bind combat input to an exact W101 target and admitted client geometry.");
        _target = target; _geometryId = geometryId; _clientSize = clientSize; _runtime = runtime;
    }

    internal static W101CombatInput ForAddressedWindow(WizwalkerTarget target, string geometryId,
        Size clientSize, Func<double> clock) => new(target, geometryId, clientSize,
        new(t => t.IsCurrent(), t => GameWindows.ClientSize(t.Window),
            (t, point, release) => Send(t, clientSize, point, release), clock, Task.Delay));

    internal async Task<RoutineReceipt> DispatchAsync(W101CombatDispatch proposal, OperationContext context,
        Func<bool> ready, CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return Receipt(proposal, false, true);
        try { return await DispatchCoreAsync(proposal, context, ready, token).ConfigureAwait(false); }
        finally { Volatile.Write(ref _busy, 0); }
    }

    private async Task<RoutineReceipt> DispatchCoreAsync(W101CombatDispatch proposal, OperationContext context,
        Func<bool> ready, CancellationToken token)
    {
        bool Owned() => !token.IsCancellationRequested && context.Request.WritesGame && !context.Request.DelegatedInput &&
            context.Request.Target == W101QuestStep.TargetBinding(_target) && context.Input is { CanDispatch: true } &&
            ReferenceEquals(context.Input, GameInputLease.Current);
        bool Ready() => Owned() && ready() && _runtime.IsCurrent(_target) &&
            _runtime.ClientSize(_target) == _clientSize && double.IsFinite(_runtime.Clock());

        // Keep malformed/replayed proposals from reaching even the transport seam.
        if (!ValidProposal(proposal) || !TryMapCenter(proposal.Control.Bounds, _clientSize, out var point) ||
            _used.Count >= MaximumProposals || _used.Contains(proposal.ProposalId))
            return Receipt(proposal, false, true);
        bool accepted = false, released = true, attempted = false;
        try
        {
            if (!Ready()) return Receipt(proposal, false, true);
            _used.Add(proposal.ProposalId);
            // Recheck after preparation, immediately before the only press. A proposal
            // consumed here is not retried if cancellation/takeover wins this race.
            if (!Ready()) return Receipt(proposal, false, true);
            attempted = true; released = false;
            accepted = _runtime.Send(_target, point, false);
            if (accepted) await _runtime.Delay(PulseMilliseconds, token).ConfigureAwait(false);
            accepted &= Ready();
        }
        catch (Exception) { accepted = false; }
        finally
        {
            if (attempted)
            {
                // Do not apply the press gate to release: cancellation, a resized client,
                // lost readiness or takeover still require an up to the original target.
                // A replaced/destroyed HWND must never receive cleanup intended for it.
                try { released = _runtime.IsCurrent(_target) && _runtime.Send(_target, point, true); }
                catch (Exception) { released = false; }
                if (!released) RequireRecovery(context);
            }
        }
        return Receipt(proposal, accepted && released, released);
    }

    private bool ValidProposal(W101CombatDispatch p) => Text(p.ProposalId) && Text(p.SnapshotId) &&
        p.GeometryId == _geometryId && p.Control is { Enabled: true } c && Text(c.Id) && Text(c.SubjectId) &&
        (p.ActionId switch
        {
            W101CombatTurnOperation.SelectCard => c.Role == W101CombatControlRole.Card,
            W101CombatTurnOperation.SelectTarget => (c.Role is W101CombatControlRole.Enemy or W101CombatControlRole.Self) && c.Alive == true,
            W101CombatTurnOperation.Pass => c.Role == W101CombatControlRole.Pass,
            _ => false
        });

    /// <summary>Map the control center into the exact client raster. Client pixels
    /// are [0,width) × [0,height); normalized 1 is the outer edge, never width itself.
    /// Signed 16-bit message coordinates limit each dimension to 32768 pixels.</summary>
    internal static bool TryMapCenter(RectangleF bounds, Size size, out Point point)
    {
        point = default;
        if (!ValidSize(size) || !float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) ||
            !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height) || bounds.X < 0 || bounds.Y < 0 || bounds.X >= 1 || bounds.Y >= 1 ||
            bounds.Width <= 0 || bounds.Height <= 0 || bounds.Right > 1 || bounds.Bottom > 1)
            return false;
        var x = (int)Math.Floor(((double)bounds.X + bounds.Width / 2.0) * size.Width);
        var y = (int)Math.Floor(((double)bounds.Y + bounds.Height / 2.0) * size.Height);
        point = new(Math.Clamp(x, 0, size.Width - 1), Math.Clamp(y, 0, size.Height - 1));
        return true;
    }

    internal static nint PackClientPoint(Point point)
    {
        if (point.X is < 0 or > short.MaxValue || point.Y is < 0 or > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(point));
        return (nint)(point.X | (point.Y << 16));
    }

    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256;
    private static bool ValidSize(Size size) => size.Width is > 0 and <= 32768 && size.Height is > 0 and <= 32768;
    private RoutineReceipt Receipt(W101CombatDispatch proposal, bool accepted, bool released) =>
        new(proposal.ProposalId ?? "invalid", proposal.ActionId ?? "invalid", _runtime.Clock(), accepted, released);

    private static void RequireRecovery(OperationContext context)
    {
        try { context.Input!.RequireRecovery("Combat mouse release was not confirmed for the original target."); }
        catch (Exception) when (context.Input?.RecoveryRequired == true)
        {
            // Recovery latches before notice persistence. Failed diagnostics must not
            // turn the uncertain release into a successful receipt or unlock gameplay.
        }
    }

    private static bool Send(WizwalkerTarget target, Size admittedSize, Point point, bool release)
    {
        if (!target.IsCurrent()) return false;
        if (!release && (GameWindows.Minimized(target.Window) || GameWindows.ClientSize(target.Window) != admittedSize))
            return false;
        // No WM_MOUSEMOVE: moving the real cursor or using a cursor hook is a separate
        // capability. This bounded addressed-message path may be ignored by the game.
        return SendMessageTimeoutW((nint)target.Handle, release ? 0x202u : 0x201u, release ? 0u : 1u,
            PackClientPoint(point), 0x23, 200, out _) != 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessageTimeoutW(nint window, uint message, nuint wParam, nint lParam,
        uint flags, uint timeout, out nuint result);
}
