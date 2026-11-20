using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectMaelstrom.Utilities;

internal sealed record ModuleAction(string ActionId, string Kind, string? Key, int DurationMs, string ControlId);
internal sealed record OperationModuleSpec(int SchemaVersion, string Id, string Version, string Kind,
    string GameId, string[] RequiredFacts, JsonElement Configuration);
internal sealed record OperationModuleDefinition(OperationModuleSpec Spec, RoutineGraph Graph, ModuleAction[] Actions);
internal sealed record OperationModuleBundle(int SchemaVersion, string Id, string GameId, string ContextId,
    double TimeoutMs, int MaxActions, OperationModuleSpec[] Modules)
{
    [JsonIgnore]
    public bool LiveQualified => false;
    public RoutineBinding Bind(string targetId, string geometryId, string visitId)
    {
        OperationModules.CheckToken(targetId, 256); OperationModules.CheckToken(geometryId, 256);
        OperationModules.CheckId(visitId); OperationModules.CheckToken(ContextId, 512);
        return new(targetId, geometryId, ContextId + ".visit." + visitId);
    }
}
internal sealed record ModuleObservation(RoutineObservation Observation,
    string[]? VerifiedFacts = null, string[]? VerifiedControls = null);
internal sealed record ModuleDecision(string ModuleId, string NodeId, string Reason,
    string? ProposalId = null, string? ActionId = null, ModuleAction? Action = null,
    bool Completed = false, bool Stopped = false)
{
    public bool IsProposal => ProposalId is not null;
}

/// <summary>Shared data contract. Definitions compile to the existing graph runner;
/// a saved specification never contains executable code or a live permission.</summary>
internal static class OperationModules
{
    internal static readonly JsonSerializerOptions Json = new(RoutineGraphs.Json)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static OperationModuleSpec Spec<T>(string id, string kind, string gameId, T configuration,
        params string[] requiredFacts)
    {
        try
        {
            return new(1, id, "1.0.0", kind, gameId, requiredFacts, JsonSerializer.SerializeToElement(configuration, Json));
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException or NotSupportedException)
        {
            throw new InvalidDataException("Module configuration must be finite, serializable data.", ex);
        }
    }

    public static T ReadConfiguration<T>(OperationModuleSpec spec)
    {
        ValidateSpec(spec);
        return spec.Configuration.Deserialize<T>(Json) ?? throw new InvalidDataException("Missing module configuration.");
    }

    public static OperationModuleDefinition Define(OperationModuleSpec spec, RoutineGraph graph, params ModuleAction[] actions)
    {
        ValidateSpec(spec);
        RoutineGraphs.Validate(graph);
        if (graph.GameId != spec.GameId || graph.Id != spec.Id) throw new InvalidDataException("Module graph identity mismatch.");
        ValidateActions(actions);
        var edges = graph.Nodes.SelectMany(node => node.Edges).ToArray();
        if (edges.Any(edge => !edge.RequiresControl || !edge.RequiresOutcomeBinding || !actions.Any(action => action.ActionId == edge.ActionId)))
            throw new InvalidDataException("Module actions need named controls and bound outcomes.");
        if (actions.Any(action => !edges.Any(edge => edge.ActionId == action.ActionId)))
            throw new InvalidDataException("Module declares an unused action.");
        return new(spec, graph, actions);
    }

    public static void CheckId(string? value) => CheckToken(value, 80);

    internal static void CheckToken(string? value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit ||
            value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.')))
            throw new InvalidDataException("Use a bounded module identifier.");
    }

    public static void ValidateSpec(OperationModuleSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        CheckId(spec.Id); CheckId(spec.GameId);
        if (spec.SchemaVersion != 1 || spec.Version != "1.0.0" ||
            spec.Kind is not ("navigation" or "sigil" or "menu" or "auto-idle") ||
            spec.Configuration.ValueKind != JsonValueKind.Object || spec.Configuration.GetRawText().Length > 65536)
            throw new InvalidDataException("Unsupported module contract or configuration.");
        CheckNames(spec.RequiredFacts, 32);
        RoutineGraphs.CheckProperties(spec.Configuration);
    }

    public static void CheckNames(string[]? values, int limit)
    {
        if (values is null || values.Length > limit || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidDataException("Invalid or repeated module names.");
        foreach (var value in values) CheckId(value);
    }

    public static void ValidateActions(ModuleAction[]? actions)
    {
        if (actions is null || actions.Length is < 1 or > 32 || actions.Any(action => action is null) ||
            actions.Select(action => action.ActionId).Distinct(StringComparer.Ordinal).Count() != actions.Length)
            throw new InvalidDataException("A module needs distinct bounded action bindings.");
        foreach (var action in actions)
        {
            CheckId(action.ActionId); CheckId(action.ControlId);
            if (action.DurationMs is < 1 or > 150 || action.Kind is not ("keyboard-pulse" or "verified-control-click") ||
                (action.Kind == "keyboard-pulse" && (string.IsNullOrWhiteSpace(action.Key) || action.Key.Length > 24 ||
                    action.Key.Any(c => !char.IsAsciiLetterOrDigit(c)))) ||
                (action.Kind == "verified-control-click" && action.Key is not null))
                throw new InvalidDataException("Invalid action binding; no scripts or input delegates are accepted.");
        }
    }
}
