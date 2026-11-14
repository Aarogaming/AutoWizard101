namespace ProjectMaelstrom.Utilities;

internal enum PetDanceScene
{
    OutsideNoSigil, DanceSigil, InstructionsUnselected, InstructionsReady, Loading,
    Watch, Go, RoundFeedback, GameResult, SnackOffer, OutsideAfterGame, MenuObserved,
    NativeIdle, Unknown
}
internal enum PetDanceLifecycleAction
{
    ApproachRoute, InteractX, ChooseLevel, Play, Wait, TeacherHandoff, Next,
    FinishWithoutSnack, OptionalSnackPlannerHandoff, OpenMenu, SelectIdle, Done, QuitToCharacterSelection, SelectWizardCandidate, EnterSelectedWizard, ExitWorldGate, TravelWorldGate, SelectWorldGate
}
internal sealed record PetDanceLifecycleOptions(string TargetId, Size Geometry, string ContextId,
    string? LevelId = null, string? ApproachRouteId = null, bool ApproachRouteQualified = false,
    bool SnacksEnabled = false);
internal sealed record PetDanceLifecycleObservation(double TimeMs, string TransitionId, PetDanceScene Scene,
    string TargetId, Size Geometry, string ContextId, bool Fresh = false, bool TargetReady = false,
    bool ContextVerified = false, bool TakenOver = false, string? SelectedLevelId = null,
    bool OutcomeVerified = false, bool SnackReviewComplete = false);
internal sealed record PetDanceLifecyclePlan(PetDanceLifecycleAction Action, bool IsProposal,
    string Reason, string[] EvidenceNeeded, string? Argument = null, bool LoadingPaused = false,
    bool Completed = false);

/// <summary>One whole-game visit, driven by caller-verified scenes. Proposes only;
/// implements no detector, route, clicks, feeding or input authorization.</summary>
internal sealed class PetDanceLifecycle
{
    private readonly PetDanceLifecycleOptions _options;
    private readonly Dictionary<string, PetDanceScene> _observedTransitions = new(StringComparer.Ordinal);
    private readonly HashSet<PetDanceLifecycleAction> _proposedActions = [];
    private double _lastTime = -1;
    private bool _interrupted, _started, _resultSeen, _outsideSeen, _completed;

