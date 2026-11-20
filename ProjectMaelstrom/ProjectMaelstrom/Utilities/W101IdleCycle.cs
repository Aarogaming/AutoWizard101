namespace ProjectMaelstrom.Utilities;

/// <summary>One pure, receipt-bound Idle departure/options/Idle cycle. No input,
/// capture or movement exists here. AwakeCandidate grants only the next options
/// action after this visit's accepted wake; it is never general outside permission.</summary>
internal sealed class W101IdleCycle
{
    internal const string Id = "w101-idle-cycle";
    internal const int TimeoutMs = 60000;
    internal static readonly ModuleAction Wake = new("wizard101.wake-idle", "keyboard-pulse", "ESCAPE", 40, "native-idle.wake");
    internal static readonly ModuleAction OpenMenu = new("game.open-menu", "keyboard-pulse", "ESCAPE", 40, "outside-menu-command");
    internal static readonly ModuleAction SelectIdle = new("wizard101.native-idle", "verified-control-click", null, 40, "game-menu.idle");
    internal static readonly ModuleAction CloseMenu = new("game.close-menu", "keyboard-pulse", "ESCAPE", 40, "game-menu.close");
    private static readonly ModuleAction[] ActionsById = [Wake, OpenMenu, SelectIdle, CloseMenu];
    private readonly string _mode;
    private readonly RoutineRunner _runner;
    private readonly RoutineBinding _binding;
    private string? _pendingProposal, _pendingAction;
    private bool _accepted, _awakened;
    private double _receiptMs = -1;
    internal int Actions { get; private set; }
    internal ModuleDecision Status => Convert(_runner.Status);

    internal static RoutineBinding Bind(WizwalkerTarget target, string geometryId, string runId) =>
        new("pid-" + target.ProcessId + "-start-" + target.StartedUtcTicks, geometryId, Id + ".visit." + runId);

    internal static RoutineGraph Graph() => new(1, Id, "wizard101", "wake", TimeoutMs, 3, ["Loading"],
    [
        new("wake", [new("wake-idle", "NativeIdle", Wake.ActionId, ["AwakeFromIdle", "MenuObserved"], "after-wake", 15000, true, true)]),
        new("after-wake",
        [
            new("open-options", "AwakeFromIdle", OpenMenu.ActionId, ["MenuObserved"], "options", 15000, true, true),
            new("idle-from-wake-menu", "MenuObserved", SelectIdle.ActionId, ["NativeIdle"], "done", 15000, true, true)
        ]),
        new("options", [new("idle-from-options", "MenuObserved", SelectIdle.ActionId, ["NativeIdle"], "done", 15000, true, true)]),
        new("done", [], "NativeIdle")
    ]);

    internal static RoutineGraph Graph(string mode) => mode switch
    {
        "round-trip" => Graph(),
        "wake" => new(1, Id, "wizard101", "wake", TimeoutMs, 2, ["Loading"],
        [
            new("wake", [new("wake-idle", "NativeIdle", Wake.ActionId, ["AwakeFromIdle", "MenuObserved"], "after-wake", 15000, true, true)], "AlreadyAwake"),
            new("after-wake", [new("close-options", "MenuObserved", CloseMenu.ActionId, ["AwakeFromIdle"], "done", 15000, true, true)], "AwakeFromIdle"),
            new("done", [], "AwakeFromIdle")
        ]),
        "idle" => new(1, Id, "wizard101", "entry", TimeoutMs, 2, ["Loading"],
        [
            new("entry", [
                new("open-options", "AwakeForIdle", OpenMenu.ActionId, ["MenuObserved"], "options", 15000, true, true),
                new("select-idle", "MenuObserved", SelectIdle.ActionId, ["NativeIdle"], "done", 15000, true, true)
            ], "NativeIdle"),
            new("options", [new("select-idle-after-menu", "MenuObserved", SelectIdle.ActionId, ["NativeIdle"], "done", 15000, true, true)]),
            new("done", [], "NativeIdle")
        ]),
        _ => throw new InvalidDataException("Choose round-trip, wake or idle.")
    };

    internal W101IdleCycle(RoutineBinding binding, string runId, string mode = "round-trip")
    {
        OperationModules.CheckId(runId);
        if (binding.ContextId != Id + ".visit." + runId)
            throw new InvalidDataException("Bind the Idle cycle to this exact visit.");
        _binding = binding;
        _mode = mode;
        _runner = new(Graph(mode), binding, ActionsById.Select(action => action.ActionId).ToHashSet(StringComparer.Ordinal), runId);
    }

    internal ModuleDecision Observe(RoutineObservation frame)
    {
        var ready = frame.Binding == _binding && frame.Fresh && frame.TargetReady && frame.ContextVerified &&
            frame.InputOwned && !frame.Cancelled && !frame.UserActive && double.IsFinite(frame.TimeMs);
        var newerReceipt = ready && _accepted && frame.TimeMs > _receiptMs &&
            (frame.OutcomeForProposalId is null || frame.OutcomeForProposalId == _pendingProposal);
        var wakeOutcome = newerReceipt && _pendingAction == Wake.ActionId && frame.Scene is "AwakeCandidate" or "MenuObserved";
        var menuOutcome = newerReceipt && _pendingAction == OpenMenu.ActionId && frame.Scene == "MenuObserved";
        var idleOutcome = newerReceipt && _pendingAction == SelectIdle.ActionId && frame.Scene == "NativeIdle";
        var closeOutcome = newerReceipt && _pendingAction == CloseMenu.ActionId && frame.Scene == "AwakeCandidate";
        var scene = frame.Scene switch
        {
            "AwakeCandidate" when _mode == "wake" && Actions == 0 && frame.ControlVerified => "AlreadyAwake",
            "AwakeCandidate" when _mode == "idle" => "AwakeForIdle",
            "AwakeCandidate" when _awakened || wakeOutcome || closeOutcome => "AwakeFromIdle",
            "NativeIdle" or "MenuObserved" or "Loading" => frame.Scene,
            _ => "Unknown"
        };
        var decision = _runner.Observe(frame with
        {
            Scene = scene,
            ContextVerified = frame.ContextVerified && scene != "Unknown",
            OutcomeForProposalId = wakeOutcome || menuOutcome || idleOutcome || closeOutcome ? _pendingProposal : null
        });
        if (wakeOutcome && !decision.Stopped) _awakened = true;
        if (decision.IsProposal)
        {
            _pendingProposal = decision.ProposalId; _pendingAction = decision.ActionId;
            _accepted = false; _receiptMs = -1; Actions++;
        }
        return Convert(decision);
    }

    internal void RecordReceipt(RoutineReceipt receipt)
    {
        _runner.RecordReceipt(receipt);
        _accepted = !_runner.Status.Stopped && receipt.ProposalId == _pendingProposal &&
            receipt.ActionId == _pendingAction && receipt.Accepted && receipt.InputReleased;
        if (_accepted) _receiptMs = receipt.TimeMs;
    }

    private static ModuleDecision Convert(RoutineDecision decision) => new(Id, decision.NodeId, decision.Reason,
        decision.ProposalId, decision.ActionId,
        decision.ActionId is null ? null : ActionsById.Single(action => action.ActionId == decision.ActionId),
        decision.Completed, decision.Stopped);
}
