using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectMaelstrom.Utilities;

internal enum PetDanceArena { WizardCity, Krokotopia, Marleybone, Mooshu, Dragonspyre }
internal enum PetSnackPreference { NeverFeed, SuggestOnly, SelectedSnacks }
internal enum PetTrainingGoal { TrainingXp, Strength, Intellect, Agility, Will, Power }

/// <summary>Local user choices, not observed inventory, identity, a consumption ledger,
/// or authorization to send input. Stat objectives are retained for future verified
/// stat-benefit planning; the existing snack planner currently ranks training XP.</summary>
internal sealed record PetTrainingPreferences(
    int SchemaVersion = 1,
    PetDanceArena Arena = PetDanceArena.WizardCity,
    bool ReturnToNativeIdle = true,
    PetSnackPreference Snacks = PetSnackPreference.NeverFeed,
    PetTrainingGoal Goal = PetTrainingGoal.TrainingXp,
    int MaxFeedsPerVisit = 0,
    int MinimumSnackRemaining = 0,
    int EnergyReserve = 10,
    int? GoalTarget = null,
    string[]? AllowedSnackNames = null,
    string[]? ProtectedSnackNames = null)
{
    internal const string FileName = "pet-training-settings.json";
    internal const int MaximumFileBytes = 65536;
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        MaxDepth = 8,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    [JsonIgnore]
    public string SnackStatus => Snacks switch
    {
        PetSnackPreference.NeverFeed => "Never feed snacks. Leave the snack screen without consuming anything.",
        PetSnackPreference.SuggestOnly => "Review suggestions only. Feeding is not enabled.",
        _ => "Selected-snack preferences saved for review. Automatic feeding still needs verified inventory and consumption checks."
    };

    [JsonIgnore]
    public bool AutomatedFeedingAvailable => false;

    public static PetTrainingPreferences Normalize(PetTrainingPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (preferences.SchemaVersion != 1 || !Enum.IsDefined(preferences.Arena) ||
            !Enum.IsDefined(preferences.Snacks) || !Enum.IsDefined(preferences.Goal))
            throw new InvalidDataException("Unsupported pet training settings.");
        if (preferences.MaxFeedsPerVisit is < 0 or > 100 || preferences.MinimumSnackRemaining is < 0 or > 999999 ||
            preferences.EnergyReserve is < 0 or > 999999 || preferences.GoalTarget is < 0 or > 999999)
            throw new InvalidDataException("Pet training limits are outside the supported range.");
        return preferences with
        {
            AllowedSnackNames = Names(preferences.AllowedSnackNames),
            ProtectedSnackNames = Names(preferences.ProtectedSnackNames)
        };
    }

    /// <summary>Malformed, unsupported, inaccessible or linked settings revert to
    /// NeverFeed defaults without overwriting the original file.</summary>
    public static PetTrainingPreferences Load(string dataRoot)
    {
        try
        {
            var path = SettingsPath(dataRoot);
            if (!File.Exists(path)) return Normalize(new());
            CheckNotLinked(path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > MaximumFileBytes) return Normalize(new());
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("SchemaVersion", out var schema) ||
                !schema.TryGetInt32(out var version) || version != 1 ||
                root.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
                return Normalize(new());
            return Normalize(root.Deserialize<PetTrainingPreferences>(Json) ?? throw new InvalidDataException("Missing preferences."));
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        { return Normalize(new()); }
    }

    public static void Save(string dataRoot, PetTrainingPreferences preferences)
    {
        var normalized = Normalize(preferences);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(normalized, Json);
        if (bytes.Length > MaximumFileBytes) throw new InvalidDataException("Pet training settings exceed 64 KiB.");
        var path = SettingsPath(dataRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        CheckNotLinked(path);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, ".pet-training-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            CheckNotLinked(path);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    /// <summary>Produces only a read-only suggestion using caller-verified name to
    /// profile-key bindings. It never promotes names alone into inventory evidence,
    /// replenishes a consumed allowance, removes observed protection, or feeds.</summary>
    public PetSnackPlan PrepareSnackReview(PetSnackSnapshot snapshot,
        IReadOnlyDictionary<string, string> verifiedNameToProfileKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(verifiedNameToProfileKey);
        var preferences = Normalize(this);
        if (preferences.Snacks == PetSnackPreference.NeverFeed) return NoSuggestion("snacks-disabled");
        if (preferences.Goal != PetTrainingGoal.TrainingXp) return NoSuggestion("stat-goal-needs-verified-benefits");
        if (preferences.MaxFeedsPerVisit == 0) return NoSuggestion("feed-allowance-disabled");
        var bindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in verifiedNameToProfileKey)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value) ||
                !bindings.TryAdd(item.Key.Trim(), item.Value)) return NoSuggestion("ambiguous-snack-name-bindings");
        }
        var allowed = preferences.AllowedSnackNames!.Where(bindings.ContainsKey).Select(name => bindings[name]).ToHashSet(StringComparer.Ordinal);
        var protectedKeys = preferences.ProtectedSnackNames!.Where(bindings.ContainsKey).Select(name => bindings[name]).ToHashSet(StringComparer.Ordinal);
        allowed.ExceptWith(protectedKeys);
        var snacks = snapshot.Snacks?.Select(snack => snack == null ? snack! : snack with
        {
            Protected = snack.Protected || protectedKeys.Contains(snack.ProfileKey),
            MinimumRemaining = snack.MinimumRemaining is { } reserve ? Math.Max(reserve, preferences.MinimumSnackRemaining) : null
        }).ToArray();
        var budget = snapshot.Budget is { } observed ? observed with
        {
            FeedsRemaining = observed.FeedsRemaining is { } remaining ? Math.Min(remaining, preferences.MaxFeedsPerVisit) : null,
            ResourceReserve = observed.ResourceReserve is { } reserve ? Math.Max(reserve, preferences.EnergyReserve) : null
        } : null;
        var progress = snapshot.TrainingXp is { } xp && preferences.GoalTarget.HasValue
            ? xp with { Target = preferences.GoalTarget } : snapshot.TrainingXp;
        return PetSnackPlanner.Plan(PetSnackMode.TrainingXp, snapshot with { Snacks = snacks, Budget = budget, TrainingXp = progress },
            new(snapshot.PetProfileKey, TrainingEnabled: true, AllowedSnackProfileKeys: allowed));
    }

    private static PetSnackPlan NoSuggestion(string reason) => new(null, reason, []);

    private static string[] Names(string[]? names)
    {
        if (names == null) return [];
        if (names.Length > 64 || names.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > 128 || name.Any(char.IsControl)))
            throw new InvalidDataException("Use at most 64 snack names, each with 1–128 characters.");
        return names.Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string SettingsPath(string dataRoot)
    {
        if (string.IsNullOrWhiteSpace(dataRoot) || !Path.IsPathFullyQualified(dataRoot))
            throw new ArgumentException("Choose an absolute Maelstrom data directory.", nameof(dataRoot));
        return Path.Combine(Path.GetFullPath(dataRoot), FileName);
    }

    private static void CheckNotLinked(string path)
    {
        foreach (var candidate in new[] { Path.GetDirectoryName(path)!, path })
            if ((File.Exists(candidate) || Directory.Exists(candidate)) &&
                (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Pet training settings must not be a symbolic link.");
    }
}