    public PetDanceLifecycle(PetDanceLifecycleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.TargetId) || string.IsNullOrWhiteSpace(options.ContextId) ||
            options.Geometry.Width <= 0 || options.Geometry.Height <= 0)
            throw new ArgumentException("Bind a target, geometry and verified game context.", nameof(options));
        _options = options;
    }

    public PetDanceLifecyclePlan Observe(PetDanceLifecycleObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.TakenOver) _interrupted = true;
        if (_interrupted) return Wait("visit-interrupted", "Release input; explicitly start a new visit after takeover or context change.");
        if ((!string.IsNullOrWhiteSpace(observation.TargetId) && observation.TargetId != _options.TargetId) ||
            (observation.Geometry.Width > 0 && observation.Geometry.Height > 0 && observation.Geometry != _options.Geometry) ||
            (!string.IsNullOrWhiteSpace(observation.ContextId) && observation.ContextId != _options.ContextId))
        {
            _interrupted = true;
            return Wait("bound-context-changed", "Rebind and recalibrate in a new visit; do not resume pending actions.");
        }
        if (!double.IsFinite(observation.TimeMs) || observation.TimeMs < 0 || observation.TimeMs <= _lastTime)
            return Wait("invalid-or-repeated-time", "A newer, finite observation timestamp.");
        _lastTime = observation.TimeMs;
        if (!observation.Fresh || !observation.TargetReady || !observation.ContextVerified)
            return Wait("observation-not-ready", "Fresh selected-window pixels, target readiness and verified scene context.");
        if (observation.TargetId != _options.TargetId || observation.Geometry != _options.Geometry || observation.ContextId != _options.ContextId)
            return Wait("bound-context-unverified", "Known target identity, geometry and context matching this visit.");
        if (string.IsNullOrWhiteSpace(observation.TransitionId) || observation.TransitionId.Length > 160 || !Enum.IsDefined(observation.Scene))
            return Wait("invalid-scene-identity", "A verified scene and stable transition identity, not a frame number.");
        if (_observedTransitions.TryGetValue(observation.TransitionId, out var previousScene))
            return new(PetDanceLifecycleAction.Wait, false,
                previousScene == observation.Scene ? "repeated-scene-transition" : "conflicting-scene-transition",
                ["A new verified logical transition; reused identities cannot advance visit milestones."],
                LoadingPaused: previousScene == PetDanceScene.Loading && observation.Scene == PetDanceScene.Loading,
                Completed: _completed && previousScene == PetDanceScene.NativeIdle && observation.Scene == PetDanceScene.NativeIdle);
        _observedTransitions.Add(observation.TransitionId, observation.Scene);
        if (_completed)
            return observation.Scene == PetDanceScene.NativeIdle ? Done() : Wait("completed-visit-left-idle", "This visit is closed; explicitly start another visit.");

        switch (observation.Scene)
        {
            case PetDanceScene.Loading:
                return new(PetDanceLifecycleAction.Wait, false, "loading-pause",
                    ["Pause inputs and the gameplay clock; refresh the scene and discard partial cue sequences before resuming."], LoadingPaused: true);
            case PetDanceScene.Unknown:
                return Wait("unknown-scene", "Reobserve; keep previous proposals unresolved and do not retry clicks.");
            case PetDanceScene.OutsideNoSigil:
                if (_started || Proposed(PetDanceLifecycleAction.Play)) return Wait("unexpected-outside-scene", "A verified whole-game result and return outside, or explicit recovery.");
                if (!_options.ApproachRouteQualified || string.IsNullOrWhiteSpace(_options.ApproachRouteId))
                    return Wait("approach-route-unqualified", "A configured, qualified route from this observed location to the Dance sigil.");
                return Propose(PetDanceLifecycleAction.ApproachRoute, "approach-configured-sigil", "Observe the Dance sigil after the route; no arbitrary movement is supplied.", _options.ApproachRouteId);
            case PetDanceScene.DanceSigil:
                if (_started || Proposed(PetDanceLifecycleAction.Play)) return Wait("visit-already-started", "Reconcile the current game; do not start another game.");
                return Propose(PetDanceLifecycleAction.InteractX, "open-dance-instructions", "Visible Dance sigil/interact prompt and subsequent instructions screen.", "X");
            case PetDanceScene.InstructionsUnselected:
                if (Proposed(PetDanceLifecycleAction.Play)) return Wait("play-outcome-unresolved", "Reobserve the existing Play outcome; do not restart.");
                if (string.IsNullOrWhiteSpace(_options.LevelId)) return Wait("level-not-configured", "An explicitly selected level profile and visible selection control.");
                return Propose(PetDanceLifecycleAction.ChooseLevel, "select-configured-level", "Observe the requested level selected before Play.", _options.LevelId);
            case PetDanceScene.InstructionsReady:
                if (string.IsNullOrWhiteSpace(_options.LevelId) || observation.SelectedLevelId != _options.LevelId)
                    return Wait("level-selection-unverified", "Fresh evidence of the configured level selection and Play control.");
                return Propose(PetDanceLifecycleAction.Play, "start-one-game", "Observe loading and the beginning of a complete Watch sequence.");
            case PetDanceScene.Watch:
                if (!Proposed(PetDanceLifecycleAction.Play)) return Wait("game-start-unassociated", "A beginning associated with this visit; do not adopt an unknown partial round.");
                var handoff = Propose(PetDanceLifecycleAction.TeacherHandoff, "handoff-observed-watch", "Qualified visual cue teacher, complete Watch beginning, shared owner and fresh per-key input checks.");
                if (handoff.IsProposal) _started = true;
                return handoff;
            case PetDanceScene.Go:
            case PetDanceScene.RoundFeedback:
                return Wait("teacher-observes-round", "The cue teacher handles variable arrows and round feedback; await a distinct whole-game result.");
            case PetDanceScene.GameResult:
                if (!_started || !observation.OutcomeVerified) return Wait("whole-game-result-unverified", "A verified whole-game result associated with this visit, not round feedback.");
                var next = Propose(PetDanceLifecycleAction.Next, "advance-verified-game-result", "A visible result Next control and the resulting snack offer or outside scene.");
                if (next.IsProposal) _resultSeen = true;
                return next;
            case PetDanceScene.SnackOffer:
                if (!_resultSeen) return Wait("snack-opportunity-unassociated", "A verified result from this visit and a uniquely identified feeding opportunity.");
                if (_options.SnacksEnabled)
                {
                    if (!Proposed(PetDanceLifecycleAction.OptionalSnackPlannerHandoff))
                        return Propose(PetDanceLifecycleAction.OptionalSnackPlannerHandoff, "request-optional-snack-review", "Separate pet/snack identity, reserves, budgets, opportunity ID and reconciliation; this handoff never feeds.");
                    if (!observation.SnackReviewComplete) return Wait("snack-review-unresolved", "Separate snack review completion; do not repeat feeding or skip unresolved consumption.");
                }
                return Propose(PetDanceLifecycleAction.FinishWithoutSnack, "leave-snack-screen-without-feeding", "A verified leave/finish control that consumes no snack and returns outside.");
            case PetDanceScene.OutsideAfterGame:
                if (!_started || !_resultSeen) return Wait("outside-return-unverified", "A whole-game result and observed return outside associated with this visit.");
                var menu = Propose(PetDanceLifecycleAction.OpenMenu, "prepare-native-idle", "Observe the actual game menu; no departure route or map movement is invented.");
                if (menu.IsProposal) _outsideSeen = true;
                return menu;
            case PetDanceScene.MenuObserved:
                if (!_outsideSeen) return Wait("menu-before-verified-return", "Verified outside return after this game before ending the visit.");
                return Propose(PetDanceLifecycleAction.SelectIdle, "select-native-idle", "The menu's actual native Idle control and subsequent visible Idle confirmation.");
            case PetDanceScene.NativeIdle:
                if (!_started || !_resultSeen || !_outsideSeen)
                    return Wait("initial-idle-is-not-completion", "Explicitly leave Idle through the UI and start the requested visit; an initial Idle frame is not a played game.");
                _completed = true;
                return Done();
            default:
                return Wait("unknown-scene", "Reobserve the selected game.");
        }
    }

    private bool Proposed(PetDanceLifecycleAction action) => _proposedActions.Contains(action);
    private PetDanceLifecyclePlan Propose(PetDanceLifecycleAction action, string reason, string evidence, string? argument = null)
    {
        if (Proposed(action))
            return Wait("proposal-outcome-unresolved", "Observe the expected next scene; repeated frames, new IDs or uncertainty do not authorize another click.");
        _proposedActions.Add(action);
        return new(action, true, reason, [evidence], argument);
    }
    private static PetDanceLifecyclePlan Wait(string reason, string evidence) => new(PetDanceLifecycleAction.Wait, false, reason, [evidence]);
    private static PetDanceLifecyclePlan Done() => new(PetDanceLifecycleAction.Done, false, "game-finished-and-native-idle-observed", [], Completed: true);
}
