using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record ModuleScreenTransition(string ActionId, string FromScene, string ToScene);
internal sealed record ModuleScreenAlias(string ObservedScene, string BoundScene, string ControlId, string[] AfterActions);
internal sealed record ModuleScreenPolicy(string GameId, ModuleAction[] Actions, ModuleScreenTransition[] Transitions,
    string[] ContextFacts, ModuleScreenAlias[] Aliases);
internal sealed record ModuleScreenAuthority(bool TargetReady, bool ContextVerified, bool InputOwned,
    bool UserActive = false, bool Cancelled = false, string[]? VerifiedFacts = null);
internal sealed record ModuleScreenDecision(ModuleDecision Decision, ModuleVisualRecognition Recognition,
    bool Stable, string? OutcomeForProposalId = null, ModuleLoadingRequirement? LoadingRequirement = null,
    ModuleLoadingObservation? LoadingObservation = null);

/// <summary>Image-bound bridge to the common runner. Two consistent observations
/// establish a screen; a receipt followed by a changed, newer recognized screen
/// can establish a registered UI transition. Pixel change alone is never movement
/// progress. The trusted policy is supplied by application code, not pack JSON.</summary>
internal sealed class ModuleScreenSession
{
    private readonly RoutineBinding _binding;
    private readonly Size _geometry;
    private readonly ModuleScreenPolicy _policy;
    private readonly ModuleRunner _runner;
    private readonly ModuleLoadingRequirement? _loadingRequirement;
    private ModuleVisualRecognition? _previous;
    private ModuleScreenDecision? _pending;
    private RoutineReceipt? _receipt;
    private string? _adoptedAlias;
    private double _lastCaptureMs = -1;
    private double _lastObservedMs = -1;
    public ModuleDecision Status => _runner.Status;
    public int Proposals => _runner.ProposedActions;
    public int CompletedModules => _runner.CompletedModules;

    public ModuleScreenSession(OperationModuleBundle bundle, RoutineBinding binding, Size geometry,
        ModuleScreenPolicy policy, string? runId = null, ModuleLoadingRequirement? loadingRequirement = null)
    {
        if (geometry.Width is <= 0 or > 8192 || geometry.Height is <= 0 or > 8192 || (long)geometry.Width * geometry.Height > 4194304)
            throw new InvalidDataException("Bound the screen geometry.");
        ValidatePolicy(bundle, policy);
        if (loadingRequirement is not null) ModuleLoadingGate.Validate(loadingRequirement);
        _loadingRequirement = loadingRequirement;
        _policy = JsonSerializer.Deserialize<ModuleScreenPolicy>(JsonSerializer.Serialize(policy, OperationModules.Json), OperationModules.Json)!;
        _binding = binding; _geometry = geometry; _runner = new(bundle, binding, runId, loadingRequirement);
    }

