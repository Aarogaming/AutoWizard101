namespace ProjectMaelstrom.Utilities;

internal sealed record NavigationCorrection(string Scene, string ActionId);
internal sealed record NavigationSegment(string Id, string FromScene, string ToScene,
    string ProgressScene, string ActionId,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    NavigationCorrection[]? Corrections = null);
internal sealed record NavigationConfiguration(NavigationSegment[] Segments, ModuleAction[] Actions,
    int MaxActions = 32, double TimeoutMs = 60000, double TransitionTimeoutMs = 10000);

/// <summary>Game-independent, bounded landmark navigation. The caller supplies scene
/// identities and action bindings. Progress and arrival must name the exact released
/// proposal; durations are input bounds, never an estimate that a destination was reached.</summary>
internal static class NavigationModule
{
    public static OperationModuleDefinition Compile(OperationModuleSpec spec)
    {
        OperationModules.ValidateSpec(spec);
        if (spec.Kind != "navigation") throw new InvalidDataException("Expected a navigation module.");
        var configuration = OperationModules.ReadConfiguration<NavigationConfiguration>(spec);
        OperationModules.ValidateActions(configuration.Actions);
        var segments = configuration.Segments;
        if (segments is null || segments.Length is < 1 or > 16 || segments.Any(segment => segment is null))
            throw new InvalidDataException("Navigation needs 1–16 explicit segments.");
        if (configuration.MaxActions < segments.Length || configuration.MaxActions > 64 ||
            !double.IsFinite(configuration.TimeoutMs) || configuration.TimeoutMs is < 1000 or > 120000 ||
            !double.IsFinite(configuration.TransitionTimeoutMs) || configuration.TransitionTimeoutMs <= 0 ||
            configuration.TransitionTimeoutMs > configuration.TimeoutMs)
            throw new InvalidDataException("Invalid navigation action or time budget.");
        var segmentIds = new HashSet<string>(StringComparer.Ordinal);
        var landmarks = new HashSet<string>(StringComparer.Ordinal);
        var progress = new HashSet<string>(StringComparer.Ordinal);
        var corrections = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            foreach (var name in new[] { segment.Id, segment.FromScene, segment.ToScene, segment.ProgressScene, segment.ActionId })
                OperationModules.CheckId(name);
            if (!segmentIds.Add(segment.Id) || !landmarks.Add(segment.FromScene) || !progress.Add(segment.ProgressScene) ||
                segment.FromScene == segment.ToScene || (i > 0 && segments[i - 1].ToScene != segment.FromScene))
                throw new InvalidDataException("Navigation requires an ordered, distinct landmark chain and progress cues.");
            if (segment.Corrections is { Length: > 4 })
                throw new InvalidDataException("A navigation segment supports at most four observed corrections.");
            foreach (var correction in segment.Corrections ?? [])
            {
                if (correction is null) throw new InvalidDataException("Missing navigation correction.");
                OperationModules.CheckId(correction.Scene);
                OperationModules.CheckId(correction.ActionId);
                if (!corrections.Add(correction.Scene) || correction.Scene is "Loading" or "Unknown")
                    throw new InvalidDataException("Correction scenes must be distinct and cannot be pause scenes.");
            }
        }
        if (!landmarks.Add(segments[^1].ToScene) || landmarks.Overlaps(progress) ||
            landmarks.Concat(progress).Any(scene => scene is "Loading" or "Unknown") ||
            corrections.Overlaps(landmarks) || corrections.Overlaps(progress))
            throw new InvalidDataException("Landmarks and progress cannot conflict with each other or pause scenes.");

        RoutineEdge Move(int index, string scene, string suffix)
        {
            var segment = segments[index];
            return new("move-" + index + "-" + suffix, scene, segment.ActionId,
                [segment.ProgressScene, segment.ToScene, .. (segment.Corrections ?? []).Select(c => c.Scene)], "segment-" + index, configuration.TransitionTimeoutMs,
                RequiresControl: true, RequiresOutcomeBinding: true);
        }
        IEnumerable<RoutineEdge> Correct(int index, string suffix)
        {
            var segment = segments[index];
            return (segment.Corrections ?? []).Select((correction, ordinal) =>
                new RoutineEdge("correct-" + index + "-" + ordinal + "-" + suffix, correction.Scene, correction.ActionId,
                    [segment.FromScene, segment.ProgressScene, .. segment.Corrections!.Select(c => c.Scene)],
                    "segment-" + index, configuration.TransitionTimeoutMs,
                    RequiresControl: true, RequiresOutcomeBinding: true));
        }
        var nodes = new List<RoutineNode> { new("start", [Move(0, segments[0].FromScene, "start"), .. Correct(0, "start")]) };
        for (var i = 0; i < segments.Length; i++)
        {
            var edges = new List<RoutineEdge> { Move(i, segments[i].ProgressScene, "continue") };
            if (segments[i].Corrections is { Length: > 0 })
            {
                edges.Add(Move(i, segments[i].FromScene, "aligned"));
                edges.AddRange(Correct(i, "continue"));
            }
            if (i + 1 < segments.Length) edges.Add(Move(i + 1, segments[i].ToScene, "start"));
            nodes.Add(new("segment-" + i, edges.ToArray(), i + 1 == segments.Length ? segments[i].ToScene : null));
        }
        var graph = new RoutineGraph(1, spec.Id, spec.GameId, "start", configuration.TimeoutMs,
            configuration.MaxActions, ["Loading", "Unknown"], nodes.ToArray());
        return OperationModules.Define(spec, graph, configuration.Actions);
    }
}
