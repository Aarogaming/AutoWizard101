namespace ProjectMaelstrom.Utilities;

/// <summary>Adapter to the existing postgame input implementation. Acquires no
/// ownership and never focuses a window. Saved-screen review has no such transport.
/// The existing Dance cleanup continuation uses this adapter under its inherited
/// scheduler lease; new source integration still needs live qualification.</summary>
internal sealed class ModuleOwnedInput
{
    private readonly OperationContext _context;
    private readonly RoutineBinding _binding;
    private readonly Size _geometry;
    private readonly Func<bool> _ready;
    private readonly Func<double> _clock;
    private readonly Func<PetDancePostGamePlan, Func<bool>, CancellationToken, Task<PetDanceMenuReceipt>> _dispatch;
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);
    private int _busy;

    internal ModuleOwnedInput(OperationContext context, RoutineBinding binding, Size geometry, Func<bool> ready, Func<double> clock,
        Func<PetDancePostGamePlan, Func<bool>, CancellationToken, Task<PetDanceMenuReceipt>> dispatch)
    {
        if (context.Request.Target != binding.TargetId)
            throw new InvalidDataException("The scheduler admission and input binding must name the same target.");
        _context = context; _binding = binding; _geometry = geometry; _ready = ready; _clock = clock; _dispatch = dispatch;
    }

    public static ModuleOwnedInput ForPetDance(OperationContext context, RoutineBinding binding, Size geometry,
        GameWindow target, Size clientGeometry, nuint tag, Func<bool> ready, Func<double> clock)
    {
        if (binding.TargetId != TargetId(target)) throw new InvalidDataException("Bind input to the selected process instance.");
        var input = new PetDanceMenuInput(tag);
        var targetGuard = new WindowsDodgeKeySink(tag);
        return new(context, binding, geometry, () => ready() && targetGuard.Ready(target, clientGeometry), clock,
            (plan, guard, token) => input.DispatchAsync(plan, target, geometry, guard, token));
    }

    internal static string TargetId(GameWindow target) => "pid-" + target.ProcessId + "-start-" + target.StartedUtcTicks;

    public async Task<RoutineReceipt> DispatchAsync(ModuleScreenDecision proposal, CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return new(proposal.Decision.ProposalId ?? "invalid", proposal.Decision.ActionId ?? "invalid", _clock(), false, true);
        try { return await DispatchCoreAsync(proposal, token).ConfigureAwait(false); }
        finally { Volatile.Write(ref _busy, 0); }
    }

    private async Task<RoutineReceipt> DispatchCoreAsync(ModuleScreenDecision proposal, CancellationToken token)
    {
        var decision = proposal.Decision; var frame = proposal.Recognition.Frame;
        var accepted = false; var released = true;
        bool Ready() => !token.IsCancellationRequested && _context.Request.WritesGame && !_context.Request.DelegatedInput &&
            _context.Input is { CanDispatch: true } && ReferenceEquals(GameInputLease.Current, _context.Input) &&
            _ready() && frame.Binding == _binding && frame.Geometry == _geometry && frame.TextOrigin == "local-windows-ocr" &&
            double.IsFinite(_clock()) && _clock() >= frame.CapturedMs && _clock() - frame.CapturedMs <= 300 &&
            ModuleLoadingGate.IsClear(proposal.LoadingRequirement, proposal.LoadingObservation, _binding, _clock());
        if (!decision.IsProposal || decision.Action is null || !proposal.Stable || decision.Stopped || decision.Completed ||
            !Ready() || _used.Count >= 64 || !_used.Add(decision.ProposalId!))
            return new(decision.ProposalId ?? "invalid", decision.ActionId ?? "invalid", _clock(), false, true);
        var action = W101ModulePrograms.Resolve(decision.Action);
        if (decision.ActionId != decision.Action.ActionId) throw new InvalidDataException("Proposal/action identity mismatch.");
        var control = proposal.Recognition.Controls.SingleOrDefault(control => control.ControlId == decision.Action.ControlId);
        if (control is null || !PetDancePostGameClassifier.ValidBounds(control.Bounds, _geometry))
            return new(decision.ProposalId!, decision.ActionId!, _clock(), false, true);
        try
        {
            var result = await _dispatch(new(action, true, "module-screen-proposal", control.Bounds), Ready, token).ConfigureAwait(false);
            accepted = result.Accepted; released = result.InputReleased;
            RequireRecoveryIfUnreleased(released, "Module UI input release could not be confirmed: " + result.Reason);
        }
        catch (Exception error)
        {
            // A thrown transport call provides no release receipt. Preserve that
            // uncertainty rather than claiming cleanup succeeded on cancellation.
            accepted = false; released = false;
            RequireRecoveryIfUnreleased(false, "Module UI transport did not confirm release: " + error.Message);
        }
        return new(decision.ProposalId!, decision.ActionId!, _clock(), accepted, released);
    }

    private void RequireRecoveryIfUnreleased(bool released, string reason)
    {
        if (released) return;
        try { _context.Input?.RequireRecovery(reason); }
        catch (Exception) when (_context.Input?.RecoveryRequired == true)
        {
            // The durable guard and local latch already deny further gameplay.
            // Preserve the unreleased receipt; the scheduler reports the saved
            // diagnostic failure without claiming cleanup succeeded.
        }
    }
}
