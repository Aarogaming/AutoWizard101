using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record DodgeAction(string Id, string Key, int DurationMs);

/// <summary>Strict subset of the draft pack format supported for bounded Dodge tests.</summary>
internal sealed class DodgeLivePack
{
    private static readonly HashSet<string> Keys = new(StringComparer.Ordinal) { "LEFT", "RIGHT", "UP", "DOWN", "A", "D", "W", "S", "UP_LEFT", "UP_RIGHT", "DOWN_LEFT", "DOWN_RIGHT" };
    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal) { "move-left", "move-right", "move-up", "move-down", "move-up-left", "move-up-right", "move-down-left", "move-down-right" };
    private readonly Dictionary<string, DodgeAction> _rules;
    public string Id { get; }
    public string Version { get; }
    public string Sha256 { get; }
    public DodgeGameContext? Context { get; }
    internal DodgeLearnedPolicy? LearnedPolicy { get; }
    internal DodgeOutcomePolicy? OutcomePolicy { get; }
    public int NominalDurationMs => _rules.Count == 0 ? 0 : _rules.Values.First().DurationMs;
    private DodgeLivePack(string id, string version, string hash, Dictionary<string, DodgeAction> rules, DodgeGameContext? context, DodgeLearnedPolicy? learnedPolicy, DodgeOutcomePolicy? outcomePolicy) { Id = id; Version = version; Sha256 = hash; _rules = rules; Context = context; LearnedPolicy = learnedPolicy; OutcomePolicy = outcomePolicy; }
    public DodgeAction? Resolve(string advice) => _rules.GetValueOrDefault("dodge-advice-" + advice.ToLowerInvariant());

    public static DodgeLivePack Load(string path)
    {
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Pack is too large.");
        var bytes = File.ReadAllBytes(path);
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        RejectDuplicateFields(json.RootElement);
        var root = json.RootElement;
        if (root.TryGetProperty("recognition", out _)) throw new InvalidDataException("Unsupported recognition assets in this pack version.");
        if (root.GetProperty("schema_version").GetInt32() != 2 || root.GetProperty("game_id").GetString() != "godot-dodge-the-creeps" || root.GetProperty("status").GetString() != "draft")
            throw new InvalidDataException("Choose a draft Dodge schema 2 pack.");
        var id = RequiredText(root, "pack_id");
        var version = RequiredText(root, "version");
        var registry = new Dictionary<string, DodgeAction>(StringComparer.Ordinal);
        var entries = root.GetProperty("actions");
        if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() > 128) throw new InvalidDataException("Invalid action registry.");
        foreach (var entry in entries.EnumerateArray())
        {
            RequireFields(entry, "id", "title", "key", "duration_ms");
            var actionId = RequiredText(entry, "id");
            _ = RequiredText(entry, "title");
            var key = RequiredText(entry, "key");
            var duration = entry.GetProperty("duration_ms").GetInt32();
            if (!Actions.Contains(actionId) || !Keys.Contains(key) || duration is < 1 or > 200 || !registry.TryAdd(actionId, new(actionId, key, duration)))
                throw new InvalidDataException("Dodge tests support unique movement actions with arrow/WASD keys and durations of at most 200 ms.");
        }
        var rules = new Dictionary<string, DodgeAction>(StringComparer.Ordinal);
        var skillIds = new HashSet<string>(StringComparer.Ordinal);
        var skills = root.GetProperty("skills");
        if (skills.ValueKind != JsonValueKind.Array || skills.GetArrayLength() > 128) throw new InvalidDataException("Invalid rules.");
        foreach (var skill in skills.EnumerateArray())
        {
            RequireFields(skill, "id", "when", "propose");
            var skillId = RequiredText(skill, "id");
            if (!System.Text.RegularExpressions.Regex.IsMatch(skillId, "^[a-z0-9][a-z0-9._-]{0,63}$") || !skillIds.Add(skillId))
                throw new InvalidDataException("Dodge skill IDs must be valid and unique.");
            var when = skill.GetProperty("when");
            RequireFields(when, "state");
            var state = RequiredText(when, "state");
            var proposal = skill.GetProperty("propose");
            RequireFields(proposal, "type", "parameters");
            if (proposal.GetProperty("type").GetString() != "action") throw new InvalidDataException("Dodge live tests require named actions.");
            var parameters = proposal.GetProperty("parameters");
            RequireFields(parameters, "action_id");
            var actionId = RequiredText(parameters, "action_id");
            if (state is not ("dodge-advice-left" or "dodge-advice-right" or "dodge-advice-up" or "dodge-advice-down" or "dodge-advice-up-left" or "dodge-advice-up-right" or "dodge-advice-down-left" or "dodge-advice-down-right") || !registry.TryGetValue(actionId, out var action) || !rules.TryAdd(state, action))
                throw new InvalidDataException("Dodge rule is ambiguous or refers to an unsupported action.");
        }
        var context = root.TryGetProperty("context", out var contextElement) ? DodgeGameContext.Parse(contextElement) : null;
        var learnedPolicy = root.TryGetProperty("experimental_model", out var modelElement) ? DodgeLearnedPolicy.Parse(modelElement) : null;
        var outcomePolicy = root.TryGetProperty("experimental_outcome_model", out var outcomeElement) ? DodgeOutcomePolicy.Parse(outcomeElement) : null;
        if ((context?.AvoidancePolicy == "directional-learned-v33") != (learnedPolicy != null))
            throw new InvalidDataException("The experimental learned policy and its model must be supplied together.");
        if ((context?.AvoidancePolicy is "directional-outcome-v34" or "directional-outcome-turn-v36" or "directional-outcome-reference-v37" or "directional-outcome-escape-v38") != (outcomePolicy != null))
            throw new InvalidDataException("The experimental outcome policy and its model must be supplied together.");
        if (outcomePolicy != null && !outcomePolicy.MatchesContext(context!))
            throw new InvalidDataException("The outcome model calibration must match its game context.");
        if (context == null && rules.Values.Any(a => a.Key.Contains('_') || a.Id is "move-up-left" or "move-up-right" or "move-down-left" or "move-down-right")) throw new InvalidDataException("Diagonal movement is limited to the context-aware isolated Dodge test.");
        if (context != null && (rules.Count == 0 || rules.Values.Select(a => a.DurationMs).Distinct().Count() != 1 || rules.Values.Any(a => a.DurationMs > context.MaxActionDurationMs)))
            throw new InvalidDataException("Context-aware Dodge rules require one consistent action duration within the context limit.");
        if (context != null && rules.Values.Any(a => !DodgeDirectionMatches(a)))
            throw new InvalidDataException("Context-aware Dodge movement keys must match their action direction.");
        if (context != null && rules.Any(rule => rule.Key != "dodge-advice-" + rule.Value.Id["move-".Length..]))
            throw new InvalidDataException("Context-aware Dodge advice must resolve to its matching movement direction.");
        return new(id, version, Convert.ToHexString(SHA256.HashData(bytes)), rules, context, learnedPolicy, outcomePolicy);
    }

    private static bool DodgeDirectionMatches(DodgeAction action) => action.Id switch
    {
        "move-left" => action.Key is "LEFT" or "A",
        "move-right" => action.Key is "RIGHT" or "D",
        "move-up" => action.Key is "UP" or "W",
        "move-down" => action.Key is "DOWN" or "S",
        "move-up-left" => action.Key == "UP_LEFT",
        "move-up-right" => action.Key == "UP_RIGHT",
        "move-down-left" => action.Key == "DOWN_LEFT",
        "move-down-right" => action.Key == "DOWN_RIGHT",
        _ => false
    };

    private static string RequiredText(JsonElement item, string name)
    {
        var value = item.GetProperty(name).GetString();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 120) throw new InvalidDataException($"Invalid {name}.");
        return value;
    }
    private static void RequireFields(JsonElement item, params string[] expected)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidDataException("Dodge pack contains unsupported or missing action fields.");
    }
    private static void RejectDuplicateFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Pack contains duplicate fields.");
                RejectDuplicateFields(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var value in element.EnumerateArray()) RejectDuplicateFields(value);
    }
}
