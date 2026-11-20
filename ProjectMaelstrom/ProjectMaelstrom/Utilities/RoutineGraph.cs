using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectMaelstrom.Utilities;

internal sealed record RoutineEdge(string Id, string Scene, string ActionId, string[] ExpectedScenes,
    string NextNode, double TimeoutMs, bool RequiresControl = false, bool RequiresOutcomeBinding = false);
internal sealed record RoutineNode(string Id, RoutineEdge[] Edges, string? CompleteScene = null);
internal sealed record RoutineGraph(int SchemaVersion, string Id, string GameId, string StartNode,
    double TimeoutMs, int MaxActions, string[] PauseScenes, RoutineNode[] Nodes);
internal sealed record RoutineBinding(string TargetId, string GeometryId, string ContextId);
internal sealed record RoutineObservation(double TimeMs, string Scene, RoutineBinding Binding,
    bool Fresh = false, bool TargetReady = false, bool ContextVerified = false, bool InputOwned = false,
    bool ControlVerified = false, bool Cancelled = false, bool UserActive = false,
    string? OutcomeForProposalId = null);
internal sealed record RoutineReceipt(string ProposalId, string ActionId, double TimeMs, bool Accepted, bool InputReleased);
internal sealed record RoutineDecision(string NodeId, string Reason, string? ProposalId = null,
    string? ActionId = null, bool Completed = false, bool Stopped = false)
{
    public bool IsProposal => ProposalId is not null;
}

/// <summary>Versioned data, never scripts or input delegates. Live adapters supply their
/// own trusted action IDs; loading a file grants no dispatch capability.</summary>
internal static class RoutineGraphs
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
        WriteIndented = true
    };
    public static RoutineGraph Read(string json)
    {
        if (json.Length > 65536) throw new InvalidDataException("Routine exceeds 64K characters.");
        using var document = JsonDocument.Parse(json, new() { MaxDepth = 16 });
        CheckProperties(document.RootElement);
        var graph = JsonSerializer.Deserialize<RoutineGraph>(json, Json) ?? throw new InvalidDataException("Missing routine.");
        Validate(graph);
        return graph;
    }

    internal static void CheckProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate JSON property.");
                CheckProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckProperties(item);
    }

    public static RoutineGraph BuiltIn(string name)
    {
        if (name is not ("pet-dance-postgame" or "pet-dance-visit-draft" or "pet-dance-visit" or "dodge-movement-draft" or "combat-turn-decision"))
            throw new ArgumentException("Unknown built-in routine.", nameof(name));
        using var stream = typeof(RoutineGraphs).Assembly.GetManifestResourceStream("Maelstrom.Routines." + name + ".json")
            ?? throw new InvalidDataException("Missing built-in routine.");
        using var reader = new StreamReader(stream);
        return Read(reader.ReadToEnd());
    }

    public static void Validate(RoutineGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        static bool Name(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
            value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
        static bool Budget(double value) => double.IsFinite(value) && value > 0 && value <= 300000;
        if (graph.SchemaVersion != 1 || !Name(graph.Id) || !Name(graph.GameId) || !Name(graph.StartNode) ||
            !Budget(graph.TimeoutMs) || graph.MaxActions is < 1 or > 10000 ||
            graph.Nodes is null || graph.Nodes.Length is < 1 or > 128 ||
            graph.PauseScenes is null || graph.PauseScenes.Length > 16 || graph.PauseScenes.Any(s => !Name(s)))
            throw new InvalidDataException("Invalid routine identity, limits or nodes.");
        var nodes = new Dictionary<string, RoutineNode>(StringComparer.Ordinal);
        var edges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            if (node is null || !Name(node.Id) || !nodes.TryAdd(node.Id, node) || node.Edges is null ||
                node.Edges.Length > 32 || (node.CompleteScene is not null && !Name(node.CompleteScene)) ||
                (node.Edges.Length == 0 && node.CompleteScene is null) || graph.PauseScenes.Contains(node.CompleteScene))
                throw new InvalidDataException("Invalid or duplicate routine node.");
            var triggers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edge in node.Edges)
                if (edge is null || !Name(edge.Id) || !edges.Add(edge.Id) || !Name(edge.Scene) || !triggers.Add(edge.Scene) ||
                    edge.Scene == node.CompleteScene || graph.PauseScenes.Contains(edge.Scene) || !Name(edge.ActionId) ||
                    !Name(edge.NextNode) || !Budget(edge.TimeoutMs) || edge.TimeoutMs > graph.TimeoutMs ||
                    edge.ExpectedScenes is null || edge.ExpectedScenes.Length is < 1 or > 16 ||
                    edge.ExpectedScenes.Any(s => !Name(s) || graph.PauseScenes.Contains(s)) ||
                    edge.ExpectedScenes.Distinct(StringComparer.Ordinal).Count() != edge.ExpectedScenes.Length)
                    throw new InvalidDataException("Ambiguous or invalid routine transition.");
        }
        if (!nodes.ContainsKey(graph.StartNode) || !nodes.Values.Any(n => n.CompleteScene is not null) ||
            nodes.Values.SelectMany(n => n.Edges).Any(e => !nodes.ContainsKey(e.NextNode)))
            throw new InvalidDataException("Routine has missing nodes or completion evidence.");
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(); pending.Push(graph.StartNode);
        while (pending.TryPop(out var id))
            if (reachable.Add(id)) foreach (var edge in nodes[id].Edges) pending.Push(edge.NextNode);
        if (reachable.Count != nodes.Count) throw new InvalidDataException("Routine contains unreachable nodes.");
    }
}