    internal static void ValidatePolicy(OperationModuleBundle bundle, ModuleScreenPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.GameId != bundle.GameId || policy.Transitions is not { Length: > 0 and <= 64 } ||
            policy.Aliases is null || policy.Aliases.Length > 8)
            throw new InvalidDataException("No screen policy is registered for this game.");
        OperationModules.ValidateActions(policy.Actions); OperationModules.CheckNames(policy.ContextFacts, 32);
        foreach (var transition in policy.Transitions)
        {
            if (transition is null) throw new InvalidDataException("Missing registered transition.");
            OperationModules.CheckId(transition.ActionId); OperationModules.CheckId(transition.FromScene); OperationModules.CheckId(transition.ToScene);
            if (transition.FromScene == transition.ToScene || !policy.Actions.Any(action => action.ActionId == transition.ActionId))
                throw new InvalidDataException("Screen-change policies cannot certify held-state motion progress.");
        }
        foreach (var alias in policy.Aliases)
        {
            if (alias is null) throw new InvalidDataException("Missing registered screen alias.");
            OperationModules.CheckId(alias.ObservedScene); OperationModules.CheckId(alias.BoundScene); OperationModules.CheckId(alias.ControlId);
            OperationModules.CheckNames(alias.AfterActions, 8);
            if (alias.AfterActions.Length == 0 || alias.AfterActions.Any(id => !policy.Transitions.Any(t => t.ActionId == id && t.ToScene == alias.BoundScene)))
                throw new InvalidDataException("An alias needs an explicit observed transition.");
        }
        var definitions = ModuleCatalog.Compile(bundle);
        if (definitions.SelectMany(module => module.Actions).Any(action => !policy.Actions.Contains(action)) ||
            definitions.SelectMany(module => module.Graph.Nodes).SelectMany(node => node.Edges).Any(edge =>
                edge.ExpectedScenes.Any(scene => !policy.Transitions.Contains(new(edge.ActionId, edge.Scene, scene)))))
            throw new InvalidDataException("This bundle requests actions or outcomes without a registered screen verifier.");
    }

    public ModuleScreenDecision Observe(ModuleVisualRecognition recognition, double observedMs, ModuleScreenAuthority authority,
        ModuleLoadingObservation? loading = null)
    {
        ArgumentNullException.ThrowIfNull(authority);
        var facts = authority.VerifiedFacts ?? [];
        OperationModules.CheckNames(facts, 32);
        if (facts.Any(fact => !_policy.ContextFacts.Contains(fact))) throw new InvalidDataException("Unregistered external context fact.");
        var evidence = ModuleRecognition.AsObservation(recognition, _binding, _geometry, observedMs,
            authority.TargetReady, authority.ContextVerified, authority.InputOwned, authority.UserActive, authority.Cancelled,
            authority.ContextVerified ? facts : []);
        // Observation time is the trusted execution clock. Keep capture time in
        // evidence; a future/stale image cannot reset or poison runner deadlines.
        evidence = evidence with { Observation = evidence.Observation with { TimeMs = observedMs } };
        var clockAdvanced = double.IsFinite(observedMs) && observedMs > _lastObservedMs;
        if (clockAdvanced) _lastObservedMs = observedMs;
        var loadingClear = ModuleLoadingGate.IsClear(_loadingRequirement, loading, _binding, observedMs) &&
            (_loadingRequirement is null || recognition.Frame.CapturedMs >= loading!.SampledMs);
        var admissible = loadingClear && clockAdvanced && evidence.Observation.Fresh && recognition.Frame.CapturedMs > _lastCaptureMs &&
            authority.TargetReady && authority.InputOwned && authority.ContextVerified &&
            !authority.UserActive && !authority.Cancelled;
        if (admissible) _lastCaptureMs = recognition.Frame.CapturedMs;
        var effective = loadingClear ? recognition : recognition with
        {
            Scene = _loadingRequirement is not null && ModuleLoadingGate.IsCurrent(_loadingRequirement, loading, _binding, observedMs) &&
                loading!.Loading == true ? "Loading" : "Unknown",
            Controls = [], VisualFacts = [], Reason = "optional-loading-condition-withheld"
        };
        if (!loadingClear) _adoptedAlias = null;
        var afterReleasedAction = admissible && _pending is not null && _receipt is not null &&
            recognition.Frame.CapturedMs > _receipt.TimeMs &&
            !recognition.Frame.ImageSha256.Equals(_pending.Recognition.Frame.ImageSha256, StringComparison.OrdinalIgnoreCase);
        if (admissible)
        {
            foreach (var alias in _policy.Aliases.Where(alias => alias.ObservedScene == recognition.Scene))
            {
                if ((afterReleasedAction && alias.AfterActions.Contains(_pending!.Decision.ActionId)) || _adoptedAlias == alias.BoundScene)
                {
                    effective = recognition with { Scene = alias.BoundScene, Controls = [new(alias.ControlId, new(0, 0, _geometry.Width, _geometry.Height))] };
                    _adoptedAlias = alias.BoundScene;
                }
            }
            if (!_policy.Aliases.Any(alias => alias.ObservedScene == recognition.Scene && alias.BoundScene == _adoptedAlias)) _adoptedAlias = null;
        }
        var stable = admissible && _previous is not null && _previous.Scene == effective.Scene &&
            _previous.Frame.Binding == effective.Frame.Binding && _previous.Frame.Geometry == effective.Frame.Geometry &&
            effective.Frame.CapturedMs > _previous.Frame.CapturedMs &&
            effective.Frame.CapturedMs - _previous.Frame.CapturedMs <= 1500 && SameControls(_previous.Controls, effective.Controls);
        if (_receipt is not null && _pending is not null)
            stable &= afterReleasedAction && _previous is not null && _previous.Frame.CapturedMs > _receipt.TimeMs &&
                !_previous.Frame.ImageSha256.Equals(_pending.Recognition.Frame.ImageSha256, StringComparison.OrdinalIgnoreCase);
        _previous = admissible ? effective : null;
        string? outcome = null;
        if (stable && afterReleasedAction && _policy.Transitions.Contains(new(_pending!.Decision.ActionId!, _pending.Recognition.Scene, effective.Scene)))
            outcome = _pending.Decision.ProposalId;
        evidence = evidence with
        {
            Observation = evidence.Observation with { Scene = effective.Scene, Fresh = admissible && stable,
                ControlVerified = admissible && stable && effective.Controls.Length > 0, OutcomeForProposalId = outcome },
            VerifiedControls = admissible && stable ? effective.Controls.Select(control => control.ControlId).ToArray() : [],
            VerifiedFacts = loadingClear ? evidence.VerifiedFacts : []
        };
        var decision = _runner.Observe(evidence, loading);
        var result = new ModuleScreenDecision(decision, effective, stable, outcome, _loadingRequirement, loading);
        if (outcome is not null) { _pending = null; _receipt = null; }
        if (decision.IsProposal) { _pending = result; _receipt = null; }
        return result;
    }

    public void RecordReceipt(RoutineReceipt receipt)
    {
        _runner.RecordReceipt(receipt);
        if (!_runner.Status.Stopped && _pending?.Decision.ProposalId == receipt.ProposalId &&
            _pending.Decision.ActionId == receipt.ActionId && receipt.Accepted && receipt.InputReleased)
        { _receipt = receipt; _previous = null; }
    }

    private static bool SameControls(ModuleVisualControl[] first, ModuleVisualControl[] second) =>
        first.Length == second.Length && first.All(control => second.Any(other => other.ControlId == control.ControlId &&
            Math.Abs(control.Bounds.X - other.Bounds.X) <= 8 && Math.Abs(control.Bounds.Y - other.Bounds.Y) <= 8 &&
            Math.Abs(control.Bounds.Width - other.Bounds.Width) <= 8 && Math.Abs(control.Bounds.Height - other.Bounds.Height) <= 8));
}
