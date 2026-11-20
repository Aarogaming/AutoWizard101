namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceVisitOptions(string TargetId, Size Geometry, string ContextId, PetDanceArena Arena,
    bool Enabled = false, double TimeoutMs = 180000);
internal sealed record PetDanceVisitObservation(double TimeMs, string ImageSha256, PetDanceScene Scene,
    string TargetId, Size Geometry, string ContextId, PetDanceEntryScene? Entry = null, RectangleF? ControlBounds = null,
    bool Fresh = false, bool TargetReady = false, bool ContextVerified = false, bool InputOwned = false,
    bool EntryCostApproved = false, bool WatchStartVerified = false, bool SpecialistQualified = false,
    bool WholeGameResultVerified = false, bool UserActive = false, bool Cancelled = false, bool FocusPaused = false);
internal sealed record PetDanceVisitPlan(PetDanceLifecycleAction Action, bool IsProposal, string Reason,
    string? ProposalId = null, RectangleF? ActionBounds = null, PetDanceArena? Arena = null,
    bool ResultObserved = false, bool Completed = false, bool Stopped = false);
internal sealed record PetDanceSpecialistReceipt(string ProposalId, double CompletedMs, string TargetId,
    Size Geometry, string ContextId, PetDanceArena Arena, bool Started = false, bool Completed = false, bool InputReleased = false);

/// <summary>Offline full-visit adapter over the shared receipt/outcome runner. Entry may
/// begin at a qualified sigil or instructions; initial Idle never wakes or completes it.
/// Selection, entry cost, specialist completion and result evidence are independent gates.
/// This class proposes only and is not connected to a live dispatcher.</summary>
internal sealed class PetDanceVisit
{
    private static readonly IReadOnlyDictionary<string, PetDanceLifecycleAction> Actions =
        new Dictionary<string, PetDanceLifecycleAction>(StringComparer.Ordinal)
        {
            ["game.interact"] = PetDanceLifecycleAction.InteractX,
            ["pet-dance.choose-level"] = PetDanceLifecycleAction.ChooseLevel,
            ["game.play"] = PetDanceLifecycleAction.Play,
            ["pet-dance.teacher"] = PetDanceLifecycleAction.TeacherHandoff,
            ["pet-dance.next"] = PetDanceLifecycleAction.Next,
            ["pet-dance.finish-without-snack"] = PetDanceLifecycleAction.FinishWithoutSnack,
            ["game.open-menu"] = PetDanceLifecycleAction.OpenMenu,
            ["wizard101.native-idle"] = PetDanceLifecycleAction.SelectIdle
        };
    private readonly PetDanceVisitOptions _options;
    private readonly RoutineRunner _runner;
    private RoutineDecision? _proposal;
    private bool _specialistCompleted, _resultObserved;

