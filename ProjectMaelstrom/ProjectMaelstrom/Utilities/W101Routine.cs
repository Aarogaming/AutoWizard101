using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record W101MotionPrimitive(string Id, string Title, string Key)
{
    public override string ToString() => Title;
}

/// <summary>Small shared motion vocabulary. A routine stores action IDs, never executable code.</summary>
internal sealed record W101Routine(string Id, string Title, string Category, string[] Actions, int Version = 1)
{
    public static readonly string[] Categories = ["Auto-Idle", "Farming", "Pets", "Potions", "Quests"];
    public static readonly W101MotionPrimitive[] Primitives = [new("turn-left", "Turn left · A", "A"), new("turn-right", "Turn right · D", "D"), new("step-forward", "Step forward · W", "W"), new("step-back", "Step backward · S", "S")];
    public static W101MotionPrimitive Resolve(string id) => Primitives.SingleOrDefault(p => p.Id == id) ?? throw new InvalidDataException("Unknown motion action: " + id);
    public static void ValidateActions(string[]? actions)
    {
        if (actions == null || actions.Length is < 1 or > 8) throw new InvalidDataException("A motion routine needs 1–8 actions.");
        foreach (var id in actions) _ = Resolve(id);
    }
    public void Validate()
    {
        TeachingRecording.ValidateId(Id);
        if (Id.Length > 40 || Version != 1 || string.IsNullOrWhiteSpace(Title) || Title.Length > 120 || Title.Any(char.IsControl) || !Categories.Contains(Category))
            throw new InvalidDataException("Choose a category and a routine title of 1–120 characters.");
        ValidateActions(Actions);
    }
    public string Save(string dataRoot)
    {
        Validate();
        var path = Path.Combine(dataRoot, "routines", "wizard101", Id + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); GameExperiments.WriteAtomic(path, this); return path;
    }
    public static W101Routine Load(string path)
    {
        var routine = JsonSerializer.Deserialize<W101Routine>(GameExperiments.BoundedBytes(path, 16384)) ?? throw new InvalidDataException("Invalid motion routine.");
        routine.Validate(); return routine;
    }
    public string ExportPack(string dataRoot, int pulseMs = 60)
    {
        Validate(); new HomeMovementOptions(PulseMs: pulseMs, Mode: HomeIdleMode.Routine, RoutineActions: Actions).Validate();
        var directory = Path.Combine(dataRoot, "game-packs", "wizard101-" + Id + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(directory);
        GameExperiments.WriteAtomic(Path.Combine(directory, "pack.json"), new
        {
            schema_version = 2,
            pack_id = "wizard101-" + Id,
            game_id = "wizard101",
            title = Title,
            version = "0.1.0",
            status = "draft",
            actions = Actions.Distinct().Select(Resolve).Select(p => new { id = p.Id, title = p.Title, key = p.Key, duration_ms = pulseMs }),
            capabilities = Array.Empty<string>(),
            skills = Array.Empty<object>(),
            fixtures = Array.Empty<string>(),
            dependencies = Array.Empty<string>(),
            routine_draft = new { category = Category, sequence = Actions, advance_when = "fresh-scene-change-observed", on_unknown = "pause", note = "Ordered motion draft. Live use is limited to the explicit Auto-Idle session guard. No farming, pet, potion or quest automation is qualified by this export." }
        });
        return directory;
    }
    public override string ToString() => Title;
}
