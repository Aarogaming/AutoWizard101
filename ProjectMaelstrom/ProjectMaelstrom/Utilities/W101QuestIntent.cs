using System.Globalization;

namespace ProjectMaelstrom.Utilities;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<W101QuestIntentAction>))]
internal enum W101QuestIntentAction { Unknown, Destination, Explore, Talk, Defeat, Collect, DefeatAndCollect }

/// <summary>Data-only interpretation of one tracked objective. A recognized verb
/// identifies an execution need, never a qualified routine, game entity, quest ID
/// or permission to dispatch. Raw text remains available for review and binding.</summary>
internal sealed record W101QuestIntent
{
    private W101QuestIntent(W101QuestIntentAction action, string? target, string? area,
        string? rawObjective, string? unsupportedReason, string? enemy = null, string? item = null,
        int? currentCount = null, int? requiredCount = null)
    {
        Action = action; Target = target; Area = area;
        RawObjective = rawObjective; UnsupportedReason = unsupportedReason;
        Enemy = enemy; Item = item; CurrentCount = currentCount; RequiredCount = requiredCount;
    }

    internal const int MaximumObjectiveLength = 256;
    public W101QuestIntentAction Action { get; }
    public string? Target { get; }
    public string? Area { get; }
    public string? RawObjective { get; }
    public string? UnsupportedReason { get; }
    public string? Enemy { get; }
    public string? Item { get; }
    public int? CurrentCount { get; }
    public int? RequiredCount { get; }
    public bool IsRecognized => Action != W101QuestIntentAction.Unknown;

    /// <summary>Registry lookup hint only. Unknown text has no execution key.
    /// Interaction/combat/collection may also require navigation; resolving those
    /// prerequisites and admitting a routine remain separate operations.</summary>
    public string? ExecutionKey => Action switch
    {
        W101QuestIntentAction.Destination or W101QuestIntentAction.Explore => "navigate",
        W101QuestIntentAction.Talk => "interact",
        W101QuestIntentAction.Defeat => "combat",
        W101QuestIntentAction.Collect => "collect",
        W101QuestIntentAction.DefeatAndCollect => "combat-collect",
        _ => null
    };

    /// <summary>Potential prerequisites for a future card, not enabled actions.
    /// Navigation is still required to locate the named target in the named area.</summary>
    public IReadOnlyList<string> ExecutionNeeds => Action switch
    {
        W101QuestIntentAction.Destination or W101QuestIntentAction.Explore => ["navigate"],
        W101QuestIntentAction.Talk => ["navigate", "interact"],
        W101QuestIntentAction.Defeat => ["navigate", "combat"],
        W101QuestIntentAction.Collect => ["navigate", "collect"],
        W101QuestIntentAction.DefeatAndCollect => ["navigate", "combat", "collect"],
        _ => []
    };

    /// <summary>A Go To plan stops at independently verified destination arrival;
    /// it does not enter dialogue, interact, fight or follow the next objective.</summary>
    public bool ArrivalOnly => Action == W101QuestIntentAction.Destination;

