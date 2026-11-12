using System.Text.Json.Serialization;

namespace ProjectMaelstrom.Utilities;

internal sealed record MenuStep(string Id, string FromScene, string ToScene, ModuleAction Action);
internal sealed record MenuConfiguration(MenuStep[] Steps, bool AllowIntermediateEntry = false,
    bool AllowAlreadyComplete = false, double TimeoutMs = 15000, double TransitionTimeoutMs = 10000,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProgressScene = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int MaxActions = 0);

/// <summary>Compiles a finite, observed menu path using the shared routine graph.
/// Optional adoption of an intermediate or completed menu state is explicit.
/// No coordinates, game-specific keys, callbacks or input dispatch live here.</summary>
internal static class MenuModule
{
    internal const int MaximumSteps = 16;

    public static OperationModuleDefinition Compile(OperationModuleSpec spec)
    {
        OperationModules.ValidateSpec(spec);
        if (spec.Kind != "menu") throw new InvalidDataException("Choose a menu module specification.");
        var configuration = OperationModules.ReadConfiguration<MenuConfiguration>(spec);
        var steps = configuration.Steps;
        if (steps is null || steps.Length is < 1 or > MaximumSteps || steps.Any(step => step is null || step.Action is null) ||
            !double.IsFinite(configuration.TimeoutMs) || configuration.TimeoutMs <= 0 || configuration.TimeoutMs > 300000 ||
            !double.IsFinite(configuration.TransitionTimeoutMs) || configuration.TransitionTimeoutMs <= 0 ||
            configuration.TransitionTimeoutMs > configuration.TimeoutMs)
            throw new InvalidDataException("Choose 1–16 menu steps and finite bounded transition/overall timeouts.");
        OperationModules.CheckNames(steps.Select(step => step.Id).ToArray(), MaximumSteps);
        foreach (var step in steps)
        {
            OperationModules.CheckId(step.FromScene); OperationModules.CheckId(step.ToScene);
            if (step.FromScene is "Unknown" or "Loading" || step.ToScene is "Unknown" or "Loading")
                throw new InvalidDataException("Unknown and Loading are pauses, not menu action/outcome scenes.");
        }
        if (steps.Where((step, i) => i > 0 && steps[i - 1].ToScene != step.FromScene).Any())
            throw new InvalidDataException("Menu steps must describe a contiguous observed path.");
        if (configuration.AllowIntermediateEntry && steps.Select(step => step.FromScene).Distinct(StringComparer.Ordinal).Count() != steps.Length)
            throw new InvalidDataException("Intermediate menu entry needs an unambiguous scene for every step.");
        MenuStep[] entrySteps = configuration.AllowIntermediateEntry ? steps : [steps[0]];
        var completeScene = steps[^1].ToScene;
        if (configuration.AllowAlreadyComplete && entrySteps.Any(step => step.FromScene == completeScene))
            throw new InvalidDataException("An already-complete scene cannot also request an entry action.");
        var groups = steps.GroupBy(step => step.Action.ActionId, StringComparer.Ordinal).ToArray();
        if (groups.Any(group => group.Select(step => step.Action).Distinct().Count() != 1))
            throw new InvalidDataException("A repeated action ID must keep exactly one action binding.");
        var actions = groups.Select(group => group.First().Action).ToArray();
        OperationModules.ValidateActions(actions);

        // Event-driven menus may have an unknown number of pages. A changed,
        // receipt-bound page is progress; elapsed time never authorizes a retry.
        if (configuration.ProgressScene is { } progressScene)
        {
            OperationModules.CheckId(progressScene);
            if (steps.Length != 1 || configuration.AllowIntermediateEntry || configuration.AllowAlreadyComplete ||
                configuration.MaxActions is < 1 or > MaximumSteps ||
                progressScene is "Unknown" or "Loading" || progressScene == steps[0].FromScene || progressScene == steps[0].ToScene)
                throw new InvalidDataException("Repeated menus require one step, a distinct progress scene and 1–16 actions.");
            RoutineEdge Advance(string id, string scene) => new(id, scene, steps[0].Action.ActionId,
                [progressScene, completeScene], "pages", configuration.TransitionTimeoutMs, RequiresControl: true, RequiresOutcomeBinding: true);
            return OperationModules.Define(spec, new(1, spec.Id, spec.GameId, "entry", configuration.TimeoutMs,
                configuration.MaxActions, ["Unknown", "Loading"],
                [new("entry", [Advance("begin", steps[0].FromScene)]), new("pages", [Advance("continue", progressScene)], completeScene)]), actions);
        }

        RoutineEdge Edge(int index, string prefix) => new(prefix + steps[index].Id,
            steps[index].FromScene, steps[index].Action.ActionId, [steps[index].ToScene],
            index + 1 == steps.Length ? "done" : "step-" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            configuration.TransitionTimeoutMs, RequiresControl: true, RequiresOutcomeBinding: true);
        var nodes = new List<RoutineNode>
        {
            new("entry", Enumerable.Range(0, entrySteps.Length).Select(index => Edge(index, "enter-")).ToArray(),
                configuration.AllowAlreadyComplete ? completeScene : null)
        };
        for (var i = 1; i < steps.Length; i++)
            nodes.Add(new("step-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), [Edge(i, "advance-")]));
        nodes.Add(new("done", [], completeScene));
        var graph = new RoutineGraph(1, spec.Id, spec.GameId, "entry", configuration.TimeoutMs,
            steps.Length, ["Unknown", "Loading"], nodes.ToArray());
        return OperationModules.Define(spec, graph, actions);
    }
}
