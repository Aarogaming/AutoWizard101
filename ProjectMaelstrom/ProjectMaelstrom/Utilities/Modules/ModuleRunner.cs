using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Composes modules through the existing RoutineRunner. Facts and named
/// controls are supplied by a qualified adapter, never inferred here. This class
/// proposes actions only and has no desktop, capture, model or input dependency.</summary>
internal sealed class ModuleRunner
{
    private readonly OperationModuleBundle _bundle;
    private readonly OperationModuleDefinition[] _modules;
    private readonly RoutineBinding _binding;
    private readonly string _runId;
    private readonly ModuleLoadingRequirement? _loadingRequirement;
    private RoutineRunner _runner;
    private int _index, _actions;
    private double _firstTime = -1, _lastTime = -1, _receiptTime = -1;
    private RoutineEdge? _pending;
    private string? _proposal, _stopReason;
    private bool _completed;
    public int CompletedModules { get; private set; }
    public int ProposedActions => _actions;
    public ModuleDecision Status => Convert(_runner.Status) with
    {
        Completed = _completed, Stopped = _stopReason is not null || _runner.Status.Stopped,
        Reason = _stopReason ?? (_completed ? "observed-bundle-completion" : _runner.Status.Reason)
    };

    public ModuleRunner(OperationModuleBundle bundle, RoutineBinding binding, string? runId = null,
        ModuleLoadingRequirement? loadingRequirement = null)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (loadingRequirement is not null) ModuleLoadingGate.Validate(loadingRequirement);
        _loadingRequirement = loadingRequirement;
        // Deep snapshot: later editor changes cannot alter an active run.
        ModuleCatalog.Compile(bundle);
        _bundle = ModuleCatalog.ReadBundle(JsonSerializer.Serialize(bundle, OperationModules.Json));
        _modules = ModuleCatalog.Compile(_bundle);
        OperationModules.CheckToken(binding.TargetId, 256); OperationModules.CheckToken(binding.GeometryId, 256);
        var prefix = _bundle.ContextId + ".visit.";
        if (binding.ContextId is null || !binding.ContextId.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException("Bundle requires its own context and a distinct visit binding.");
        OperationModules.CheckId(binding.ContextId[prefix.Length..]);
        _binding = binding; _runId = runId ?? Guid.NewGuid().ToString("N");
        OperationModules.CheckId(_runId);
        _runner = NewRunner();
    }

    public ModuleDecision Observe(ModuleObservation evidence, ModuleLoadingObservation? loading = null)
    {
        ArgumentNullException.ThrowIfNull(evidence); ArgumentNullException.ThrowIfNull(evidence.Observation);
        evidence = ModuleLoadingGate.Apply(evidence, _loadingRequirement, loading, _binding);
        if (_completed || _stopReason is not null || _runner.Status.Stopped) return Status;
        var frame = evidence.Observation;
        if (frame.Cancelled || frame.UserActive) return Stop(frame.Cancelled ? "routine-cancelled" : "user-takeover");
        if (frame.Binding != _binding) return Stop("bound-context-changed");
        if (!frame.TargetReady || !frame.InputOwned) return Stop("target-or-input-ownership-lost");
        if (!double.IsFinite(frame.TimeMs) || frame.TimeMs < 0 || frame.TimeMs <= _lastTime)
            return Convert(_runner.Status) with { Reason = "invalid-or-repeated-time" };
        _lastTime = frame.TimeMs;
        if (_firstTime < 0) _firstTime = frame.TimeMs;
        if (frame.TimeMs - _firstTime >= _bundle.TimeoutMs) return Stop("bundle-deadline-exceeded");
        var facts = evidence.VerifiedFacts ?? []; var controls = evidence.VerifiedControls ?? [];
        OperationModules.CheckNames(facts, 64); OperationModules.CheckNames(controls, 64);
        // At most sixteen modules can finish from a single observation. Each still
        // checks its own facts, entry policy and post-action evidence.
        while (true)
        {
            var module = _modules[_index];
            var contextReady = frame.ContextVerified && module.Spec.RequiredFacts.All(facts.Contains);
            var nodeId = _runner.Status.NodeId;
            var consumesOutcome = _pending is not null && _receiptTime >= 0 && frame.TimeMs > _receiptTime &&
                frame.Fresh && contextReady && _pending.ExpectedScenes.Contains(frame.Scene) && frame.OutcomeForProposalId == _proposal;
            if (consumesOutcome) nodeId = _pending!.NextNode;
            var candidate = module.Graph.Nodes.Single(node => node.Id == nodeId).Edges.SingleOrDefault(edge => edge.Scene == frame.Scene);
            var action = candidate is null ? null : module.Actions.Single(item => item.ActionId == candidate.ActionId);
            var decision = _runner.Observe(frame with
            {
                ContextVerified = contextReady,
                ControlVerified = frame.ControlVerified && action is not null && controls.Contains(action.ControlId)
            });
            if (consumesOutcome) { _pending = null; _proposal = null; _receiptTime = -1; }
            if (decision.Stopped) return Convert(decision);
            if (decision.IsProposal)
            {
                if (_actions >= _bundle.MaxActions) return Stop("bundle-action-budget-exceeded");
                _actions++;
                _pending = module.Graph.Nodes.Single(node => node.Id == decision.NodeId).Edges.Single(edge => edge.ActionId == decision.ActionId && edge.Scene == frame.Scene);
                _proposal = decision.ProposalId; _receiptTime = -1;
                return Convert(decision);
            }
            if (!decision.Completed) return Convert(decision);
            CompletedModules++;
            if (_index == _modules.Length - 1) { _completed = true; return Status; }
            _index++; _pending = null; _proposal = null; _receiptTime = -1;
            _runner = NewRunner();
        }
    }

    public void RecordReceipt(RoutineReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (_completed || _stopReason is not null || _runner.Status.Stopped) return;
        if (_firstTime >= 0 && double.IsFinite(receipt.TimeMs) && receipt.TimeMs - _firstTime >= _bundle.TimeoutMs)
        { Stop("bundle-deadline-exceeded"); return; }
        _runner.RecordReceipt(receipt);
        if (!_runner.Status.Stopped) { _receiptTime = receipt.TimeMs; _lastTime = receipt.TimeMs; }
    }

    private RoutineRunner NewRunner() => new(_modules[_index].Graph, _binding,
        _modules[_index].Actions.Select(action => action.ActionId).ToHashSet(StringComparer.Ordinal),
        _runId + ".m" + _index.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private ModuleDecision Convert(RoutineDecision decision) => new(_modules[_index].Spec.Id, decision.NodeId,
        decision.Reason, decision.ProposalId, decision.ActionId,
        decision.ActionId is null ? null : _modules[_index].Actions.Single(action => action.ActionId == decision.ActionId),
        decision.Completed, decision.Stopped);
    private ModuleDecision Stop(string reason) { _stopReason = reason; _pending = null; _proposal = null; return Status; }
}
