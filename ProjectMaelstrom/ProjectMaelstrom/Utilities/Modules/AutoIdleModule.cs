namespace ProjectMaelstrom.Utilities;

internal sealed record AutoIdleConfiguration(string Mode, ModuleAction[] Pulses,
    string EntryScene = "IdleAreaReady", string[]? OutcomeScenes = null,
    MenuConfiguration? NativeMenu = null, double TimeoutMs = 30000,
    string NativeIdleScene = "NativeIdle");

/// <summary>Compiles one finite idle operation to the shared graph contract. Native
/// idle delegates to MenuModule; motion contains no wake, recurring timer or input API.
/// Game-specific keys, controls and required facts come from the specification.</summary>
internal static class AutoIdleModule
{
    public static OperationModuleDefinition Compile(OperationModuleSpec spec)
    {
        OperationModules.ValidateSpec(spec);
        if (spec.Kind != "auto-idle") throw new InvalidDataException("Expected an auto-idle module.");
        var configuration = OperationModules.ReadConfiguration<AutoIdleConfiguration>(spec);
        if (configuration.Mode is not ("native" or "spin" or "step" or "routine") ||
            configuration.Pulses is null || !double.IsFinite(configuration.TimeoutMs) ||
            configuration.TimeoutMs <= 0 || configuration.TimeoutMs > 300000)
            throw new InvalidDataException("Invalid idle mode, pulses or finite timeout.");
        OperationModules.CheckId(configuration.NativeIdleScene);
        if (configuration.NativeIdleScene is "Loading" or "Unknown")
            throw new InvalidDataException("Native idle needs its own observed terminal scene.");

        if (configuration.Mode == "native")
        {
            var native = configuration.NativeMenu;
            if (configuration.Pulses.Length != 0 || configuration.OutcomeScenes is { Length: > 0 } ||
                native is null || native.Steps is not { Length: > 0 } ||
                native.Steps.Any(step => step is null) || native.Steps[^1].ToScene != configuration.NativeIdleScene ||
                native.Steps.Any(step => step.FromScene == configuration.NativeIdleScene))
                throw new InvalidDataException("Native idle needs an explicit terminal menu path and no motion or wake step.");
            var menu = native with
            {
                TimeoutMs = Math.Min(configuration.TimeoutMs, native.TimeoutMs)
            };
            // MenuModule validates its transition timeout against the effective bound.
            var childSpec = OperationModules.Spec(spec.Id, "menu", spec.GameId, menu, spec.RequiredFacts);
            var child = MenuModule.Compile(childSpec);
            return OperationModules.Define(spec, child.Graph, child.Actions);
        }

        if (configuration.NativeMenu is not null || configuration.Pulses.Length is < 1 or > 8 ||
            configuration.Pulses.Any(pulse => pulse is null || pulse.Kind != "keyboard-pulse" ||
                pulse.DurationMs is < 40 or > 150) ||
            configuration.Pulses.Sum(pulse => pulse.DurationMs) > 600 ||
            configuration.OutcomeScenes is null || configuration.OutcomeScenes.Length != configuration.Pulses.Length)
            throw new InvalidDataException("A motion cycle needs 1–8 keyboard pulses, at most 600 ms, and one explicit outcome per pulse.");
        OperationModules.CheckId(configuration.EntryScene);
        string[] pauses = ["Loading", "Unknown", configuration.NativeIdleScene];
        if (pauses.Contains(configuration.EntryScene))
            throw new InvalidDataException("A motion cycle cannot begin in an uncertain or native-idle scene.");
        foreach (var scene in configuration.OutcomeScenes)
        {
            OperationModules.CheckId(scene);
            if (pauses.Contains(scene))
                throw new InvalidDataException("Loading, unknown or native idle cannot prove a motion outcome.");
        }
        var groups = configuration.Pulses.GroupBy(pulse => pulse.ActionId, StringComparer.Ordinal).ToArray();
        if (groups.Any(group => group.Any(pulse => pulse != group.First())))
            throw new InvalidDataException("A repeated action ID must retain exactly the same binding.");
        var actions = groups.Select(group => group.First()).ToArray();
        OperationModules.ValidateActions(actions);

        var nodes = new List<RoutineNode>();
        for (var i = 0; i < configuration.Pulses.Length; i++)
        {
            var from = i == 0 ? configuration.EntryScene : configuration.OutcomeScenes[i - 1];
            var next = i == configuration.Pulses.Length - 1 ? "done" : "pulse-" + (i + 1);
            nodes.Add(new("pulse-" + i,
                [new("pulse-" + i, from, configuration.Pulses[i].ActionId,
                    [configuration.OutcomeScenes[i]], next, Math.Min(5000, configuration.TimeoutMs),
                    RequiresControl: true, RequiresOutcomeBinding: true)]));
        }
        nodes.Add(new("done", [], configuration.OutcomeScenes[^1]));
        var graph = new RoutineGraph(1, spec.Id, spec.GameId, "pulse-0", configuration.TimeoutMs,
            configuration.Pulses.Length, pauses, nodes.ToArray());
        return OperationModules.Define(spec, graph, actions);
    }
}