    /// <summary>Recognizes only verb + target + "in" + area. Case and horizontal
    /// whitespace are normalized for grammar matching, with proper-name case and
    /// raw text preserved. Also recognizes Defeat enemy and Collect item, and an
    /// optional exact (current of total) suffix for defeat/collection progress.
    /// Other counts/compound commands remain unsupported. Names are text, not
    /// inferred catalog identities; progress counts never authorize execution.</summary>
    public static W101QuestIntent Parse(string? objective)
    {
        W101QuestIntent Unknown(string reason) => new(W101QuestIntentAction.Unknown, null, null,
            objective is { Length: <= MaximumObjectiveLength } ? objective : null, reason);
        if (objective is { Length: > MaximumObjectiveLength }) return Unknown("objective-too-long");
        if (string.IsNullOrWhiteSpace(objective)) return Unknown("missing-objective");
        if (objective.Any(c => char.IsControl(c) && c != '\t' || c is '\u2028' or '\u2029'))
            return Unknown("invalid-objective-text");
        var text = string.Join(" ", objective.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.EndsWith('.')) text = text[..^1];
        int? currentCount = null, requiredCount = null;
        var countStart = text.LastIndexOf(" (", StringComparison.Ordinal);
        if (countStart >= 0)
        {
            if (!text.EndsWith(')')) return Unknown("unsupported-count");
            var parts = text[(countStart + 2)..^1].Split(' ');
            bool Digits(string value) => value.Length is >= 1 and <= 5 && value.All(char.IsAsciiDigit);
            if (parts.Length != 3 || !parts[1].Equals("of", StringComparison.OrdinalIgnoreCase) ||
                !Digits(parts[0]) || !Digits(parts[2]) ||
                !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var current) ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var required) ||
                required is < 1 or > 10000 || current > required)
                return Unknown("unsupported-count");
            currentCount = current; requiredCount = required;
            text = text[..countStart];
            if (text.EndsWith('.')) text = text[..^1];
        }
        (string Prefix, W101QuestIntentAction Action)[] verbs =
        [
            ("Go To ", W101QuestIntentAction.Destination),
            ("Explore ", W101QuestIntentAction.Explore),
            ("Talk To ", W101QuestIntentAction.Talk),
            ("Defeat ", W101QuestIntentAction.Defeat),
            ("Collect ", W101QuestIntentAction.Collect)
        ];
        var verb = verbs.FirstOrDefault(item => text.StartsWith(item.Prefix, StringComparison.OrdinalIgnoreCase));
        if (verb.Action == W101QuestIntentAction.Unknown) return Unknown("unsupported-verb");
        var body = text[verb.Prefix.Length..];
        var separator = body.IndexOf(" in ", StringComparison.OrdinalIgnoreCase);
        if (separator < 1 || separator + 4 >= body.Length) return Unknown("missing-target-or-area");
        if (body.IndexOf(" in ", separator + 4, StringComparison.OrdinalIgnoreCase) >= 0)
            return Unknown("ambiguous-area");
        var target = body[..separator]; var area = body[(separator + 4)..];
        var action = verb.Action;
        string? enemy = action == W101QuestIntentAction.Defeat ? target : null;
        string? item = action == W101QuestIntentAction.Collect ? target : null;
        if (action == W101QuestIntentAction.Defeat)
        {
            var collectAt = target.IndexOf(" and Collect ", StringComparison.OrdinalIgnoreCase);
            if (collectAt >= 0)
            {
                enemy = target[..collectAt]; item = target[(collectAt + 13)..];
                action = W101QuestIntentAction.DefeatAndCollect;
            }
        }
        var counted = action is W101QuestIntentAction.Defeat or W101QuestIntentAction.Collect or W101QuestIntentAction.DefeatAndCollect;
        if (currentCount.HasValue && !counted) return Unknown("unsupported-count");
        if (counted && (target.Any(char.IsDigit) || area.Any(char.IsDigit) ||
            enemy is not null && HasCountPrefix(enemy) || item is not null && HasCountPrefix(item)))
            return Unknown("unsupported-count");
        if (!Name(area) || (action == W101QuestIntentAction.DefeatAndCollect
            ? !Name(enemy!) || !Name(item!) : !Name(target)))
            return Unknown("unsupported-name-or-compound-objective");
        return new(action, target, area, objective, null, enemy, item, currentCount, requiredCount);
    }

    private static bool HasCountPrefix(string target)
    {
        var first = target.Split(' ')[0];
        return new[] { "a", "an", "all", "some", "one", "two", "three", "four", "five", "six",
            "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen",
            "sixteen", "seventeen", "eighteen", "nineteen", "twenty" }.Contains(first, StringComparer.OrdinalIgnoreCase);
    }

    private static bool Name(string value) => value.Any(char.IsLetter) && value.All(c =>
        char.IsLetterOrDigit(c) || c is ' ' or '\'' or '\u2019' or '-' or '.' ||
        char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark) &&
        !value.Split(' ').Any(word => word.Equals("then", StringComparison.OrdinalIgnoreCase) ||
            word.Equals("and", StringComparison.OrdinalIgnoreCase) || word.Equals("or", StringComparison.OrdinalIgnoreCase));
}