    public PetDanceVisit(PetDanceVisitOptions options, string? replayRunId = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.TargetId) || string.IsNullOrWhiteSpace(options.ContextId) ||
            options.Geometry.Width <= 0 || options.Geometry.Height <= 0 || !Enum.IsDefined(options.Arena) ||
            !double.IsFinite(options.TimeoutMs) || options.TimeoutMs <= 0 || options.TimeoutMs > 300000)
            throw new ArgumentException("Bind one target, geometry, visit and arena with a finite budget.", nameof(options));
        _options = options;
        var graph = RoutineGraphs.BuiltIn("pet-dance-visit");
        graph = graph with
        {
            TimeoutMs = options.TimeoutMs,
            Nodes = graph.Nodes.Select(node => node with
            {
                Edges = node.Edges.Select(edge => edge with { TimeoutMs = Math.Min(edge.TimeoutMs, options.TimeoutMs) }).ToArray()
            }).ToArray()
        };
        _runner = new(graph, Bind(options.TargetId, options.Geometry, options.ContextId),
            Actions.Keys.ToHashSet(StringComparer.Ordinal), replayRunId);
    }

    public PetDanceVisitPlan Status => !_options.Enabled
        ? new(PetDanceLifecycleAction.Wait, false, "visit-disabled", Stopped: true) : Plan(_runner.Status);

    public PetDanceVisitPlan Observe(PetDanceVisitObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!_options.Enabled) return new(PetDanceLifecycleAction.Wait, false, "visit-disabled", Stopped: true);
        // An explicitly observed focus pause with the same bound identity is
        // a bounded observation pause, not loss of the input lease. Feed an
        // untrusted frame through the runner so deadlines still advance. Actual
        // identity/geometry/context changes, takeover and lost ownership remain fatal.
        var focusPaused = observation.FocusPaused && !observation.TargetReady && observation.InputOwned && observation.ContextVerified &&
            !observation.UserActive && !observation.Cancelled && observation.TargetId == _options.TargetId &&
            observation.Geometry == _options.Geometry && observation.ContextId == _options.ContextId;
        var scene = observation.Scene;
        RectangleF? bounds = null; string? withheld = null;
        var validImage = PetDanceEntryVision.IsImageHash(observation.ImageSha256);
        var entry = observation.Entry;
        var boundEntry = entry != null && validImage && entry.ImageSha256 == observation.ImageSha256 &&
            entry.CapturedMs == observation.TimeMs && entry.Geometry == observation.Geometry && entry.Scene == scene;
        if (scene is PetDanceScene.DanceSigil or PetDanceScene.InstructionsUnselected or PetDanceScene.InstructionsReady)
        {
            if (!boundEntry) withheld = "entry-evidence-frame-mismatch";
            else if (scene == PetDanceScene.InstructionsReady && entry!.SelectedArena == _options.Arena)
            {
                bounds = entry.PlayBounds;
                if (!entry.SelectionVerified || !entry.PlayEnabledVerified || entry.LevelControls?.Count > 5 ||
                    entry.LevelControls?.Count(control => control is not null && control.Arena == _options.Arena &&
                        control.IdentityVerified && PetDancePostGameClassifier.ValidBounds(control.Bounds, _options.Geometry)) != 1)
                    withheld = "level-selection-or-enabled-play-unverified";
                else if (!observation.EntryCostApproved) withheld = "entry-cost-not-approved";
            }
            else if (scene is PetDanceScene.InstructionsUnselected or PetDanceScene.InstructionsReady)
            {
                scene = PetDanceScene.InstructionsUnselected;
                var controls = entry!.LevelControls;
                var matching = controls?.Where(control => control is not null && control.Arena == _options.Arena && control.IdentityVerified).ToArray() ?? [];
                if (controls?.Count > 5 || matching.Length != 1) withheld = "requested-level-control-unverified";
                else bounds = matching[0].Bounds;
            }
        }
        else if (scene == PetDanceScene.Watch && (!observation.WatchStartVerified || !observation.SpecialistQualified))
            withheld = "qualified-specialist-and-watch-start-required";
        else if (scene == PetDanceScene.GameResult && (!_specialistCompleted || !observation.WholeGameResultVerified))
            withheld = "completed-specialist-and-whole-game-result-required";
        else if (scene is PetDanceScene.GameResult or PetDanceScene.SnackOffer or PetDanceScene.MenuObserved)
            bounds = observation.ControlBounds;

        var decision = _runner.Observe(new(observation.TimeMs, withheld == null ? scene.ToString() : PetDanceScene.Unknown.ToString(),
            Bind(observation.TargetId, observation.Geometry, observation.ContextId),
            Fresh: observation.Fresh && validImage && Enum.IsDefined(scene) && !focusPaused,
            TargetReady: observation.TargetReady || focusPaused,
            ContextVerified: observation.ContextVerified, InputOwned: observation.InputOwned,
            ControlVerified: bounds is { } box && PetDancePostGameClassifier.ValidBounds(box, _options.Geometry),
            Cancelled: observation.Cancelled, UserActive: observation.UserActive));
        if (decision.IsProposal)
        {
            _proposal = decision;
            if (decision.ActionId == "pet-dance.next") _resultObserved = true;
        }
        return Plan(decision, decision.IsProposal ? bounds : null,
            !decision.Stopped && !decision.Completed && !decision.IsProposal
                ? focusPaused ? "target-not-ready-paused" : withheld : null);
    }

    /// <summary>Use the exact emitted proposal ID and actual release timestamp. Teacher
    /// work must supply RecordSpecialist instead; accepted dispatch alone is not completion.</summary>
    public void RecordDispatch(string proposalId, PetDanceLifecycleAction action, double releasedMs, bool accepted, bool inputReleased)
    {
        var id = Actions.FirstOrDefault(pair => pair.Value == action).Key ?? "unregistered";
        _runner.RecordReceipt(new(proposalId, id, releasedMs, accepted && action != PetDanceLifecycleAction.TeacherHandoff, inputReleased));
        _proposal = null;
    }

    public void RecordSpecialist(PetDanceSpecialistReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var matching = _proposal?.ProposalId == receipt.ProposalId && _proposal.ActionId == "pet-dance.teacher" &&
            receipt.TargetId == _options.TargetId && receipt.Geometry == _options.Geometry &&
            receipt.ContextId == _options.ContextId && receipt.Arena == _options.Arena;
        _runner.RecordReceipt(new(receipt.ProposalId, "pet-dance.teacher", receipt.CompletedMs,
            matching && receipt.Started && receipt.Completed, receipt.InputReleased));
        _specialistCompleted = matching && receipt.Started && receipt.Completed && receipt.InputReleased && !_runner.Status.Stopped;
        _proposal = null;
    }

    private PetDanceVisitPlan Plan(RoutineDecision decision, RectangleF? bounds = null, string? withheld = null)
    {
        var action = decision.ActionId is { } id ? Actions[id] : decision.Completed ? PetDanceLifecycleAction.Done : PetDanceLifecycleAction.Wait;
        return new(action, decision.IsProposal, withheld ?? decision.Reason, decision.ProposalId, bounds,
            action is PetDanceLifecycleAction.ChooseLevel or PetDanceLifecycleAction.Play or PetDanceLifecycleAction.TeacherHandoff ? _options.Arena : null,
            _resultObserved, decision.Completed, decision.Stopped);
    }

    private static RoutineBinding Bind(string target, Size geometry, string context) =>
        new(target, FormattableString.Invariant($"{geometry.Width}x{geometry.Height}"), context);
}
