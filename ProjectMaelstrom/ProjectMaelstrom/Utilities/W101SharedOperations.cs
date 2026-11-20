namespace ProjectMaelstrom.Utilities;

internal sealed record W101RouteSegment(string Id, string FromLandmark, string ToLandmark, string MotionId);
internal sealed record W101RouteSpecification(string Id, string WorldId, string StartZoneId,
    string DestinationZoneId, string SigilId, W101RouteSegment[] Segments);

/// <summary>Wizard101 data bindings for reusable operation modules. This adapter
/// supplies game vocabulary and preferences, not graphs, a controller or live permission.</summary>
internal static class W101SharedOperations
{
    internal static void CheckId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 40 ||
            value.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new InvalidDataException("W101 identities need 1–40 letters, digits or hyphens.");
    }

    public static string Landmark(string id) { CheckId(id); return "Landmark." + id; }
    public static string Progress(string id) { CheckId(id); return "Progress." + id; }

    public static OperationModuleBundle RouteAndMenuVisit(W101RouteSpecification specification,
        int pulseMs = 60, int maxActions = 40, double timeoutMs = 60000, double transitionTimeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(specification);
        foreach (var id in new[] { specification.Id, specification.WorldId, specification.StartZoneId,
            specification.DestinationZoneId, specification.SigilId }) CheckId(id);
        if (specification.Segments is null || specification.Segments.Length is < 1 or > 8 ||
            specification.Segments.Any(segment => segment is null))
            throw new InvalidDataException("A W101 route needs 1–8 explicit landmark segments.");
        if (pulseMs is < 40 or > 150 || maxActions < specification.Segments.Length + 4 || maxActions > 64 ||
            !double.IsFinite(timeoutMs) || timeoutMs is < 1000 or > 120000 ||
            !double.IsFinite(transitionTimeoutMs) || transitionTimeoutMs <= 0 || transitionTimeoutMs > timeoutMs)
            throw new InvalidDataException("Invalid W101 route pulse, action or time budget.");
        var segments = specification.Segments.Select(segment =>
        {
            CheckId(segment.Id);
            _ = W101Routine.Resolve(segment.MotionId);
            return new NavigationSegment(segment.Id, Landmark(segment.FromLandmark), Landmark(segment.ToLandmark),
                Progress(segment.Id), segment.MotionId);
        }).ToArray();
        var actions = segments.Select(segment => MotionBinding(segment.ActionId, pulseMs)).DistinctBy(action => action.ActionId).ToArray();
        var prefix = "w101-" + specification.Id;
        var world = "wizard101.world." + specification.WorldId;
        var route = OperationModules.Spec(prefix + ".navigation", "navigation", "wizard101",
            new NavigationConfiguration(segments, actions, maxActions - 4, timeoutMs, transitionTimeoutMs),
            world, "wizard101.route." + specification.Id, "wizard101.movement-bindings");
        var sigil = OperationModules.Spec(prefix + ".sigil", "sigil", "wizard101",
            new SigilConfiguration(segments[^1].ToScene, "SigilMenu." + specification.SigilId,
                new("game.interact", "keyboard-pulse", "X", 40, "sigil." + specification.SigilId),
                Math.Min(15000, timeoutMs)), world);
        var cancel = OperationModules.Spec(prefix + ".cancel-menu", "menu", "wizard101",
            new MenuConfiguration([new("cancel", "SigilMenu." + specification.SigilId, "OutsideReady",
                new("game.cancel-menu", "verified-control-click", null, 40, "sigil." + specification.SigilId + ".cancel"))],
                TimeoutMs: Math.Min(15000, timeoutMs), TransitionTimeoutMs: Math.Min(transitionTimeoutMs, 15000)), world);
        var idle = NativeIdleSpec(prefix + ".native-idle", Math.Min(15000, timeoutMs), world);
        // Validate the supplied data through the shared compilers. No graph is built here.
        _ = NavigationModule.Compile(route); _ = SigilModule.Compile(sigil); _ = MenuModule.Compile(cancel); _ = AutoIdleModule.Compile(idle);
        var context = "wizard101.route." + specification.Id + "." + specification.WorldId + "." +
            specification.StartZoneId + "." + specification.DestinationZoneId + "." + specification.SigilId;
        return new(1, "w101-route-" + specification.Id, "wizard101", context, timeoutMs, maxActions, [route, sigil, cancel, idle]);
    }

    /// <summary>Map existing user preferences to one bounded module. Recurrence and
    /// input ownership remain with the existing shared runtime; no Idle wake is defined.</summary>
    public static OperationModuleBundle IdleCycle(string contextId, HomeMovementOptions options)
    {
        CheckId(contextId); ArgumentNullException.ThrowIfNull(options); options.Validate();
        var context = "wizard101.idle." + contextId;
        if (options.Mode == HomeIdleMode.NativeIdle)
        {
            var native = NativeIdleSpec("w101-native-idle", 15000, "wizard101.idle-context");
            _ = AutoIdleModule.Compile(native);
            return new(1, "w101-native-idle-bundle", "wizard101", context, 15000, 2, [native]);
        }
        var motionIds = options.Mode == HomeIdleMode.Routine ? options.RoutineActions!.ToArray()
            : options.Keys.Select(key => W101Routine.Primitives.Single(primitive => primitive.Key == key).Id).ToArray();
        var mode = options.Mode switch
        {
            HomeIdleMode.SpinLeft or HomeIdleMode.SpinRight or HomeIdleMode.OpposingTurns => "spin",
            HomeIdleMode.WalkPairs => "step",
            _ => "routine"
        };
        var facts = new List<string> { "wizard101.idle-context", "wizard101.clear-area" };
        if (options.RequiresHome) facts.Add("wizard101.home-confirmed");
        var cycle = OperationModules.Spec("w101-idle-cycle", "auto-idle", "wizard101",
            new AutoIdleConfiguration(mode, motionIds.Select(id => MotionBinding(id, options.PulseMs)).ToArray(),
                OutcomeScenes: Enumerable.Range(1, motionIds.Length).Select(index => "MotionObserved." + index).ToArray()),
            facts.ToArray());
        _ = AutoIdleModule.Compile(cycle);
        return new(1, "w101-idle-cycle-bundle", "wizard101", context, 30000, motionIds.Length, [cycle]);
    }

    private static ModuleAction MotionBinding(string id, int duration) =>
        new(id, "keyboard-pulse", W101Routine.Resolve(id).Key, duration, "verified-movement-area");

    private static OperationModuleSpec NativeIdleSpec(string id, double timeoutMs, params string[] facts) =>
        OperationModules.Spec(id, "auto-idle", "wizard101", new AutoIdleConfiguration("native", [],
            NativeMenu: new MenuConfiguration(
            [
                new("open-menu", "OutsideReady", "MenuObserved", new("game.open-menu", "keyboard-pulse", "ESCAPE", 40, "outside-menu-command")),
                new("select-idle", "MenuObserved", "NativeIdle", new("wizard101.native-idle", "verified-control-click", null, 40, "game-menu.idle"))
            ], AllowIntermediateEntry: true, AllowAlreadyComplete: true, TimeoutMs: timeoutMs,
                TransitionTimeoutMs: Math.Min(10000, timeoutMs)), TimeoutMs: timeoutMs), facts);
}