/// <summary>Pure, bounded transition runner. The clock and observations are supplied;
/// no capture, OCR, sleeping, input or model is available here. An accepted receipt
/// acknowledges dispatch, while a NEWER observed outcome advances the graph.</summary>
internal sealed class RoutineRunner
{
    private readonly RoutineGraph _graph;
    private readonly RoutineBinding _binding;
    private readonly Dictionary<string, RoutineNode> _nodes;
    private readonly string _runId;
    private string _node;
    private RoutineEdge? _pending;
    private string? _proposal;
    private double _firstTime = -1, _lastTime = -1, _proposedTime, _receiptTime = -1;
    private int _actions;
    private string? _stopReason;
    private bool _completed;

    public RoutineDecision Status => _stopReason is not null ? Wait(_stopReason) : _completed ? Done() : Wait("replay-incomplete");

    public RoutineRunner(RoutineGraph graph, RoutineBinding binding, IReadOnlySet<string> trustedActions, string? replayRunId = null)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(trustedActions);
        if (string.IsNullOrWhiteSpace(binding.TargetId) || string.IsNullOrWhiteSpace(binding.GeometryId) ||
            string.IsNullOrWhiteSpace(binding.ContextId)) throw new ArgumentException("Routine must be bound to a visit and target.", nameof(binding));
        RoutineGraphs.Validate(graph);
        // Snapshot mutable arrays so a caller cannot modify a running plan after validation.
        _graph = RoutineGraphs.Read(JsonSerializer.Serialize(graph, RoutineGraphs.Json));
        if (_graph.Nodes.SelectMany(n => n.Edges).Any(e => !trustedActions.Contains(e.ActionId)))
            throw new InvalidDataException("Routine requests an unregistered action.");
        _binding = binding;
        // Live runs always get a new nonce. Offline replay may supply its recorded
        // attempt ID for deterministic reports; it has no dispatcher.
        _runId = replayRunId ?? Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(_runId) || _runId.Length > 128) throw new ArgumentException("Invalid replay run ID.", nameof(replayRunId));
        _nodes = _graph.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        _node = _graph.StartNode;
    }

    public RoutineDecision Observe(RoutineObservation frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (_stopReason is not null) return Wait(_stopReason);
        // Completion is a historical observed outcome, not a lease that needs
        // renewing. Later desktop activity cannot erase it or restart input.
        if (_completed)
            return frame.Binding == _binding && frame.Fresh && frame.ContextVerified && frame.TargetReady && frame.InputOwned &&
                !frame.Cancelled && !frame.UserActive && frame.Scene == _nodes[_node].CompleteScene
                ? Done() : Wait("closed-routine-left-completion");
        if (frame.Cancelled || frame.UserActive) return Stop(frame.Cancelled ? "routine-cancelled" : "user-takeover");
        if (frame.Binding != _binding) return Stop("bound-context-changed");
        if (!frame.TargetReady || !frame.InputOwned) return Stop("target-or-input-ownership-lost");
        if (!double.IsFinite(frame.TimeMs) || frame.TimeMs < 0 || frame.TimeMs <= _lastTime)
            return Wait("invalid-or-repeated-time");
        _lastTime = frame.TimeMs;
        if (_firstTime < 0) _firstTime = frame.TimeMs;
        if (!_completed && frame.TimeMs - _firstTime >= _graph.TimeoutMs) return Stop("routine-deadline-exceeded");
        if (_pending is not null && frame.TimeMs - _proposedTime >= _pending.TimeoutMs)
            return Stop("transition-deadline-exceeded");
        if (!frame.Fresh || !frame.ContextVerified) return Wait("observation-not-ready");
        if (_graph.PauseScenes.Contains(frame.Scene)) return Wait("scene-pause");
        if (_pending is not null)
        {
            if (_receiptTime < 0) return Wait("dispatch-receipt-required");
            if (frame.TimeMs <= _receiptTime || !_pending.ExpectedScenes.Contains(frame.Scene)) return Wait("observed-outcome-required");
            // A held state (for example, Progress) is not evidence that another
            // action worked. Qualified adapters bind a new measured outcome to
            // its exact proposal; later timestamps alone cannot reuse that proof.
            if (_pending.RequiresOutcomeBinding && frame.OutcomeForProposalId != _proposal)
                return Wait("outcome-proposal-binding-required");
            _node = _pending.NextNode;
            _pending = null; _proposal = null; _receiptTime = -1;
        }
        var node = _nodes[_node];
        if (node.CompleteScene == frame.Scene) { _completed = true; return Done(); }
        var edge = node.Edges.SingleOrDefault(e => e.Scene == frame.Scene);
        if (edge is null) return Wait("scene-not-applicable");
        if (edge.RequiresControl && !frame.ControlVerified) return Wait("action-control-unverified");
        if (_actions >= _graph.MaxActions) return Stop("action-budget-exceeded");
        _pending = edge; _proposedTime = frame.TimeMs;
        _proposal = _runId + "/" + _graph.Id + "/" + (++_actions).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new(_node, "action-proposed", _proposal, edge.ActionId);
    }

    public void RecordReceipt(RoutineReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (_stopReason is not null || _completed) return;
        if (_pending is null || _receiptTime >= 0 || receipt.ProposalId != _proposal || receipt.ActionId != _pending.ActionId ||
            !receipt.Accepted || !receipt.InputReleased || !double.IsFinite(receipt.TimeMs) || receipt.TimeMs < _lastTime ||
            receipt.TimeMs - _firstTime >= _graph.TimeoutMs || receipt.TimeMs - _proposedTime >= _pending.TimeoutMs)
        { Stop("dispatch-unconfirmed"); return; }
        _receiptTime = receipt.TimeMs;
        _lastTime = receipt.TimeMs;
    }

    private RoutineDecision Wait(string reason) => new(_node, reason, Stopped: _stopReason is not null);
    private RoutineDecision Stop(string reason) { _stopReason = reason; _pending = null; _proposal = null; return Wait(reason); }
    private RoutineDecision Done() => new(_node, "observed-completion", Completed: true);
}
